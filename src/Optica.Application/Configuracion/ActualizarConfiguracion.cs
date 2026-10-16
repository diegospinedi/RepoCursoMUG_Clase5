using Optica.Application.Comun;
using Optica.Domain.Configuracion;

namespace Optica.Application.Configuracion;

/// <summary>
/// Actualiza los cuatro valores de la configuración (FR-07 a FR-14). Si alguno es inválido, el dominio
/// lanza <see cref="Optica.Domain.Comun.ErroresValidacion"/> antes de guardar y no se modifica nada.
/// </summary>
public sealed class ActualizarConfiguracion(IRepositorioConfiguracion repositorio, IUnidadDeTrabajo unidadDeTrabajo)
{
    public async Task<DatosConfiguracion> EjecutarAsync(DatosConfiguracion datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        DatosConfiguracion? guardada = null;
        await unidadDeTrabajo.EjecutarAsync(async () =>
        {
            var configuracion = await repositorio.Obtener();
            configuracion.Actualizar(datos.AlicuotaIva, datos.CondicionFiscal, datos.TopeIdentificacion, datos.MultiploRedondeo);
            await repositorio.Guardar(configuracion);
            guardada = DatosConfiguracion.Desde(configuracion);
        });

        return guardada!;
    }
}
