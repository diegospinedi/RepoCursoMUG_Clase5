using Optica.Domain.Acceso;

namespace Optica.Application.Acceso;

/// <summary>Estado público del acceso: si hay contraseña y, si corresponde, hasta cuándo dura el bloqueo.</summary>
public sealed record EstadoAcceso(bool ContrasenaDefinida, DateTimeOffset? BloqueadoHasta);

/// <summary>Informa si la contraseña ya fue creada y si el ingreso sigue bloqueado (FR-02, NFR-02).</summary>
public sealed class ObtenerEstadoAcceso(IRepositorioCredencial repositorio, TimeProvider reloj)
{
    public async Task<EstadoAcceso> EjecutarAsync()
    {
        var credencial = await repositorio.Obtener();
        var bloqueadoHasta = credencial.EstaBloqueada(reloj.GetUtcNow()) ? credencial.BloqueadoHasta : null;
        return new EstadoAcceso(credencial.ContrasenaDefinida, bloqueadoHasta);
    }
}
