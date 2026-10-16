using Optica.Application.Configuracion;
using Optica.Application.Tests.Dobles;
using Optica.Domain.Comun;
using Optica.Domain.Configuracion;

namespace Optica.Application.Tests;

public class ConfiguracionTests
{
    private readonly RepositorioConfiguracionEnMemoria _repositorio = new();

    private ActualizarConfiguracion Actualizacion() => new(_repositorio, new UnidadDeTrabajoInmediata(_repositorio));

    [Fact]
    public async Task ActualizarConfiguracion_Guarda4Valores()
    {
        var datos = new DatosConfiguracion(10.5m, CondicionFiscal.Monotributo, 100000m, 50m);

        var guardada = await Actualizacion().EjecutarAsync(datos);

        Assert.Equal(datos, guardada);
        Assert.Equal(1, _repositorio.CantidadGuardados);
        Assert.Equal(datos, await new ObtenerConfiguracion(_repositorio).EjecutarAsync());
    }

    [Fact]
    public async Task ActualizarConfiguracion_Invalida_NoGuardaNada()
    {
        var datos = new DatosConfiguracion(101m, CondicionFiscal.Monotributo, -1m, 0.001m);

        var errores = await Assert.ThrowsAsync<ErroresValidacion>(() => Actualizacion().EjecutarAsync(datos));

        var campos = errores.Errores.Select(e => e.Campo).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "alicuotaIva", "multiploRedondeo", "topeIdentificacion" }, campos);
        Assert.Equal(0, _repositorio.CantidadGuardados);
        Assert.Equal(
            new DatosConfiguracion(21m, CondicionFiscal.ResponsableInscripto, null, 0.01m),
            await new ObtenerConfiguracion(_repositorio).EjecutarAsync());
    }

    [Fact]
    public async Task UnidadDeTrabajo_FallaPersistencia_Propaga()
    {
        var falla = new InvalidOperationException("falla simulada de la base");
        _repositorio.FallaAlGuardar = falla;

        var lanzada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Actualizacion().EjecutarAsync(new DatosConfiguracion(10.5m, CondicionFiscal.Monotributo, null, 1m)));

        Assert.Same(falla, lanzada);
        // La unidad de trabajo revirtió lo escrito antes de la falla.
        Assert.Equal(21m, _repositorio.AlicuotaIva);
        Assert.Equal(CondicionFiscal.ResponsableInscripto, _repositorio.CondicionFiscal);
        Assert.Null(_repositorio.TopeIdentificacion);
        Assert.Equal(0.01m, _repositorio.MultiploRedondeo);
    }
}
