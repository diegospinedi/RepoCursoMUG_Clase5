using System.Globalization;
using System.Text;
using Optica.Api.Configuracion;
using Optica.Application;
using Optica.Application.Acceso;
using Optica.Infrastructure;

namespace Optica.Api.Herramientas;

/// <summary>
/// Blanqueo local de la contraseña (FR-06, AC-07): <c>Optica.Api --blanquear-contrasena</c> en la PC del
/// servidor. Deja el sistema sin contraseña, invalida todas las sesiones y deja una línea en
/// <c>{DirectorioAuditoria}/auditoria-blanqueo.log</c> (<c>fecha-hora ISO UTC | usuario | resultado</c>).
/// </summary>
/// <remarks>
/// <para>
/// Se detecta solo en los argumentos crudos de la línea de comandos, antes de construir cualquier host
/// (<see cref="EsInvocacion"/>). Nunca se lee de <c>IConfiguration</c> ni de variables de entorno, así no se puede
/// disparar por configuración (threat model C5, AC-08).
/// </para>
/// <para>
/// Usa un host genérico mínimo, sin Kestrel: no abre ningún puerto. Escribe la credencial solo a través del caso
/// de uso <see cref="BlanquearContrasena"/> y la unidad de trabajo, nunca con SQL propio.
/// </para>
/// </remarks>
public static class ComandoBlanqueo
{
    public const string Flag = "--blanquear-contrasena";
    public const string NombreArchivoAuditoria = "auditoria-blanqueo.log";
    public const int CodigoExito = 0;
    public const int CodigoError = 1;

    private static readonly UTF8Encoding Utf8SinBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>El blanqueo se pide solo con el argumento exacto <see cref="Flag"/> (comparación ordinal).</summary>
    public static bool EsInvocacion(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains(Flag, StringComparer.Ordinal);
    }

    public static async Task<int> EjecutarAsync(string[] args, TextWriter salida, TextWriter errores)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(salida);
        ArgumentNullException.ThrowIfNull(errores);

        var builder = CrearBuilder(args);

        // Las rutas se validan antes de registrar Infraestructura, que crea el directorio de la base.
        OpcionesOptica opciones;
        try
        {
            opciones = OpcionesOptica.Cargar(builder.Configuration);
        }
        catch (OpcionesOpticaInvalidas error)
        {
            await errores.WriteLineAsync($"No se pudo blanquear la contraseña. Configuración inválida:{Environment.NewLine}{error.Message}");
            return CodigoError;
        }

        bool habiaContrasena;
        try
        {
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddInfraestructura(opciones.RutaBase);
            builder.Services.AddAplicacion();

            using var host = builder.Build();
            await RegistroInfraestructura.AplicarMigracionesAsync(host.Services);
            await using var alcance = host.Services.CreateAsyncScope();
            habiaContrasena = await alcance.ServiceProvider.GetRequiredService<BlanquearContrasena>().EjecutarAsync();
        }
        catch (Exception error)
        {
            var detalle = $"{error.GetType().Name}: {UnaLinea(error.Message)}";
            await errores.WriteLineAsync($"No se pudo blanquear la contraseña. {detalle}");
            await AuditarSinFallarAsync(opciones, $"ERROR - {detalle}", errores);
            return CodigoError;
        }

        var resultado = habiaContrasena ? "OK - contraseña blanqueada" : "OK - no había contraseña definida";
        if (!await AuditarSinFallarAsync(opciones, resultado, errores))
        {
            await errores.WriteLineAsync("La contraseña se blanqueó, pero no quedó registrada en la auditoría.");
            return CodigoError;
        }

        await salida.WriteLineAsync(habiaContrasena
            ? "La contraseña se blanqueó. Al abrir el sistema se va a pedir crear una contraseña nueva."
            : "No había ninguna contraseña definida. Al abrir el sistema se va a pedir crearla.");
        return CodigoExito;
    }

    /// <summary>
    /// Las rutas que usaría el comando con estos argumentos, resueltas exactamente igual que en
    /// <see cref="EjecutarAsync"/>, sin tocar el disco ni la base. Lanza <see cref="OpcionesOpticaInvalidas"/> si
    /// la configuración no es válida. Permite verificar a qué base apunta antes de blanquear.
    /// </summary>
    public static OpcionesOptica ResolverOpciones(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return OpcionesOptica.Cargar(CrearBuilder(args).Configuration);
    }

    // El resto de los argumentos se pasa como configuración (por ejemplo, --Optica:RutaBase=...).
    private static HostApplicationBuilder CrearBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args.Where(a => !string.Equals(a, Flag, StringComparison.Ordinal)).ToArray(),
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = NombreEntorno(),
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        return builder;
    }

    /// <summary>
    /// El mismo entorno que el servidor: <c>DOTNET_ENVIRONMENT</c> si está definido; si no,
    /// <c>ASPNETCORE_ENVIRONMENT</c> (el que usa el servicio). Sin ninguno, Production. Así el comando lee las
    /// mismas rutas (<c>appsettings.{entorno}.json</c>) que el servidor.
    /// </summary>
    public static string? NombreEntorno()
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(dotnet))
        {
            return dotnet;
        }

        var aspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.IsNullOrWhiteSpace(aspNetCore) ? null : aspNetCore;
    }

    private static async Task<bool> AuditarSinFallarAsync(OpcionesOptica opciones, string resultado, TextWriter errores)
    {
        try
        {
            Directory.CreateDirectory(opciones.DirectorioAuditoria);
            var linea = string.Join(
                " | ",
                TimeProvider.System.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                UnaLinea(Usuario()),
                UnaLinea(resultado));
            await File.AppendAllTextAsync(
                Path.Combine(opciones.DirectorioAuditoria, NombreArchivoAuditoria),
                linea + Environment.NewLine,
                Utf8SinBom);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            await errores.WriteLineAsync($"No se pudo escribir la auditoría del blanqueo: {UnaLinea(error.Message)}");
            return false;
        }
    }

    private static string Usuario() =>
        OperatingSystem.IsWindows() ? $@"{Environment.UserDomainName}\{Environment.UserName}" : Environment.UserName;

    private static string UnaLinea(string texto) =>
        texto.ReplaceLineEndings(" ").Replace('|', '/');
}
