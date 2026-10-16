using Microsoft.AspNetCore.Http.Features;

namespace Optica.Api.Seguridad;

/// <summary>
/// Limita a 4 KB el cuerpo de los pedidos a <c>/api/acceso/*</c> y <c>/api/configuracion</c> (E4-13). Además de
/// acotar la memoria, acota el costo de PBKDF2 ante contraseñas enormes. Si el cuerpo trae un
/// <c>Content-Length</c> mayor se rechaza sin leerlo; si no lo trae, se corta al pasar el límite mientras se lee.
/// En los dos casos se lanza <see cref="BadHttpRequestException"/> con 413, que traduce el manejador de errores.
/// </summary>
public sealed class LimiteCuerpo(RequestDelegate siguiente)
{
    public const long MaximoBytes = 4 * 1024;

    private static readonly PathString[] RutasLimitadas = ["/api/acceso", "/api/configuracion"];

    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (!RutasLimitadas.Any(ruta => contexto.Request.Path.StartsWithSegments(ruta, StringComparison.OrdinalIgnoreCase)))
        {
            await siguiente(contexto);
            return;
        }

        // Con Kestrel, el servidor también hace cumplir el límite.
        if (contexto.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limiteServidor)
        {
            limiteServidor.MaxRequestBodySize = MaximoBytes;
        }

        if (contexto.Request.ContentLength > MaximoBytes)
        {
            throw Excedido();
        }

        contexto.Request.Body = new FlujoLimitado(contexto.Request.Body, MaximoBytes);
        await siguiente(contexto);
    }

    internal static BadHttpRequestException Excedido() =>
        new("El cuerpo del pedido supera el máximo permitido.", StatusCodes.Status413PayloadTooLarge);

    /// <summary>Flujo de solo lectura que falla en cuanto se leen más bytes que el máximo.</summary>
    private sealed class FlujoLimitado(Stream interno, long maximo) : Stream
    {
        private long _leidos;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Contar(interno.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Contar(await interno.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Contar(await interno.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Contar(int leidos)
        {
            _leidos += leidos;
            if (_leidos > maximo)
            {
                throw Excedido();
            }

            return leidos;
        }
    }
}
