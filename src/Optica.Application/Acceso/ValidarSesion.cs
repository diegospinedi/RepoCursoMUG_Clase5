using Optica.Domain.Acceso;

namespace Optica.Application.Acceso;

/// <summary>
/// Una sesión es válida solo si hay contraseña definida y su sello coincide con el vigente: cambiar o
/// blanquear la contraseña invalida las sesiones anteriores (F-01 del threat model).
/// </summary>
public sealed class ValidarSesion(IRepositorioCredencial repositorio)
{
    public async Task<bool> EjecutarAsync(Guid sello)
    {
        var credencial = await repositorio.Obtener();
        return credencial.ContrasenaDefinida && credencial.SelloSeguridad == sello;
    }
}
