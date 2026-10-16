using Optica.Domain.Configuracion;

namespace Optica.Application.Configuracion;

/// <summary>Los cuatro valores de la configuración del negocio, tal como se leen y se actualizan (FR-07).</summary>
public sealed record DatosConfiguracion(
    decimal AlicuotaIva,
    CondicionFiscal CondicionFiscal,
    decimal? TopeIdentificacion,
    decimal MultiploRedondeo)
{
    public static DatosConfiguracion Desde(ConfiguracionNegocio configuracion) => new(
        configuracion.AlicuotaIva,
        configuracion.CondicionFiscal,
        configuracion.TopeIdentificacion,
        configuracion.MultiploRedondeo);
}

/// <summary>Devuelve la configuración vigente del negocio (FR-07).</summary>
public sealed class ObtenerConfiguracion(IRepositorioConfiguracion repositorio)
{
    public async Task<DatosConfiguracion> EjecutarAsync() =>
        DatosConfiguracion.Desde(await repositorio.Obtener());
}
