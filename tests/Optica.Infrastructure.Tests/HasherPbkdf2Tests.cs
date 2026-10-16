using System.Text.RegularExpressions;
using Optica.Domain.Acceso;
using Optica.Domain.Comun;
using Optica.Infrastructure.Seguridad;

namespace Optica.Infrastructure.Tests;

public class HasherPbkdf2Tests
{
    private const string Contrasena = "clave-de-prueba-123";

    // PBKDF2-SHA256$600000$<sal de 16 bytes en base64>$<hash de 32 bytes en base64>
    private static readonly Regex FormatoEsperado =
        new(@"^PBKDF2-SHA256\$600000\$[A-Za-z0-9+/]{22}==\$[A-Za-z0-9+/]{43}=$");

    private readonly HasherPbkdf2 _hasher = new();

    [Fact]
    public void HasherPbkdf2_FormatoYVerificacion()
    {
        var hash = _hasher.Hashear(ContrasenaNueva.Crear(Contrasena));

        Assert.Matches(FormatoEsperado, hash);
        Assert.DoesNotContain(Contrasena, hash, StringComparison.Ordinal);
        Assert.True(_hasher.Verificar(Contrasena, hash));
        Assert.False(_hasher.Verificar("clave-de-prueba-124", hash));
        Assert.False(_hasher.Verificar(string.Empty, hash));
    }

    [Fact]
    public void HasherPbkdf2_MismaContrasena_DistintaSal()
    {
        var primero = _hasher.Hashear(ContrasenaNueva.Crear(Contrasena));
        var segundo = _hasher.Hashear(ContrasenaNueva.Crear(Contrasena));

        Assert.NotEqual(primero, segundo);
        Assert.NotEqual(primero.Split('$')[2], segundo.Split('$')[2]);
        Assert.True(_hasher.Verificar(Contrasena, primero));
        Assert.True(_hasher.Verificar(Contrasena, segundo));
    }

    [Fact]
    public void HasherPbkdf2_FormatoDesconocido_DevuelveFalse()
    {
        // Partes con forma válida, armadas a mano para no depender de la salida del hasher.
        string[] partes = ["PBKDF2-SHA256", "600000", Convert.ToBase64String(new byte[16]), Convert.ToBase64String(new byte[32])];
        var valido = string.Join('$', partes);

        string[] desconocidos =
        [
            Contrasena,                                                  // texto plano guardado por error
            string.Empty,
            "PBKDF2-SHA256$600000$sal",                                  // faltan partes
            $"PBKDF2-SHA1${partes[1]}${partes[2]}${partes[3]}",          // otro algoritmo
            $"{partes[0]}$1000${partes[2]}${partes[3]}",                 // otras iteraciones
            $"{partes[0]}$abc${partes[2]}${partes[3]}",                  // iteraciones no numéricas
            $"{partes[0]}${partes[1]}$no-es-base64!${partes[3]}",        // sal inválida
            $"{partes[0]}${partes[1]}${partes[2]}$AAAA",                 // hash de largo incorrecto
            $"{valido}$extra",                                           // partes de más
        ];

        foreach (var hash in desconocidos)
        {
            Assert.False(_hasher.Verificar(Contrasena, hash), $"Aceptó el formato desconocido «{hash}»");
        }
    }

    [Fact]
    public void HasherPbkdf2_ContrasenaUtf16Invalida_DevuelveFalse()
    {
        const string surrogateSuelto = "clave-\uD800-123";
        var hash = _hasher.Hashear(ContrasenaNueva.Crear(Contrasena));

        foreach (var invalida in new[] { surrogateSuelto, "\uD800" })
        {
            var resultado = true;
            var excepcion = Record.Exception(() => resultado = _hasher.Verificar(invalida, hash));
            Assert.Null(excepcion);
            Assert.False(resultado);
        }

        // Al crear o cambiar la contraseña, el texto inválido es un error de validación del campo, no un 500.
        var error = Assert.Throws<ErrorValidacion>(() => _hasher.Hashear(ContrasenaNueva.Crear(surrogateSuelto)));
        Assert.Equal(ContrasenaNueva.Campo, error.Campo);
    }
}
