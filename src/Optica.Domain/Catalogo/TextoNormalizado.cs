using System.Globalization;
using System.Text;

namespace Optica.Domain.Catalogo;

/// <summary>Normalizaciones de texto del catálogo (FR-13 y FR-22).</summary>
public static class TextoNormalizado
{
    /// <summary>
    /// Clave de coincidencia del Código en el proveedor: en <see cref="NormalizationForm.FormC"/>,
    /// sin espacios al borde y en minúsculas invariantes. Conserva los acentos: "AÑO-1" y "ANO-1"
    /// son claves distintas, pero una "Ñ" descompuesta coincide con la compuesta (FR-13).
    /// </summary>
    public static string ClaveCodigoProveedor(string texto) =>
        CodigoProveedor(texto).ToLowerInvariant();

    /// <summary>Código en el proveedor tal como se guarda: en FormC y sin espacios al borde.</summary>
    public static string CodigoProveedor(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return texto.Normalize(NormalizationForm.FormC).Trim();
    }

    /// <summary>Texto de búsqueda: sin espacios al borde, en minúsculas y sin diacríticos (FR-22).</summary>
    public static string ParaBusqueda(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
