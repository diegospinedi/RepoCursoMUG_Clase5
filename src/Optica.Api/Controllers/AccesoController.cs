using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optica.Api.Errores;
using Optica.Api.Seguridad;
using Optica.Application.Acceso;
using Optica.Application.Comun;
using Optica.Domain.Acceso;
using Optica.Domain.Comun;

namespace Optica.Api.Controllers;

// Los cuerpos con secretos son clases, no records: MVC loguea los argumentos de la acción en nivel Trace con
// ToString(), y el de un record incluye los valores. El ToString() de una clase solo muestra el nombre del tipo.
// Lo verifica Dtos_ToStringNoMuestraTextos para todo cuerpo de cualquier controlador.

/// <summary>Cuerpo de la creación inicial y del ingreso.</summary>
public sealed class PedidoContrasena(string contrasena)
{
    public string Contrasena { get; } = contrasena;
}

/// <summary>Cuerpo del cambio de contraseña.</summary>
public sealed class PedidoCambioContrasena(string contrasenaActual, string contrasenaNueva)
{
    public string ContrasenaActual { get; } = contrasenaActual;

    public string ContrasenaNueva { get; } = contrasenaNueva;
}

/// <summary>Estado público del acceso: no incluye ningún dato de negocio.</summary>
public sealed record RespuestaEstadoAcceso(bool ContrasenaDefinida, string? BloqueadoHasta, bool SesionActiva);

/// <summary>
/// Acceso con la contraseña única (FR-01 a FR-05, NFR-02). Los casos de uso se inyectan por acción, así cada
/// endpoint declara exactamente qué operación sobre la credencial puede ejecutar (AC-08). No existe ningún
/// endpoint de blanqueo: el blanqueo es el comando local <c>--blanquear-contrasena</c> (FR-06).
/// </summary>
[ApiController]
[Route("api/acceso")]
public sealed class AccesoController(ILogger<AccesoController> registro, TimeProvider reloj) : ControllerBase
{
    public const string CampoContrasena = "contrasena";
    public const string MensajeMaximo = "La contraseña puede tener como máximo 128 caracteres";

    /// <summary>Estado del acceso para el SPA; emite el token antiforgery.</summary>
    [HttpGet("ingreso")]
    [AllowAnonymous]
    public async Task<RespuestaEstadoAcceso> ObtenerEstado([FromServices] ObtenerEstadoAcceso obtenerEstado)
    {
        var estado = await obtenerEstado.EjecutarAsync();
        ConfiguracionSesion.EmitirTokenXsrf(HttpContext);

        return new RespuestaEstadoAcceso(
            estado.ContrasenaDefinida,
            estado.BloqueadoHasta is { } hasta ? Problemas.FechaUtc(hasta) : null,
            User.Identity?.IsAuthenticated == true);
    }

    /// <summary>Crea la contraseña cuando todavía no existe. No inicia sesión.</summary>
    [HttpPost("contrasena-inicial")]
    [AllowAnonymous]
    public async Task<IActionResult> CrearContrasenaInicial(PedidoContrasena pedido, [FromServices] CrearContrasenaInicial crear)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ValidarLargo((CampoContrasena, pedido.Contrasena));

        try
        {
            await crear.EjecutarAsync(pedido.Contrasena);
        }
        catch (ErrorValidacion error) when (error.Campo == ContrasenaNueva.Campo)
        {
            // En este endpoint el campo se llama "contrasena".
            throw new ErrorValidacion(CampoContrasena, error.Mensaje);
        }

        registro.ContrasenaCreada(reloj.GetUtcNow());
        return NoContent();
    }

    /// <summary>Ingresa con la contraseña: emite la cookie de sesión y un token antiforgery nuevo.</summary>
    [HttpPost("ingreso")]
    [AllowAnonymous]
    public async Task<IActionResult> Ingresar(PedidoContrasena pedido, [FromServices] IniciarSesion iniciarSesion)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ValidarLargo((CampoContrasena, pedido.Contrasena));

        Guid sello;
        try
        {
            sello = await iniciarSesion.EjecutarAsync(pedido.Contrasena);
        }
        catch (ContrasenaIncorrecta error)
        {
            registro.IngresoFallido(error.IntentosRestantes, reloj.GetUtcNow());
            throw;
        }
        catch (IngresoBloqueado error)
        {
            registro.IngresoBloqueado(error.Hasta, reloj.GetUtcNow());
            throw;
        }

        await ConfiguracionSesion.IniciarAsync(HttpContext, sello);
        ConfiguracionSesion.EmitirTokenXsrf(HttpContext);
        registro.IngresoExitoso(reloj.GetUtcNow());
        return NoContent();
    }

    /// <summary>Cierra la sesión.</summary>
    [HttpPost("salida")]
    public async Task<IActionResult> Salir()
    {
        await ConfiguracionSesion.CerrarAsync(HttpContext);
        ConfiguracionSesion.EmitirTokenXsrf(HttpContext);
        registro.SesionCerrada(reloj.GetUtcNow());
        return NoContent();
    }

    /// <summary>Cambia la contraseña; reemite la cookie con el sello nuevo (las demás sesiones quedan inválidas).</summary>
    [HttpPut("contrasena")]
    public async Task<IActionResult> CambiarContrasena(PedidoCambioContrasena pedido, [FromServices] CambiarContrasena cambiarContrasena)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ValidarLargo((ContrasenaActualIncorrecta.Campo, pedido.ContrasenaActual), (ContrasenaNueva.Campo, pedido.ContrasenaNueva));

        var sello = await cambiarContrasena.EjecutarAsync(pedido.ContrasenaActual, pedido.ContrasenaNueva);

        await ConfiguracionSesion.IniciarAsync(HttpContext, sello);
        ConfiguracionSesion.EmitirTokenXsrf(HttpContext);
        registro.ContrasenaCambiada(reloj.GetUtcNow());
        return NoContent();
    }

    // Ninguna contraseña de más de 128 caracteres llega a PBKDF2 (tampoco en el ingreso, donde no se aplica el
    // mínimo). Rechazarla acá no cuenta como intento de ingreso.
    private static void ValidarLargo(params (string Campo, string? Valor)[] campos)
    {
        var errores = campos
            .Where(c => c.Valor?.Length > ContrasenaNueva.LongitudMaxima)
            .Select(c => new ErrorValidacion(c.Campo, MensajeMaximo))
            .ToArray();

        if (errores.Length == 1)
        {
            throw errores[0];
        }

        if (errores.Length > 1)
        {
            throw new ErroresValidacion(errores);
        }
    }
}
