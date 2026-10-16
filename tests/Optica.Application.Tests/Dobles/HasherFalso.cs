using Optica.Domain.Acceso;

namespace Optica.Application.Tests.Dobles;

/// <summary>Hash trivial y reversible, solo para tests. Cuenta las llamadas a <see cref="Verificar"/>.</summary>
public sealed class HasherFalso : IHasherContrasena
{
    public int LlamadasAVerificar { get; private set; }

    public static string HashDe(string contrasena) => "falso$" + contrasena;

    public string Hashear(ContrasenaNueva contrasena) => HashDe(contrasena.Valor);

    public bool Verificar(string contrasena, string hash)
    {
        LlamadasAVerificar++;
        return hash == HashDe(contrasena);
    }
}
