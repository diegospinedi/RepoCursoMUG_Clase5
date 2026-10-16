namespace Optica.Domain.Acceso;

public interface IRepositorioCredencial
{
    Task<CredencialAcceso> Obtener();

    /// <summary>
    /// Define la contraseña solo si todavía no hay una (condicional en la base).
    /// Devuelve <c>false</c> si ya estaba definida, sin modificar nada.
    /// </summary>
    Task<bool> CrearSiNoExiste(string hash, Guid sello);

    Task Guardar(CredencialAcceso credencial);
}
