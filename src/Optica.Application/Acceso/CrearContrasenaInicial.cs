using Optica.Application.Comun;
using Optica.Domain.Acceso;

namespace Optica.Application.Acceso;

/// <summary>
/// Crea la contraseña de acceso cuando todavía no existe (FR-02, FR-03). La escritura es condicional
/// en el repositorio, así que dos creaciones simultáneas no pueden ganar ambas.
/// </summary>
public sealed class CrearContrasenaInicial(IRepositorioCredencial repositorio, IHasherContrasena hasher)
{
    public async Task EjecutarAsync(string? contrasena)
    {
        var nueva = ContrasenaNueva.Crear(contrasena);

        if (!await repositorio.CrearSiNoExiste(hasher.Hashear(nueva), Guid.NewGuid()))
        {
            throw new ContrasenaYaDefinida();
        }
    }
}
