using Microsoft.EntityFrameworkCore;
using Optica.Domain.Configuracion;

namespace Optica.Infrastructure.Persistencia;

/// <summary>Acceso a la fila única de la configuración del negocio.</summary>
public sealed class RepositorioConfiguracion(OpticaDbContext contexto) : IRepositorioConfiguracion
{
    public async Task<ConfiguracionNegocio> Obtener() =>
        await contexto.Set<ConfiguracionNegocio>().AsNoTracking().SingleAsync(c => c.Id == ConfiguracionNegocio.IdUnico);

    public async Task Guardar(ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var entrada = contexto.Update(configuracion);
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
