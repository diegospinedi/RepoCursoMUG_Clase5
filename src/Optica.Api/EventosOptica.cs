namespace Optica.Api;

/// <summary>
/// Eventos de acceso y configuración que quedan en el log (repudio, threat model C2/R y AR-03): fecha y hora,
/// acción y resultado. Nunca reciben contraseñas, hashes, sellos ni cookies como parámetro.
/// </summary>
internal static partial class EventosOptica
{
    [LoggerMessage(EventId = 1001, EventName = "ContrasenaCreada", Level = LogLevel.Information,
        Message = "Se creó la contraseña de acceso ({Momento:O})")]
    public static partial void ContrasenaCreada(this ILogger registro, DateTimeOffset momento);

    [LoggerMessage(EventId = 1002, EventName = "IngresoExitoso", Level = LogLevel.Information,
        Message = "Ingreso exitoso ({Momento:O})")]
    public static partial void IngresoExitoso(this ILogger registro, DateTimeOffset momento);

    [LoggerMessage(EventId = 1003, EventName = "IngresoFallido", Level = LogLevel.Warning,
        Message = "Ingreso fallido; quedan {IntentosRestantes} intentos ({Momento:O})")]
    public static partial void IngresoFallido(this ILogger registro, int intentosRestantes, DateTimeOffset momento);

    [LoggerMessage(EventId = 1004, EventName = "IngresoBloqueado", Level = LogLevel.Warning,
        Message = "Ingreso bloqueado hasta {Hasta:O} ({Momento:O})")]
    public static partial void IngresoBloqueado(this ILogger registro, DateTimeOffset hasta, DateTimeOffset momento);

    [LoggerMessage(EventId = 1005, EventName = "ContrasenaCambiada", Level = LogLevel.Information,
        Message = "Se cambió la contraseña de acceso; las demás sesiones quedan cerradas ({Momento:O})")]
    public static partial void ContrasenaCambiada(this ILogger registro, DateTimeOffset momento);

    [LoggerMessage(EventId = 1006, EventName = "SesionCerrada", Level = LogLevel.Information,
        Message = "Sesión cerrada ({Momento:O})")]
    public static partial void SesionCerrada(this ILogger registro, DateTimeOffset momento);

    [LoggerMessage(EventId = 1101, EventName = "ConfiguracionActualizada", Level = LogLevel.Information,
        Message = "Se actualizó la configuración del negocio ({Momento:O})")]
    public static partial void ConfiguracionActualizada(this ILogger registro, DateTimeOffset momento);

    [LoggerMessage(EventId = 9101, EventName = "DireccionFueraDeLoopback", Level = LogLevel.Error,
        Message = "El servidor se detiene: {Motivo}")]
    public static partial void DireccionFueraDeLoopback(this ILogger registro, string motivo);
}
