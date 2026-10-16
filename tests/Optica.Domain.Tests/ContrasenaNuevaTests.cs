using Optica.Domain.Acceso;
using Optica.Domain.Comun;

namespace Optica.Domain.Tests;

public class ContrasenaNuevaTests
{
    [Fact]
    public void ContrasenaNueva_7Caracteres_Rechaza()
    {
        var error = Assert.Throws<ErrorValidacion>(() => ContrasenaNueva.Crear("1234567"));

        Assert.Equal("contrasenaNueva", error.Campo);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres", error.Mensaje);
    }

    [Fact]
    public void ContrasenaNueva_8Caracteres_Acepta()
    {
        var contrasena = ContrasenaNueva.Crear("1234 678");

        Assert.Equal("1234 678", contrasena.Valor);
    }

    [Fact]
    public void ContrasenaNueva_129Caracteres_Rechaza()
    {
        var error = Assert.Throws<ErrorValidacion>(() => ContrasenaNueva.Crear(new string('a', 129)));

        Assert.Equal("contrasenaNueva", error.Campo);
        Assert.Equal("La contraseña puede tener como máximo 128 caracteres", error.Mensaje);
    }

    [Fact]
    public void ContrasenaNueva_128Caracteres_Acepta()
    {
        var valor = new string('a', 128);

        Assert.Equal(valor, ContrasenaNueva.Crear(valor).Valor);
    }
}
