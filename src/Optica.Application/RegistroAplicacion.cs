using Microsoft.Extensions.DependencyInjection;
using Optica.Application.Acceso;
using Optica.Application.Configuracion;

namespace Optica.Application;

/// <summary>
/// Registra los casos de uso. Los repositorios, el hasher, la unidad de trabajo y el
/// <see cref="TimeProvider"/> los registra el composition root.
/// </summary>
public static class RegistroAplicacion
{
    public static IServiceCollection AddAplicacion(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<ObtenerEstadoAcceso>();
        servicios.AddScoped<CrearContrasenaInicial>();
        servicios.AddScoped<IniciarSesion>();
        servicios.AddScoped<ValidarSesion>();
        servicios.AddScoped<CambiarContrasena>();
        servicios.AddScoped<BlanquearContrasena>();
        servicios.AddScoped<ObtenerConfiguracion>();
        servicios.AddScoped<ActualizarConfiguracion>();

        return servicios;
    }
}
