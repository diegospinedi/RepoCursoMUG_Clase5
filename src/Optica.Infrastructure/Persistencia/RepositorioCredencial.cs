using Microsoft.EntityFrameworkCore;
using Optica.Domain.Acceso;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Acceso a la fila única de la credencial. Las lecturas no quedan en el seguimiento de EF: cada
/// <see cref="Obtener"/> refleja lo que está en la base (incluido lo escrito por <see cref="CrearSiNoExiste"/>).
/// </summary>
public sealed class RepositorioCredencial(OpticaDbContext contexto) : IRepositorioCredencial
{
    public async Task<CredencialAcceso> Obtener() =>
        await contexto.Set<CredencialAcceso>().AsNoTracking().SingleAsync(c => c.Id == CredencialAcceso.IdUnico);

    /// <summary>
    /// <c>UPDATE Credencial SET Hash = @hash, SelloSeguridad = @sello WHERE Id = 1 AND Hash IS NULL</c>,
    /// parametrizado. Es atómico en la base: de dos creaciones simultáneas solo una afecta la fila (FR-03).
    /// </summary>
    public async Task<bool> CrearSiNoExiste(string hash, Guid sello)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var filasAfectadas = await contexto.Set<CredencialAcceso>()
            .Where(c => c.Id == CredencialAcceso.IdUnico && c.Hash == null)
            .ExecuteUpdateAsync(fila => fila
                .SetProperty(c => c.Hash, hash)
                .SetProperty(c => c.SelloSeguridad, sello));

        return filasAfectadas == 1;
    }

    /// <summary>
    /// Reescribe la fila completa (hash, sello, intentos y bloqueo) con los valores de la entidad, sin
    /// comparar contra lo que haya en la base. Solo es seguro dentro de <see cref="Optica.Application.Comun.IUnidadDeTrabajo"/>:
    /// su <c>BEGIN IMMEDIATE</c> garantiza que nadie escribió la credencial entre el <see cref="Obtener"/> y este
    /// guardado. Fuera de una unidad, un cambio de otra conexión en ese intervalo se perdería.
    /// </summary>
    public async Task Guardar(CredencialAcceso credencial)
    {
        ArgumentNullException.ThrowIfNull(credencial);

        var entrada = contexto.Update(credencial);
        try
        {
            await contexto.SaveChangesAsync();
        }
        finally
        {
            entrada.State = EntityState.Detached;
        }
    }
}
