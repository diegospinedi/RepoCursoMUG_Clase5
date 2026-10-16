using Optica.Domain.Catalogo;
using Optica.Domain.Configuracion;

namespace Optica.Domain.Tests;

public class CalculadoraPrecioVentaTests
{
    private static ConfiguracionNegocio Configuracion(
        decimal alicuota = 21m,
        CondicionFiscal condicion = CondicionFiscal.ResponsableInscripto,
        decimal multiplo = 0.01m)
    {
        var configuracion = ConfiguracionNegocio.Inicial();
        configuracion.Actualizar(alicuota, condicion, null, multiplo);
        return configuracion;
    }

    [Fact]
    public void Calculadora_RI_Costo1000Margen50_Da1500()
    {
        var configuracion = Configuracion();

        var precio = CalculadoraPrecioVenta.Calcular(1000m, 50m, configuracion);

        Assert.Equal(1500.00m, precio);
    }

    [Fact]
    public void Calculadora_RI_Costo2000Margen50_Da3000_SinSubirPorRestos()
    {
        var configuracion = Configuracion();

        var precio = CalculadoraPrecioVenta.Calcular(2000m, 50m, configuracion);

        // En decimal, 2000 / 1,21 × 1,5 × 1,21 queda apenas por encima de 3.000: el techo directo
        // daría 3.000,01. El redondeo previo a 6 decimales lo evita.
        Assert.Equal(3000.01m, Math.Ceiling(2000m / 1.21m * 1.5m * 1.21m / 0.01m) * 0.01m);
        Assert.Equal(3000.00m, precio);
    }

    [Fact]
    public void Calculadora_RI_Costo1210Margen50_Da1815_ConIntermediosExactos()
    {
        var configuracion = Configuracion();

        var precio = CalculadoraPrecioVenta.Calcular(1210m, 50m, configuracion);

        // AC-03 menciona intermedios (costo sin IVA 1.000,00 y venta sin IVA 1.500,00), pero
        // Calcular solo expone el resultado final: los intermedios no son observables desde el
        // código de producción y este test verifica únicamente el precio.
        Assert.Equal(1815.00m, precio);
    }

    [Fact]
    public void Calculadora_Monotributo_Costo1210Margen50_Da1815()
    {
        var configuracion = Configuracion(condicion: CondicionFiscal.Monotributo);

        var precio = CalculadoraPrecioVenta.Calcular(1210m, 50m, configuracion);

        Assert.Equal(1815.00m, precio);
    }

    [Fact]
    public void Calculadora_Multiplo50_181537_Da1850()
    {
        // Monotributo con margen 0: el precio calculado es el costo, 1.815,37.
        var configuracion = Configuracion(condicion: CondicionFiscal.Monotributo, multiplo: 50m);

        var precio = CalculadoraPrecioVenta.Calcular(1815.37m, 0m, configuracion);

        Assert.Equal(1850.00m, precio);
    }

    [Fact]
    public void Calculadora_Multiplo001_1666656_Da166666()
    {
        // 1.388,88 × 1,20 = 1.666,656 exacto.
        var configuracion = Configuracion(condicion: CondicionFiscal.Monotributo);

        var precio = CalculadoraPrecioVenta.Calcular(1388.88m, 20m, configuracion);

        Assert.Equal(1666.66m, precio);
    }

    [Theory]
    [InlineData(CondicionFiscal.ResponsableInscripto)]
    [InlineData(CondicionFiscal.Monotributo)]
    public void Calculadora_Exacto166666_NoSube(CondicionFiscal condicion)
    {
        // Precio calculado exactamente 1.666,66 (833,33 × 2). En RI, dividir y volver a
        // multiplicar por 1,21 deja restos en decimal que no deben subirlo a 1.666,67.
        var configuracion = Configuracion(condicion: condicion);

        var precio = CalculadoraPrecioVenta.Calcular(833.33m, 100m, configuracion);

        Assert.Equal(1666.66m, precio);
    }

    [Fact]
    public void Calculadora_RI_Alicuota105_Costo1105Margen50_Da165750()
    {
        var configuracion = Configuracion(alicuota: 10.5m);

        var precio = CalculadoraPrecioVenta.Calcular(1105m, 50m, configuracion);

        Assert.Equal(1657.50m, precio);
    }

    [Fact]
    public void Calculadora_CostoNegativoYCero_NoLanza()
    {
        var configuracion = Configuracion();

        var negativo = CalculadoraPrecioVenta.Calcular(-100m, 50m, configuracion);
        var cero = CalculadoraPrecioVenta.Calcular(0m, 50m, configuracion);

        Assert.Equal(-150.00m, negativo);
        Assert.Equal(0m, cero);
        Assert.True(negativo <= 0m && cero <= 0m);
    }

    [Fact]
    public void Calculadora_NegativoNoMultiplo_TechoHaciaCero()
    {
        // Monotributo, margen 0, múltiplo 50: -1.815,37 sube hacia cero hasta -1.800.
        var configuracion = Configuracion(condicion: CondicionFiscal.Monotributo, multiplo: 50m);

        var precio = CalculadoraPrecioVenta.Calcular(-1815.37m, 0m, configuracion);

        Assert.Equal(-1800.00m, precio);
    }

    [Theory]
    [InlineData(CondicionFiscal.ResponsableInscripto, 1)]
    [InlineData(CondicionFiscal.ResponsableInscripto, -1)]
    [InlineData(CondicionFiscal.Monotributo, 1)]
    [InlineData(CondicionFiscal.Monotributo, -1)]
    public void Calculadora_PeorCaso_CostoMaximoMargenMaximoAlicuota100Multiplo1000000_NoDesborda(
        CondicionFiscal condicion,
        int signo)
    {
        // ±100.000.000 × (1 + 1.000/100) = ±1.100.000.000, múltiplo exacto de 1.000.000.
        // En RI: 100.000.000 / 2 × 11 × 2, también exacto.
        var configuracion = Configuracion(alicuota: 100m, condicion: condicion, multiplo: 1_000_000m);

        var precio = CalculadoraPrecioVenta.Calcular(signo * 100_000_000m, 1_000m, configuracion);

        Assert.Equal(signo * 1_100_000_000m, precio);
    }

    [Fact]
    public void Calculadora_ConfiguracionNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => CalculadoraPrecioVenta.Calcular(1000m, 50m, null!));
    }
}
