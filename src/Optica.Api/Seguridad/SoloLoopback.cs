namespace Optica.Api.Seguridad;

/// <summary>
/// Primer middleware del pipeline: rechaza con 403, sin procesar nada, todo pedido que haya llegado por una
/// interfaz que no sea loopback (<c>Connection.LocalIpAddress</c>). Cierra la ventana entre que Kestrel abre el
/// puerto y que <see cref="GuardaLoopback"/> detiene el servidor mal configurado (AR-01).
/// </summary>
/// <remarks>
/// <c>LocalIpAddress</c> nulo se acepta como loopback. Solo pasa con TestServer (pruebas en memoria, sin
/// interfaz de red): Kestrel siempre informa la dirección local de la conexión TCP.
/// </remarks>
public sealed class SoloLoopback(RequestDelegate siguiente)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Connection.LocalIpAddress is { } local && !GuardaLoopback.EsIpLoopback(local))
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return siguiente(contexto);
    }
}
