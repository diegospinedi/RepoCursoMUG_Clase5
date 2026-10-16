using System.Globalization;
using Optica.Infrastructure.Persistencia;

namespace Optica.Infrastructure.Tests;

public class ConversorDecimalEscaladoTests
{
    private readonly ConversorDecimalEscalado _conversor = new();

    [Fact]
    public void Conversor_IdaYVuelta_SinPerdida()
    {
        (decimal Valor, long Escalado)[] casos = [(10.5m, 105_000L), (0.01m, 100L), (100000m, 1_000_000_000L)];

        var culturaOriginal = CultureInfo.CurrentCulture;
        try
        {
            foreach (var cultura in new[] { "es-AR", "en-US" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultura);
                foreach (var (valor, escalado) in casos)
                {
                    var guardado = Assert.IsType<long>(_conversor.ConvertToProvider(valor));
                    Assert.Equal(escalado, guardado);
                    Assert.Equal(valor, Assert.IsType<decimal>(_conversor.ConvertFromProvider(guardado)));
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaOriginal;
        }
    }

    [Fact]
    public void Conversor_MasDe4Decimales_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() => _conversor.ConvertToProvider(0.00001m));
    }

    [Fact]
    public void Conversor_FueraDeRangoLong_Lanza()
    {
        // 10^15 × 10.000 supera long.MaxValue; decimal.MaxValue × 10.000 desborda el propio decimal.
        Assert.Throws<InvalidOperationException>(() => _conversor.ConvertToProvider(1_000_000_000_000_000m));
        Assert.Throws<InvalidOperationException>(() => _conversor.ConvertToProvider(-1_000_000_000_000_000m));
        Assert.Throws<InvalidOperationException>(() => _conversor.ConvertToProvider(decimal.MaxValue));
    }
}
