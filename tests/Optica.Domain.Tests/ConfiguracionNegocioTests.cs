using Optica.Domain.Comun;
using Optica.Domain.Configuracion;

namespace Optica.Domain.Tests;

public class ConfiguracionNegocioTests
{
    private static ErrorValidacion UnicoError(Action accion)
    {
        var errores = Assert.Throws<ErroresValidacion>(accion);
        return Assert.Single(errores.Errores);
    }

    [Fact]
    public void Configuracion_Inicial_TieneValoresPorDefecto()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        Assert.Equal(1, configuracion.Id);
        Assert.Equal(21m, configuracion.AlicuotaIva);
        Assert.Equal(CondicionFiscal.ResponsableInscripto, configuracion.CondicionFiscal);
        Assert.Null(configuracion.TopeIdentificacion);
        Assert.Equal(0.01m, configuracion.MultiploRedondeo);
    }

    [Fact]
    public void Configuracion_Multiplo0001_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(21m, CondicionFiscal.ResponsableInscripto, null, 0.001m));

        Assert.Equal("multiploRedondeo", error.Campo);
        Assert.Equal("El valor mínimo es 0,01", error.Mensaje);
        Assert.Equal(0.01m, configuracion.MultiploRedondeo);
    }

    [Fact]
    public void Configuracion_MultiploMayorAlMaximo_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(21m, CondicionFiscal.ResponsableInscripto, null, 1_000_000.01m));

        Assert.Equal("multiploRedondeo", error.Campo);
        Assert.Equal("El valor máximo es 1.000.000", error.Mensaje);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Configuracion_AlicuotaFueraDeRango_Rechaza(int alicuotaEntera)
    {
        decimal alicuota = alicuotaEntera;
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(alicuota, CondicionFiscal.ResponsableInscripto, null, 0.01m));

        Assert.Equal("alicuotaIva", error.Campo);
        Assert.Equal("La alícuota de IVA debe estar entre 0 y 100", error.Mensaje);
        Assert.Equal(21m, configuracion.AlicuotaIva);
    }

    [Fact]
    public void Configuracion_Alicuota105_Acepta()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        configuracion.Actualizar(10.5m, CondicionFiscal.Monotributo, 100000m, 50m);

        Assert.Equal(10.5m, configuracion.AlicuotaIva);
        Assert.Equal(CondicionFiscal.Monotributo, configuracion.CondicionFiscal);
        Assert.Equal(100000m, configuracion.TopeIdentificacion);
        Assert.Equal(50m, configuracion.MultiploRedondeo);
    }

    [Fact]
    public void Configuracion_CondicionInvalida_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(21m, (CondicionFiscal)99, null, 0.01m));

        Assert.Equal("condicionFiscal", error.Campo);
        Assert.Equal("Elegí una condición fiscal válida", error.Mensaje);
        Assert.Equal(CondicionFiscal.ResponsableInscripto, configuracion.CondicionFiscal);
    }

    [Fact]
    public void Configuracion_TopeNegativo_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(21m, CondicionFiscal.ResponsableInscripto, -1m, 0.01m));

        Assert.Equal("topeIdentificacion", error.Campo);
        Assert.Equal("El tope no puede ser negativo", error.Mensaje);
        Assert.Null(configuracion.TopeIdentificacion);
    }

    [Fact]
    public void Configuracion_TopeMayorAlMaximo_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var error = UnicoError(() => configuracion.Actualizar(21m, CondicionFiscal.ResponsableInscripto, 1_000_000_000.01m, 0.01m));

        Assert.Equal("topeIdentificacion", error.Campo);
        Assert.Equal("El tope no puede superar 1.000.000.000", error.Mensaje);
    }

    [Fact]
    public void Configuracion_TresDecimales_Rechaza()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var errores = Assert.Throws<ErroresValidacion>(
            () => configuracion.Actualizar(10.505m, CondicionFiscal.ResponsableInscripto, 100.125m, 1.005m));

        Assert.Collection(
            errores.Errores.OrderBy(e => e.Campo, StringComparer.Ordinal),
            e => { Assert.Equal("alicuotaIva", e.Campo); Assert.Equal("Ingresá como máximo 2 decimales", e.Mensaje); },
            e => { Assert.Equal("multiploRedondeo", e.Campo); Assert.Equal("Ingresá como máximo 2 decimales", e.Mensaje); },
            e => { Assert.Equal("topeIdentificacion", e.Campo); Assert.Equal("Ingresá como máximo 2 decimales", e.Mensaje); });
        Assert.Equal(21m, configuracion.AlicuotaIva);
    }

    [Fact]
    public void Configuracion_VariosErrores_InformaTodosLosCampos()
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        var errores = Assert.Throws<ErroresValidacion>(
            () => configuracion.Actualizar(101m, (CondicionFiscal)99, -1m, 0.001m));

        var campos = errores.Errores.Select(e => e.Campo).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "alicuotaIva", "condicionFiscal", "multiploRedondeo", "topeIdentificacion" }, campos);
        Assert.Equal(21m, configuracion.AlicuotaIva);
        Assert.Equal(CondicionFiscal.ResponsableInscripto, configuracion.CondicionFiscal);
        Assert.Null(configuracion.TopeIdentificacion);
        Assert.Equal(0.01m, configuracion.MultiploRedondeo);
    }

    public static TheoryData<decimal, decimal?, decimal> BordesAceptados => new()
    {
        { 0m, null, 0.01m },
        { 100m, 0m, 1_000_000m },
        { 0m, 1_000_000_000m, 0.01m },
    };

    [Theory]
    [MemberData(nameof(BordesAceptados))]
    public void Configuracion_ValoresEnLosBordes_Acepta(decimal alicuota, decimal? tope, decimal multiplo)
    {
        var configuracion = ConfiguracionNegocio.Inicial();

        configuracion.Actualizar(alicuota, CondicionFiscal.Monotributo, tope, multiplo);

        Assert.Equal(alicuota, configuracion.AlicuotaIva);
        Assert.Equal(tope, configuracion.TopeIdentificacion);
        Assert.Equal(multiplo, configuracion.MultiploRedondeo);
        Assert.Equal(CondicionFiscal.Monotributo, configuracion.CondicionFiscal);
    }
}
