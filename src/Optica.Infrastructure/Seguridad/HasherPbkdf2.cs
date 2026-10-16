using System.Globalization;
using System.Security.Cryptography;
using Optica.Domain.Acceso;
using Optica.Domain.Comun;

namespace Optica.Infrastructure.Seguridad;

/// <summary>
/// Hash de la contraseña con PBKDF2-HMAC-SHA256, sal aleatoria de 16 bytes y 600.000 iteraciones
/// (NFR-01). Formato: <c>PBKDF2-SHA256$600000$&lt;sal base64&gt;$&lt;hash base64&gt;</c>.
/// La contraseña en texto plano nunca se guarda ni se loguea.
/// </summary>
/// <remarks>
/// Un texto con UTF-16 inválido (un surrogate suelto, por ejemplo <c>"\uD800"</c>) no se puede codificar en
/// UTF-8 sin pérdida: <see cref="Hashear"/> lo rechaza como error de validación del campo y
/// <see cref="Verificar"/> devuelve <c>false</c> sin lanzar.
/// </remarks>
public sealed class HasherPbkdf2 : IHasherContrasena
{
    public const string Algoritmo = "PBKDF2-SHA256";
    public const int Iteraciones = 600_000;
    public const int BytesSal = 16;
    public const int BytesHash = 32;

    private const char Separador = '$';
    private static readonly HashAlgorithmName FuncionHash = HashAlgorithmName.SHA256;

    public string Hashear(ContrasenaNueva contrasena)
    {
        ArgumentNullException.ThrowIfNull(contrasena);
        if (!EsUtf16Valido(contrasena.Valor))
        {
            throw new ErrorValidacion(ContrasenaNueva.Campo, "La contraseña contiene caracteres no válidos");
        }

        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Derivar(contrasena.Valor, sal, Iteraciones);

        return string.Join(
            Separador,
            Algoritmo,
            Iteraciones.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(sal),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Compara en tiempo constante. Un hash con formato desconocido devuelve <c>false</c> sin lanzar (E2-1).
    /// </summary>
    public bool Verificar(string contrasena, string hash)
    {
        if (contrasena is null || string.IsNullOrEmpty(hash) || !EsUtf16Valido(contrasena))
        {
            return false;
        }

        var partes = hash.Split(Separador);
        if (partes.Length != 4
            || !string.Equals(partes[0], Algoritmo, StringComparison.Ordinal)
            || !string.Equals(partes[1], Iteraciones.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return false;
        }

        var sal = new byte[BytesSal];
        var esperado = new byte[BytesHash];
        if (!Convert.TryFromBase64String(partes[2], sal, out var largoSal) || largoSal != BytesSal
            || !Convert.TryFromBase64String(partes[3], esperado, out var largoHash) || largoHash != BytesHash)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derivar(contrasena, sal, Iteraciones), esperado);
    }

    // Cada surrogate alto tiene que estar seguido de uno bajo, y no puede haber surrogates bajos sueltos.
    private static bool EsUtf16Valido(string texto)
    {
        for (var i = 0; i < texto.Length; i++)
        {
            if (char.IsHighSurrogate(texto[i]))
            {
                if (i + 1 >= texto.Length || !char.IsLowSurrogate(texto[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(texto[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] Derivar(string contrasena, byte[] sal, int iteraciones) =>
        Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, FuncionHash, BytesHash);
}
