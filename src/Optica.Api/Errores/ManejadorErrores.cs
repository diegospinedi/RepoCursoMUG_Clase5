using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Optica.Application.Comun;
using Optica.Domain.Comun;
using Optica.Infrastructure.Persistencia;

namespace Optica.Api.Errores;

/// <summary>
/// Traduce las excepciones de los casos de uso (y las de infraestructura que llegan hasta acá) a
/// <c>ProblemDetails</c>. La respuesta lleva solo un título fijo, el estado y, cuando corresponde, los campos con
/// error, los intentos restantes o la hora de liberación. Nunca incluye el mensaje de una excepción no prevista,
/// ni su stack trace, en ningún entorno: así no puede filtrarse una contraseña, un hash o un sello.
/// </summary>
/// <remarks>
/// La base ocupada (SQLITE_BUSY, E2-4) la reconoce Infrastructure con <see cref="ErroresPersistencia.EsBaseOcupada"/>:
/// la API no conoce ningún tipo de SQLite ni de EF Core.
/// </remarks>
public sealed partial class ManejadorErrores(ILogger<ManejadorErrores> registro, IOptions<JsonOptions> opcionesJson) : IExceptionHandler
{
    public const string TituloBaseOcupada = "La base está ocupada, intentá de nuevo";
    public const string TituloErrorInesperado = "Ocurrió un error inesperado";
    public const string TituloCuerpoGrande = "El pedido supera el tamaño máximo de 4 KB";
    public const string TituloPedidoInvalido = "El pedido no es válido";
    public const int EstadoBloqueado = StatusCodes.Status423Locked;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var problema = Traducir(exception);
        var estado = problema.Status ?? StatusCodes.Status500InternalServerError;
        if (estado == StatusCodes.Status500InternalServerError)
        {
            ErrorInesperado(registro, httpContext.Request.Method, httpContext.Request.Path, exception);
        }
        else
        {
            PedidoRechazado(registro, estado, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = estado;
        await httpContext.Response.WriteAsJsonAsync(
            problema,
            problema.GetType(),
            opcionesJson.Value.JsonSerializerOptions,
            Problemas.TipoContenido,
            cancellationToken);
        return true;
    }

    public static ProblemDetails Traducir(Exception excepcion) => excepcion switch
    {
        ErroresValidacion errores => Problemas.Validacion(errores.Errores.Select(e => (e.Campo, e.Mensaje))),
        ErrorValidacion error => Problemas.Validacion([(error.Campo, error.Mensaje)]),
        ContrasenaActualIncorrecta error => Problemas.Validacion([(ContrasenaActualIncorrecta.Campo, error.Mensaje)]),
        ContrasenaIncorrecta error => Problemas.Crear(
            StatusCodes.Status401Unauthorized,
            error.Message,
            [new("intentosRestantes", error.IntentosRestantes)]),
        IngresoBloqueado error => Problemas.Crear(
            EstadoBloqueado,
            error.Message,
            [new("bloqueadoHasta", Problemas.FechaUtc(error.Hasta))]),
        ContrasenaYaDefinida or ContrasenaNoDefinida => Problemas.Crear(StatusCodes.Status409Conflict, excepcion.Message),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
            Problemas.Crear(StatusCodes.Status413PayloadTooLarge, TituloCuerpoGrande),
        BadHttpRequestException error => Problemas.Crear(error.StatusCode, TituloPedidoInvalido),
        _ when ErroresPersistencia.EsBaseOcupada(excepcion) => Problemas.Crear(StatusCodes.Status503ServiceUnavailable, TituloBaseOcupada),
        _ => Problemas.Crear(StatusCodes.Status500InternalServerError, TituloErrorInesperado),
    };

    [LoggerMessage(EventId = 9001, EventName = "ErrorInesperado", Level = LogLevel.Error, Message = "Error no previsto al atender {Metodo} {Ruta}")]
    private static partial void ErrorInesperado(ILogger registro, string metodo, PathString ruta, Exception excepcion);

    [LoggerMessage(EventId = 9002, EventName = "PedidoRechazado", Level = LogLevel.Information, Message = "Pedido rechazado con {Estado} ({Tipo})")]
    private static partial void PedidoRechazado(ILogger registro, int estado, string tipo);
}
