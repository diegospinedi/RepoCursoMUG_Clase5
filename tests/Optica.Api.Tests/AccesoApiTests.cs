using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Optica.Domain.Acceso;
using Optica.Infrastructure.Seguridad;

namespace Optica.Api.Tests;

public class AccesoApiTests
{
    public const string Contrasena = "clave-correcta-1";
    public const string OtraContrasena = "otra-clave-segura-2";
    public const string ContrasenaErronea = "clave-erronea-9";

    private const string MensajeMinimo = "La contraseña debe tener al menos 8 caracteres";

    // ---- Estado y creación inicial ----

    [Fact]
    public async Task Estado_SinContrasena_ContrasenaDefinidaFalse()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.ObtenerEstadoAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(estado.GetProperty("contrasenaDefinida").GetBoolean());
        Assert.Equal(JsonValueKind.Null, estado.GetProperty("bloqueadoHasta").ValueKind);
        Assert.False(estado.GetProperty("sesionActiva").GetBoolean());

        // Solo los 3 campos documentados: ningún dato de negocio (threat model C2/I).
        Assert.Equal(
            ["bloqueadoHasta", "contrasenaDefinida", "sesionActiva"],
            estado.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());

        // Emite el token antiforgery en la cookie legible por el SPA.
        Assert.False(string.IsNullOrEmpty(cliente.TokenXsrf), "GET /api/acceso/ingreso no emitió la cookie XSRF-TOKEN");
    }

    [Fact]
    public async Task ContrasenaInicial_Valida_Devuelve204()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.CrearContrasenaAsync(Contrasena);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.True((await cliente.EstadoAsync()).GetProperty("contrasenaDefinida").GetBoolean());

        // No inicia sesión: la UI pasa a la pantalla de ingreso.
        Assert.Null(cliente.ValorCookieSesion);
        using var configuracion = await cliente.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, configuracion.StatusCode);

        // La contraseña creada es la que se acepta en el ingreso.
        await cliente.IniciarSesionAsync(Contrasena);
    }

    [Fact]
    public async Task ContrasenaInicial_7Caracteres_Devuelve400ConCampo()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.CrearContrasenaAsync("1234567");

        var errores = await Problemas.ErroresAsync(respuesta);
        Assert.Equal([MensajeMinimo], errores["contrasena"]);
        Assert.False((await cliente.EstadoAsync()).GetProperty("contrasenaDefinida").GetBoolean());
    }

    [Fact]
    public async Task ContrasenaInicial_YaDefinida_Devuelve409YNoCambiaLaContrasena()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        using (var primera = await cliente.CrearContrasenaAsync(Contrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        }

        using var segunda = await cliente.CrearContrasenaAsync(OtraContrasena);

        var problema = await Problemas.LeerAsync(segunda, HttpStatusCode.Conflict);
        Assert.Equal("La contraseña ya está definida", problema.GetProperty("title").GetString());

        using (var conLaSegunda = await cliente.IngresarAsync(OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, conLaSegunda.StatusCode);
        }

        await cliente.IniciarSesionAsync(Contrasena);
    }

    [Fact]
    public async Task ContrasenaInicial_Concurrente_SoloUnaGana()
    {
        await using var fabrica = new FabricaApi();
        using var clienteA = fabrica.CrearCliente();
        using var clienteB = fabrica.CrearCliente();
        await clienteA.PrepararAntiforgeryAsync();
        await clienteB.PrepararAntiforgeryAsync();

        var pedidoA = clienteA.CrearContrasenaAsync(Contrasena);
        var pedidoB = clienteB.CrearContrasenaAsync(OtraContrasena);
        var respuestas = await Task.WhenAll(pedidoA, pedidoB);

        var estados = respuestas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();
        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], estados);

        var (ganadora, perdedora) = respuestas[0].StatusCode == HttpStatusCode.NoContent
            ? (Contrasena, OtraContrasena)
            : (OtraContrasena, Contrasena);
        using var cliente = fabrica.CrearCliente();
        using (var conLaPerdedora = await cliente.IngresarAsync(perdedora))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, conLaPerdedora.StatusCode);
        }

        await cliente.IniciarSesionAsync(ganadora);
        foreach (var respuesta in respuestas)
        {
            respuesta.Dispose();
        }
    }

    // ---- Ingreso y bloqueo ----

    [Fact]
    public async Task Ingreso_SinContrasenaCreada_Devuelve409()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();

        using var respuesta = await cliente.IngresarAsync(Contrasena);

        var problema = await Problemas.LeerAsync(respuesta, HttpStatusCode.Conflict);
        Assert.Equal("Todavía no se creó la contraseña", problema.GetProperty("title").GetString());
        Assert.Null(cliente.ValorCookieSesion);
    }

    [Fact]
    public async Task Ingreso_Incorrecta_Devuelve401ConIntentosRestantes()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConContrasenaAsync(fabrica);

        using var primera = await cliente.IngresarAsync(ContrasenaErronea);
        var problema = await Problemas.LeerAsync(primera, HttpStatusCode.Unauthorized);
        Assert.Equal("La contraseña no es correcta", problema.GetProperty("title").GetString());
        Assert.Equal(4, problema.GetProperty("intentosRestantes").GetInt32());

        using var segunda = await cliente.IngresarAsync(ContrasenaErronea);
        Assert.Equal(3, (await Problemas.LeerAsync(segunda, HttpStatusCode.Unauthorized)).GetProperty("intentosRestantes").GetInt32());

        Assert.Null(cliente.ValorCookieSesion);
    }

    [Fact]
    public async Task Ingreso_5Fallos_Bloquea_LiberaA5Minutos()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConContrasenaAsync(fabrica);
        var inicio = fabrica.Reloj.GetUtcNow();
        var liberacion = inicio.AddMinutes(5);

        for (var intento = 1; intento <= 4; intento++)
        {
            using var fallo = await cliente.IngresarAsync(ContrasenaErronea);
            Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
        }

        using (var quinto = await cliente.IngresarAsync(ContrasenaErronea))
        {
            var problema = await Problemas.LeerAsync(quinto, (HttpStatusCode)423);
            Assert.Equal("Ingreso bloqueado", problema.GetProperty("title").GetString());
            AssertFechaUtc(liberacion, problema.GetProperty("bloqueadoHasta").GetString());
        }

        AssertFechaUtc(liberacion, (await cliente.EstadoAsync()).GetProperty("bloqueadoHasta").GetString());

        // A los 4:59 la contraseña correcta también se rechaza.
        fabrica.Reloj.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        using (var bloqueado = await cliente.IngresarAsync(Contrasena))
        {
            Assert.Equal((HttpStatusCode)423, bloqueado.StatusCode);
        }

        Assert.Null(cliente.ValorCookieSesion);

        // A los 5:00 se libera solo.
        fabrica.Reloj.Advance(TimeSpan.FromSeconds(1));
        await cliente.IniciarSesionAsync(Contrasena);
        Assert.Equal(JsonValueKind.Null, (await cliente.EstadoAsync()).GetProperty("bloqueadoHasta").ValueKind);
    }

    [Fact]
    public async Task Ingreso_FallosConcurrentes_NoSuperan5()
    {
        var hasher = new HasherContador();
        await using var fabrica = new FabricaApi(servicios: s => s.AddSingleton<IHasherContrasena>(hasher));
        using var cliente = await ConContrasenaAsync(fabrica);
        await cliente.PrepararAntiforgeryAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => cliente.IngresarAsync(ContrasenaErronea)));

        Assert.True(hasher.Verificaciones <= 5, $"Se verificó el hash {hasher.Verificaciones} veces con 10 pedidos simultáneos");
        Assert.Equal(4, respuestas.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
        Assert.Equal(6, respuestas.Count(r => r.StatusCode == (HttpStatusCode)423));

        using var correcta = await cliente.IngresarAsync(Contrasena);
        Assert.Equal((HttpStatusCode)423, correcta.StatusCode);
        Assert.True(hasher.Verificaciones <= 5);
        foreach (var respuesta in respuestas)
        {
            respuesta.Dispose();
        }
    }

    // ---- Sesión ----

    [Fact]
    public async Task Sesion_60MinutosInactiva_Devuelve401()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConContrasenaAsync(fabrica);
        using (var ingreso = await cliente.IngresarAsync(Contrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, ingreso.StatusCode);
            var cookies = ingreso.Headers.GetValues("Set-Cookie").ToArray();

            // Cookie de sesión: HttpOnly, SameSite=Strict, de sesión del navegador y sin Secure (AR-01: solo loopback).
            var sesion = Atributos(Assert.Single(cookies, c => c.StartsWith(ClienteApi.CookieSesion + "=", StringComparison.Ordinal)));
            Assert.Contains("httponly", sesion);
            Assert.Contains("samesite=strict", sesion);
            Assert.Contains("path=/", sesion);
            Assert.DoesNotContain("secure", sesion);
            Assert.DoesNotContain(sesion, a => a.StartsWith("expires=", StringComparison.Ordinal));

            // El sello viaja solo dentro de la cookie protegida con Data Protection, nunca en claro.
            using var conexion = fabrica.Directorios.AbrirConexionCruda();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT SelloSeguridad FROM Credencial WHERE Id = 1";
            var sello = Guid.Parse((string)comando.ExecuteScalar()!);
            Assert.All(cookies, c => Assert.DoesNotContain(sello.ToString("D"), c, StringComparison.OrdinalIgnoreCase));
            Assert.All(cookies, c => Assert.DoesNotContain(sello.ToString("N"), c, StringComparison.OrdinalIgnoreCase));

            // El token antiforgery sí lo lee el SPA: no es HttpOnly.
            var xsrf = Atributos(Assert.Single(cookies, c => c.StartsWith(ClienteApi.CookieXsrf + "=", StringComparison.Ordinal)));
            Assert.DoesNotContain("httponly", xsrf);
            Assert.Contains("samesite=strict", xsrf);
        }

        using (var activa = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, activa.StatusCode);
        }

        fabrica.Reloj.Advance(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(1));

        using var vencida = await cliente.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, vencida.StatusCode);
        Assert.Null(vencida.Headers.Location);
        Assert.False((await cliente.EstadoAsync()).GetProperty("sesionActiva").GetBoolean());
    }

    [Fact]
    public async Task Sesion_ActividadRenuevaElVencimiento()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConSesionAsync(fabrica);

        fabrica.Reloj.Advance(TimeSpan.FromMinutes(50));
        using (var a50 = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, a50.StatusCode);
        }

        fabrica.Reloj.Advance(TimeSpan.FromMinutes(50));
        using (var a100 = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, a100.StatusCode);
        }

        Assert.True((await cliente.EstadoAsync()).GetProperty("sesionActiva").GetBoolean());

        // La renovación no la vuelve eterna: 60 minutos sin actividad la cierran.
        fabrica.Reloj.Advance(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(1));
        using var vencida = await cliente.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, vencida.StatusCode);
    }

    // ---- Cambio de contraseña ----

    [Fact]
    public async Task CambiarContrasena_IngresoSiguienteSoloConLaNueva()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConSesionAsync(fabrica);

        using (var cambio = await cliente.CambiarContrasenaAsync(Contrasena, OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        }

        // La sesión de quien cambió sigue activa (la cookie se reemite con el sello nuevo).
        using (var sigue = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, sigue.StatusCode);
        }

        using (var salida = await cliente.SalirAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, salida.StatusCode);
        }

        using (var sinSesion = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        }

        await cliente.PrepararAntiforgeryAsync();
        using (var conLaAnterior = await cliente.IngresarAsync(Contrasena))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, conLaAnterior.StatusCode);
        }

        await cliente.IniciarSesionAsync(OtraContrasena);
    }

    [Fact]
    public async Task CambiarContrasena_OtraSesionQuedaInvalida()
    {
        await using var fabrica = new FabricaApi();
        using var quienCambia = await ConSesionAsync(fabrica);
        using var otra = fabrica.CrearCliente();
        await otra.IniciarSesionAsync(Contrasena);
        using (var antes = await otra.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        using (var cambio = await quienCambia.CambiarContrasenaAsync(Contrasena, OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        }

        using (var deQuienCambio = await quienCambia.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, deQuienCambio.StatusCode);
        }

        using var deLaOtra = await otra.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, deLaOtra.StatusCode);

        // E4-2: la cookie con el sello anterior se borra.
        Assert.Null(otra.ValorCookieSesion);
    }

    [Fact]
    public async Task CambiarContrasena_ActualIncorrecta_Devuelve400ConCampo()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConSesionAsync(fabrica);

        using (var respuesta = await cliente.CambiarContrasenaAsync(ContrasenaErronea, OtraContrasena))
        {
            var errores = await Problemas.ErroresAsync(respuesta);
            Assert.Equal(["La contraseña actual no es correcta"], errores["contrasenaActual"]);
            Assert.False(errores.ContainsKey("contrasenaNueva"));
        }

        // Si fallan las dos, se informan los dos campos.
        using (var ambas = await cliente.CambiarContrasenaAsync(ContrasenaErronea, "1234567"))
        {
            var errores = await Problemas.ErroresAsync(ambas);
            Assert.Equal(["La contraseña actual no es correcta"], errores["contrasenaActual"]);
            Assert.Equal([MensajeMinimo], errores["contrasenaNueva"]);
        }

        // La sesión sigue y la contraseña anterior es la única aceptada.
        using (var sigue = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, sigue.StatusCode);
        }

        using var otro = fabrica.CrearCliente();
        using (var conLaNueva = await otro.IngresarAsync(OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, conLaNueva.StatusCode);
        }

        await otro.IniciarSesionAsync(Contrasena);
    }

    [Fact]
    public async Task CambiarContrasena_Nueva7Caracteres_Devuelve400ConCampo()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await ConSesionAsync(fabrica);

        using (var corta = await cliente.CambiarContrasenaAsync(Contrasena, "1234567"))
        {
            var errores = await Problemas.ErroresAsync(corta);
            Assert.Equal([MensajeMinimo], errores["contrasenaNueva"]);
            Assert.False(errores.ContainsKey("contrasenaActual"));
        }

        // Límite de 128 caracteres en los dos campos.
        using (var larga = await cliente.CambiarContrasenaAsync(Contrasena, new string('n', 129)))
        {
            var errores = await Problemas.ErroresAsync(larga);
            Assert.Equal(["La contraseña puede tener como máximo 128 caracteres"], errores["contrasenaNueva"]);
        }

        using (var actualLarga = await cliente.CambiarContrasenaAsync(new string('a', 129), OtraContrasena))
        {
            var errores = await Problemas.ErroresAsync(actualLarga);
            Assert.Equal(["La contraseña puede tener como máximo 128 caracteres"], errores["contrasenaActual"]);
        }

        using var otro = fabrica.CrearCliente();
        await otro.IniciarSesionAsync(Contrasena);
    }

    // ---- Ayudas ----

    public static async Task<ClienteApi> ConContrasenaAsync(FabricaApi fabrica)
    {
        var cliente = fabrica.CrearCliente();
        using var respuesta = await cliente.CrearContrasenaAsync(Contrasena);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        return cliente;
    }

    public static async Task<ClienteApi> ConSesionAsync(FabricaApi fabrica)
    {
        var cliente = await ConContrasenaAsync(fabrica);
        await cliente.IniciarSesionAsync(Contrasena);
        return cliente;
    }

    private static string[] Atributos(string setCookie) =>
        setCookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToArray();

    private static void AssertFechaUtc(DateTimeOffset esperada, string? texto)
    {
        Assert.NotNull(texto);
        Assert.EndsWith("Z", texto, StringComparison.Ordinal);
        Assert.Equal(esperada, DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture));
    }
}

/// <summary>Hasher real que cuenta cuántas veces se verificó una contraseña.</summary>
public sealed class HasherContador : IHasherContrasena
{
    private readonly HasherPbkdf2 _real = new();
    private int _verificaciones;

    public int Verificaciones => Volatile.Read(ref _verificaciones);

    public string Hashear(ContrasenaNueva contrasena) => _real.Hashear(contrasena);

    public bool Verificar(string contrasena, string hash)
    {
        Interlocked.Increment(ref _verificaciones);
        return _real.Verificar(contrasena, hash);
    }
}

/// <summary>Lectura de respuestas <c>application/problem+json</c> (RFC 9457).</summary>
public static class Problemas
{
    public static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta, HttpStatusCode estado)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)estado, problema.GetProperty("status").GetInt32());
        return problema;
    }

    public static async Task<Dictionary<string, string[]>> ErroresAsync(HttpResponseMessage respuesta)
    {
        var problema = await LeerAsync(respuesta, HttpStatusCode.BadRequest);
        Assert.True(problema.TryGetProperty("errors", out var errores), "El 400 no trae 'errors' por campo");
        return errores.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.EnumerateArray().Select(m => m.GetString()!).ToArray());
    }
}
