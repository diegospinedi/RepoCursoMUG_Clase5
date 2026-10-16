using Optica.Domain.Acceso;

namespace Optica.Domain.Tests;

public class CredencialAccesoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static CredencialAcceso CredencialConHash() => new("hash-original", Guid.NewGuid());

    [Fact]
    public void Credencial_CuatroFallos_NoBloquea()
    {
        var credencial = CredencialConHash();

        for (var i = 0; i < 4; i++)
        {
            credencial.RegistrarFallo(Ahora);
        }

        Assert.Equal(4, credencial.IntentosFallidos);
        Assert.Null(credencial.BloqueadoHasta);
        Assert.False(credencial.EstaBloqueada(Ahora));
    }

    [Fact]
    public void Credencial_QuintoFallo_Bloquea5Minutos()
    {
        var credencial = CredencialConHash();

        for (var i = 0; i < 5; i++)
        {
            credencial.RegistrarFallo(Ahora);
        }

        Assert.Equal(Ahora.AddMinutes(5), credencial.BloqueadoHasta);
        Assert.Equal(0, credencial.IntentosFallidos);
        Assert.True(credencial.EstaBloqueada(Ahora));
    }

    [Fact]
    public void Credencial_Bloqueada_RechazaSinVerificar()
    {
        var credencial = CredencialConHash();
        for (var i = 0; i < 5; i++)
        {
            credencial.RegistrarFallo(Ahora);
        }

        Assert.True(credencial.EstaBloqueada(Ahora.AddMinutes(4).AddSeconds(59)));
        Assert.False(credencial.EstaBloqueada(Ahora.AddMinutes(5)));
    }

    [Fact]
    public void Credencial_IngresoExitoso_ReiniciaContador()
    {
        var credencial = new CredencialAcceso("hash-original", Guid.NewGuid(), 3, Ahora.AddMinutes(-1));
        Assert.Equal(3, credencial.IntentosFallidos);
        Assert.NotNull(credencial.BloqueadoHasta);

        credencial.RegistrarIngresoExitoso();

        Assert.Equal(0, credencial.IntentosFallidos);
        Assert.Null(credencial.BloqueadoHasta);
    }

    [Fact]
    public void Credencial_CambiarHashYBlanquear_RegeneranSello()
    {
        var selloInicial = Guid.NewGuid();
        var credencial = new CredencialAcceso("hash-original", selloInicial);
        Assert.True(credencial.ContrasenaDefinida);

        credencial.CambiarHash("hash-nuevo");
        var selloTrasCambio = credencial.SelloSeguridad;

        Assert.Equal("hash-nuevo", credencial.Hash);
        Assert.NotEqual(selloInicial, selloTrasCambio);
        Assert.NotEqual(Guid.Empty, selloTrasCambio);

        credencial.RegistrarFallo(Ahora);
        credencial.Blanquear();

        Assert.Null(credencial.Hash);
        Assert.False(credencial.ContrasenaDefinida);
        Assert.Equal(0, credencial.IntentosFallidos);
        Assert.Null(credencial.BloqueadoHasta);
        Assert.NotEqual(selloTrasCambio, credencial.SelloSeguridad);
        Assert.NotEqual(selloInicial, credencial.SelloSeguridad);
        Assert.NotEqual(Guid.Empty, credencial.SelloSeguridad);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Credencial_ConstructorHashEnBlanco_Lanza(string hash)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CredencialAcceso(hash, Guid.NewGuid()));
    }

    [Fact]
    public void Credencial_ConstructorSelloVacio_Lanza()
    {
        var excepcion = Assert.Throws<ArgumentException>(() => new CredencialAcceso(null, Guid.Empty));

        Assert.Equal("selloSeguridad", excepcion.ParamName);
    }
}
