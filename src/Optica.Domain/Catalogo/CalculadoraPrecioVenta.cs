using Optica.Domain.Configuracion;

namespace Optica.Domain.Catalogo;

/// <summary>
/// Calcula el Precio de Venta (final al consumidor, IVA incluido) a partir del costo, el margen
/// y la configuración del negocio (FR-03 a FR-06). Servicio puro: solo aritmética <c>decimal</c>.
/// </summary>
public static class CalculadoraPrecioVenta
{
    /// <summary>
    /// Decimales a los que se redondea el resultado exacto antes de aplicar el múltiplo. Con costo y
    /// margen de hasta 2 decimales, el valor matemático tiene como máximo 6, así que este redondeo
    /// elimina solo los restos de la división por la alícuota (NFR-02).
    /// </summary>
    public const int DecimalesResultadoExacto = 6;

    private const int DecimalesPrecio = 2;

    /// <summary>
    /// Devuelve el precio de venta llevado hacia arriba al múltiplo de redondeo comercial (FR-06).
    /// Para valores negativos el techo va hacia cero. No valida rangos: eso es responsabilidad de
    /// <see cref="Articulo"/>.
    /// </summary>
    public static decimal Calcular(decimal costo, decimal margen, ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var factorMargen = 1 + margen / 100;
        var bruto = configuracion.CondicionFiscal == CondicionFiscal.Monotributo
            ? costo * factorMargen
            : CalcularResponsableInscripto(costo, factorMargen, configuracion.AlicuotaIva);

        var exacto = Math.Round(bruto, DecimalesResultadoExacto, MidpointRounding.AwayFromZero);
        var multiplo = configuracion.MultiploRedondeo;
        var precio = Math.Ceiling(exacto / multiplo) * multiplo;

        return Math.Round(precio, DecimalesPrecio);
    }

    // FR-04: (a) costo sin IVA, (b) venta sin IVA y (c) venta con IVA, sin redondeos intermedios.
    private static decimal CalcularResponsableInscripto(decimal costo, decimal factorMargen, decimal alicuota)
    {
        var factorIva = 1 + alicuota / 100;
        var costoSinIva = costo / factorIva;
        var ventaSinIva = costoSinIva * factorMargen;
        return ventaSinIva * factorIva;
    }
}
