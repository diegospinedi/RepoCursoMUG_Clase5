namespace Optica.Api.Seguridad;

/// <summary>
/// Agrega a toda respuesta los headers contra clickjacking, sniffing y XSS (R-11 del threat model). Se fijan
/// justo antes de enviar los headers, así sobreviven a un <c>Response.Clear()</c> del manejador de errores.
/// </summary>
public sealed class EncabezadosSeguridad(RequestDelegate siguiente)
{
    public const string PoliticaContenido = "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        contexto.Response.OnStarting(() =>
        {
            var encabezados = contexto.Response.Headers;
            encabezados.XFrameOptions = "DENY";
            encabezados.XContentTypeOptions = "nosniff";
            encabezados["Referrer-Policy"] = "no-referrer";
            encabezados.ContentSecurityPolicy = PoliticaContenido;
            return Task.CompletedTask;
        });

        return siguiente(contexto);
    }
}
