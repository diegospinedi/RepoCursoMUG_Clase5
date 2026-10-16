using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Optica.Application.Acceso;

namespace Optica.Api.Seguridad;

/// <summary>
/// Sesión por cookie, autorización por defecto y antiforgery (FR-01, NFR-03, threat model C2).
/// </summary>
/// <remarks>
/// <para>
/// La cookie <c>optica.sesion</c> va cifrada y firmada con Data Protection y lleva el <c>SelloSeguridad</c> de la
/// credencial en un claim. En cada pedido autenticado <see cref="ValidarPrincipalAsync"/> lo compara con el de la
/// base: si la contraseña se cambió o se blanqueó, la sesión se rechaza y la cookie se borra (E4-2). El sello
/// nunca se loguea.
/// </para>
/// <para>
/// Vencimiento: 60 minutos sin actividad. <c>SlidingExpiration</c> está apagado y la renovación es explícita
/// (<c>ShouldRenew = true</c>) en cada pedido con sesión válida, así cada pedido corre el vencimiento 60 minutos.
/// </para>
/// <para>
/// <c>SecurePolicy = None</c>: el servidor escucha solo en la interfaz loopback y no hay TLS (riesgo aceptado
/// AR-01). Si el sistema pasa a escuchar en otra interfaz, esto se tiene que revisar junto con AR-01.
/// </para>
/// </remarks>
public static class ConfiguracionSesion
{
    public const string Esquema = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string NombreCookie = "optica.sesion";
    public const string ClaimSello = "optica:sello";
    public const string NombreCookieAntiforgery = "optica.antiforgery";
    public const string NombreCookieXsrf = "XSRF-TOKEN";
    public const string HeaderXsrf = "X-XSRF-TOKEN";

    // Identificador fijo de la única identidad del sistema (contraseña compartida, sin usuarios). El token
    // antiforgery se ata a este valor, no al sello: un cambio de contraseña no invalida el token vigente.
    private const string IdentificadorOperadora = "operadora";

    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(60);

    public static IServiceCollection AddSesionOptica(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios
            .AddAuthentication(Esquema)
            .AddCookie(Esquema, opciones =>
            {
                opciones.Cookie.Name = NombreCookie;
                opciones.Cookie.HttpOnly = true;
                opciones.Cookie.SameSite = SameSiteMode.Strict;
                opciones.Cookie.SecurePolicy = CookieSecurePolicy.None; // AR-01: solo loopback.
                opciones.Cookie.Path = "/";
                opciones.Cookie.IsEssential = true;
                opciones.ExpireTimeSpan = Duracion;
                opciones.SlidingExpiration = false;
                opciones.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidarPrincipalAsync,
                    OnRedirectToLogin = contexto => ResponderSinRedireccion(contexto, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = contexto => ResponderSinRedireccion(contexto, StatusCodes.Status403Forbidden),
                };
            });

        // El vencimiento usa el mismo reloj que el resto del sistema (FakeTimeProvider en las pruebas).
        servicios.AddOptions<CookieAuthenticationOptions>(Esquema)
            .Configure<TimeProvider>((opciones, reloj) => opciones.TimeProvider = reloj);

        // Todo endpoint exige sesión salvo los marcados explícitamente con [AllowAnonymous] (lista blanca).
        servicios.AddAuthorization(opciones =>
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        servicios.AddAntiforgery(opciones =>
        {
            opciones.HeaderName = HeaderXsrf;
            opciones.Cookie.Name = NombreCookieAntiforgery;
            opciones.Cookie.HttpOnly = true;
            opciones.Cookie.SameSite = SameSiteMode.Strict;
            opciones.Cookie.SecurePolicy = CookieSecurePolicy.None; // AR-01: solo loopback.
            opciones.Cookie.IsEssential = true;
            opciones.SuppressXFrameOptionsHeader = true; // Lo fija EncabezadosSeguridad (DENY).
        });

        return servicios;
    }

    /// <summary>Inicia (o reemite) la sesión con el sello vigente y la deja como usuario del pedido en curso.</summary>
    public static async Task IniciarAsync(HttpContext contexto, Guid sello)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, IdentificadorOperadora),
                new Claim(ClaimSello, sello.ToString("D")),
            ],
            Esquema);
        var principal = new ClaimsPrincipal(identidad);

        await contexto.SignInAsync(Esquema, principal, new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        contexto.User = principal;
    }

    /// <summary>Cierra la sesión: borra la cookie y deja al pedido en curso como anónimo.</summary>
    public static async Task CerrarAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        await contexto.SignOutAsync(Esquema);
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <summary>
    /// Emite el token antiforgery para el usuario actual del pedido: la cookie <c>XSRF-TOKEN</c> (legible por el
    /// SPA, que la copia en el header <c>X-XSRF-TOKEN</c>) y la cookie <c>HttpOnly</c> de antiforgery.
    /// </summary>
    public static void EmitirTokenXsrf(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var tokens = contexto.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(contexto);
        contexto.Response.Cookies.Append(
            NombreCookieXsrf,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = false, // AR-01: solo loopback.
                Path = "/",
                IsEssential = true,
            });
    }

    private static async Task ValidarPrincipalAsync(CookieValidatePrincipalContext contexto)
    {
        var valor = contexto.Principal?.FindFirst(ClaimSello)?.Value;
        var valida = Guid.TryParse(valor, out var sello)
            && await contexto.HttpContext.RequestServices.GetRequiredService<ValidarSesion>().EjecutarAsync(sello);

        if (!valida)
        {
            contexto.RejectPrincipal();
            await contexto.HttpContext.SignOutAsync(Esquema);
            return;
        }

        contexto.ShouldRenew = true;
    }

    private static Task ResponderSinRedireccion(RedirectContext<CookieAuthenticationOptions> contexto, int estado)
    {
        contexto.Response.StatusCode = estado;
        return Task.CompletedTask;
    }
}
