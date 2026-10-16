using Optica.Application.Comun;
using Optica.Domain.Acceso;

namespace Optica.Application.Acceso;

/// <summary>
/// Deja el sistema sin contraseña e invalida todas las sesiones (FR-06). Es idempotente.
/// Devuelve si había una contraseña definida.
/// </summary>
public sealed class BlanquearContrasena(IRepositorioCredencial repositorio, IUnidadDeTrabajo unidadDeTrabajo)
{
    public async Task<bool> EjecutarAsync()
    {
        var habiaContrasena = false;
        await unidadDeTrabajo.EjecutarAsync(async () =>
        {
            var credencial = await repositorio.Obtener();
            habiaContrasena = credencial.ContrasenaDefinida;
            credencial.Blanquear();
            await repositorio.Guardar(credencial);
        });

        return habiaContrasena;
    }
}
