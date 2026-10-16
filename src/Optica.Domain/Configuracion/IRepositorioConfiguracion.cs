namespace Optica.Domain.Configuracion;

public interface IRepositorioConfiguracion
{
    Task<ConfiguracionNegocio> Obtener();

    Task Guardar(ConfiguracionNegocio configuracion);
}
