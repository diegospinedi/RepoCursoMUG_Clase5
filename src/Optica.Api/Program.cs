using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Optica.Api;
using Optica.Api.Configuracion;
using Optica.Api.Controllers;
using Optica.Api.Errores;
using Optica.Api.Herramientas;
using Optica.Api.Seguridad;
using Optica.Application;
using Optica.Infrastructure;

// Composition root. El blanqueo se detecta en los argumentos crudos, antes de crear cualquier builder: no se
// puede disparar desde la configuración ni desde variables de entorno, y no levanta Kestrel (FR-06, AC-08).
if (ComandoBlanqueo.EsInvocacion(args))
{
    return await ComandoBlanqueo.EjecutarAsync(args, Console.Out, Console.Error);
}

var builder = WebApplication.CreateBuilder(args);

// E4-11: las rutas se validan ANTES de registrar Infraestructura, que crea el directorio de la base. Si alguna
// cae dentro del repo, el proceso termina con código 1 sin crear nada.
OpcionesOptica opciones;
try
{
    opciones = OpcionesOptica.Cargar(builder.Configuration);
}
catch (OpcionesOpticaInvalidas error)
{
    await Console.Error.WriteLineAsync($"El servidor no arranca. Configuración inválida:{Environment.NewLine}{error.Message}");
    return 1;
}

opciones.CrearDirectoriosAuxiliares();

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService(servicio => servicio.ServiceName = "OpticaSistema");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfraestructura(opciones.RutaBase);
builder.Services.AddAplicacion();

// Claves de Data Protection (cifran la cookie de sesión) persistidas fuera del repo; en Windows, con DPAPI.
var proteccionDatos = builder.Services.AddDataProtection()
    .SetApplicationName("OpticaSistema")
    .PersistKeysToFileSystem(new DirectoryInfo(opciones.DirectorioClaves));
if (OperatingSystem.IsWindows())
{
    proteccionDatos.ProtectKeysWithDpapi();
}

builder.Services.AddSesionOptica();

builder.Services
    .AddControllers(mvc => mvc.Filters.Add<FiltroAntiforgery>())
    .AddJsonOptions(json =>
    {
        // Lectura estricta: sin campos de más, sin nulls en campos no anulables, sin campos faltantes, sin
        // números como texto y con los nombres exactos.
        var serializacion = json.JsonSerializerOptions;
        serializacion.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
        serializacion.RespectNullableAnnotations = true;
        serializacion.RespectRequiredConstructorParameters = true;
        serializacion.PropertyNameCaseInsensitive = false;
        serializacion.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
        serializacion.Converters.Add(new ConversorCondicionFiscal());
    })
    .ConfigureApiBehaviorOptions(api => api.InvalidModelStateResponseFactory = RespuestaValidacion.Crear);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

// Sin HttpLogging ni logging de datos sensibles de EF, en ningún entorno: la contraseña, el hash, el sello y la
// cookie no pueden aparecer en los logs (AC-11, R-07).
var app = builder.Build();

await RegistroInfraestructura.AplicarMigracionesAsync(app.Services);

// Antes que nada: un pedido que llegó por una interfaz que no es loopback no se procesa (AR-01).
app.UseMiddleware<SoloLoopback>();
app.UseHostFiltering();
app.UseMiddleware<EncabezadosSeguridad>();
app.UseExceptionHandler();
app.UseMiddleware<LimiteCuerpo>();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Una ruta /api desconocida no cae en el SPA: exige sesión (FallbackPolicy) y responde 404.
app.MapFallback("api/{**ruta}", contexto =>
{
    contexto.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
});

// El SPA (incluida la pantalla de ingreso) se sirve sin sesión; no contiene datos.
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.StartAsync();

// AR-01: si la configuración lo hizo escuchar fuera de loopback, o el servidor no informa sus direcciones, se
// detiene. Con TestServer (pruebas en memoria) no hay direcciones y la guarda no aplica.
var servidor = app.Services.GetRequiredService<IServer>();
var direcciones = servidor.Features.Get<IServerAddressesFeature>()?.Addresses;
if (GuardaLoopback.Problema(direcciones, GuardaLoopback.EsServidorDePruebas(servidor)) is { } problema)
{
    app.Logger.DireccionFueraDeLoopback(problema);
    await Console.Error.WriteLineAsync($"El servidor no arranca: {problema}. Revisá 'Urls'.");
    await app.StopAsync();
    await app.DisposeAsync();
    return 1;
}

await app.WaitForShutdownAsync();
return 0;

/// <summary>Punto de entrada; parcial y público para <c>WebApplicationFactory</c> en las pruebas.</summary>
public partial class Program;
