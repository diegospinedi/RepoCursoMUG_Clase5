using Optica.Domain.Configuracion;

namespace Optica.Domain.Catalogo;

/// <summary>
/// Recalcula el precio de venta de un conjunto de artículos, activos e inactivos, cuando cambia
/// la configuración que interviene en el cálculo (FR-10).
/// </summary>
public static class RecalculoPrecios
{
    /// <summary>Recalcula cada artículo y devuelve la cantidad recalculada.</summary>
    public static int Aplicar(IEnumerable<Articulo> articulos, ConfiguracionNegocio configuracion)
    {
        ArgumentNullException.ThrowIfNull(articulos);
        ArgumentNullException.ThrowIfNull(configuracion);

        var cantidad = 0;
        foreach (var articulo in articulos)
        {
            articulo.RecalcularPrecio(configuracion);
            cantidad++;
        }

        return cantidad;
    }
}
