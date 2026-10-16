using Microsoft.AspNetCore.Mvc;
using Optica.Application.Configuracion;
using Optica.Domain.Configuracion;

namespace Optica.Api.Controllers;

/// <summary>
/// Los cuatro parámetros del negocio, tal como viajan por la API. Los cuatro campos son obligatorios en el
/// cuerpo; <c>topeIdentificacion</c> admite <c>null</c>. No hay ningún otro campo (mass assignment).
/// </summary>
public sealed record ConfiguracionDto(
    decimal AlicuotaIva,
    CondicionFiscal CondicionFiscal,
    decimal? TopeIdentificacion,
    decimal MultiploRedondeo)
{
    public static ConfiguracionDto Desde(DatosConfiguracion datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return new(datos.AlicuotaIva, datos.CondicionFiscal, datos.TopeIdentificacion, datos.MultiploRedondeo);
    }

    public DatosConfiguracion ADatos() => new(AlicuotaIva, CondicionFiscal, TopeIdentificacion, MultiploRedondeo);
}

/// <summary>Configuración del negocio (FR-07 a FR-15). Exige sesión (política por defecto).</summary>
[ApiController]
[Route("api/configuracion")]
public sealed class ConfiguracionController(ILogger<ConfiguracionController> registro, TimeProvider reloj) : ControllerBase
{
    [HttpGet]
    public async Task<ConfiguracionDto> Obtener([FromServices] ObtenerConfiguracion obtenerConfiguracion) =>
        ConfiguracionDto.Desde(await obtenerConfiguracion.EjecutarAsync());

    [HttpPut]
    public async Task<ConfiguracionDto> Actualizar(ConfiguracionDto pedido, [FromServices] ActualizarConfiguracion actualizarConfiguracion)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var guardada = await actualizarConfiguracion.EjecutarAsync(pedido.ADatos());
        registro.ConfiguracionActualizada(reloj.GetUtcNow());
        return ConfiguracionDto.Desde(guardada);
    }
}
