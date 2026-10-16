using Optica.Domain.Catalogo;

namespace Optica.Domain.Tests;

public class TextoNormalizadoTests
{
    [Fact]
    public void TextoNormalizado_Clave_ConservaAcentos()
    {
        var conEnie = TextoNormalizado.ClaveCodigoProveedor("  AÑO-1 ");
        var sinEnie = TextoNormalizado.ClaveCodigoProveedor("ANO-1");

        Assert.Equal("año-1", conEnie);
        Assert.Equal("ano-1", sinEnie);
        Assert.NotEqual(conEnie, sinEnie);
    }

    [Fact]
    public void TextoNormalizado_Clave_IgnoraMayusculasYEspaciosAlBorde()
    {
        Assert.Equal(
            TextoNormalizado.ClaveCodigoProveedor("abc-12"),
            TextoNormalizado.ClaveCodigoProveedor("\t ABC-12  "));
    }

    [Fact]
    public void TextoNormalizado_Busqueda_QuitaAcentosYMayusculas()
    {
        Assert.Equal("lente organico ano", TextoNormalizado.ParaBusqueda("  LENTE Orgánico AÑO "));
        Assert.Equal("optica", TextoNormalizado.ParaBusqueda("ÓPTICA"));
    }

    [Fact]
    public void TextoNormalizado_Clave_FormDYFormCCoinciden()
    {
        // "AÑO-1" con la Ñ descompuesta (N + tilde combinante), como llega desde macOS.
        var descompuesto = "AN\u0303O-1";
        var compuesto = "A\u00D1O-1";

        var claveDescompuesta = TextoNormalizado.ClaveCodigoProveedor(descompuesto);

        Assert.Equal(TextoNormalizado.ClaveCodigoProveedor(compuesto), claveDescompuesta);
        Assert.Equal("a\u00F1o-1", claveDescompuesta);
        Assert.NotEqual(TextoNormalizado.ClaveCodigoProveedor("ANO-1"), claveDescompuesta);
    }

    [Fact]
    public void TextoNormalizado_Busqueda_EntradaFormD()
    {
        Assert.Equal("lente organico ano", TextoNormalizado.ParaBusqueda(" LENTE Orga\u0301nico AN\u0303O "));
    }
}
