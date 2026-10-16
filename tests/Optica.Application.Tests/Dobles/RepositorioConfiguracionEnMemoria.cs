using Optica.Domain.Configuracion;

namespace Optica.Application.Tests.Dobles;

/// <summary>
/// Guarda los cuatro valores, no la instancia. <see cref="FallaAlGuardar"/> simula una falla de persistencia
/// que ocurre después de haber escrito (para probar que la unidad de trabajo revierte).
/// </summary>
public sealed class RepositorioConfiguracionEnMemoria : IRepositorioConfiguracion, IParticipanteTransaccion
{
    private Datos _datos = Copiar(ConfiguracionNegocio.Inicial());

    public Exception? FallaAlGuardar { get; set; }

    public int CantidadGuardados { get; private set; }

    public decimal AlicuotaIva => _datos.AlicuotaIva;

    public CondicionFiscal CondicionFiscal => _datos.CondicionFiscal;

    public decimal? TopeIdentificacion => _datos.TopeIdentificacion;

    public decimal MultiploRedondeo => _datos.MultiploRedondeo;

    public Task<ConfiguracionNegocio> Obtener()
    {
        var configuracion = ConfiguracionNegocio.Inicial();
        configuracion.Actualizar(_datos.AlicuotaIva, _datos.CondicionFiscal, _datos.TopeIdentificacion, _datos.MultiploRedondeo);
        return Task.FromResult(configuracion);
    }

    public Task Guardar(ConfiguracionNegocio configuracion)
    {
        _datos = Copiar(configuracion);
        CantidadGuardados++;
        if (FallaAlGuardar is not null)
        {
            throw FallaAlGuardar;
        }

        return Task.CompletedTask;
    }

    public object TomarInstantanea() => _datos;

    public void Restaurar(object instantanea) => _datos = (Datos)instantanea;

    private static Datos Copiar(ConfiguracionNegocio c) =>
        new(c.AlicuotaIva, c.CondicionFiscal, c.TopeIdentificacion, c.MultiploRedondeo);

    private sealed record Datos(
        decimal AlicuotaIva,
        CondicionFiscal CondicionFiscal,
        decimal? TopeIdentificacion,
        decimal MultiploRedondeo);
}
