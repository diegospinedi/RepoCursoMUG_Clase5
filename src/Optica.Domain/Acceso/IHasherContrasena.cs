namespace Optica.Domain.Acceso;

public interface IHasherContrasena
{
    string Hashear(ContrasenaNueva contrasena);

    bool Verificar(string contrasena, string hash);
}
