using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Optica.Infrastructure;

namespace Optica.Api.Tests;

/// <summary>
/// Directorios temporales de una prueba (base, claves de Data Protection, auditoría y un <c>wwwroot</c> con un
/// <c>index.html</c> mínimo generado acá), siempre fuera del repo. Se pueden compartir entre dos fábricas
/// para simular un reinicio del servidor sobre la misma base.
/// </summary>
public sealed class DirectoriosPrueba : IDisposable
{
    public const string ContenidoIndex = "<!doctype html><title>Óptica (prueba)</title>";

    public DirectoriosPrueba()
    {
        Raiz = Path.Combine(Path.GetTempPath(), "optica-api-tests", Guid.NewGuid().ToString("N"));
        Datos = Path.Combine(Raiz, "datos");
        Claves = Path.Combine(Raiz, "claves");
        Auditoria = Path.Combine(Raiz, "auditoria");
        WebRoot = Path.Combine(Raiz, "wwwroot");
        Directory.CreateDirectory(WebRoot);
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), ContenidoIndex);
    }

    public string Raiz { get; }

    public string Datos { get; }

    public string Claves { get; }

    public string Auditoria { get; }

    public string WebRoot { get; }

    public string ArchivoAuditoria => Path.Combine(Auditoria, "auditoria-blanqueo.log");

    /// <summary>Argumentos de línea de comandos que apuntan el comando de blanqueo a estos directorios.</summary>
    public string[] ArgumentosRutas() =>
    [
        $"--Optica:RutaBase={Datos}",
        $"--Optica:DirectorioClaves={Claves}",
        $"--Optica:DirectorioAuditoria={Auditoria}",
    ];

    /// <summary>Conexión cruda (sin EF) a la base, para inspeccionar o bloquear la credencial.</summary>
    public SqliteConnection AbrirConexionCruda()
    {
        var conexion = new SqliteConnection(RegistroInfraestructura.CadenaConexion(Datos));
        conexion.Open();
        return conexion;
    }

    public void Dispose()
    {
        using (var conexion = new SqliteConnection(RegistroInfraestructura.CadenaConexion(Datos)))
        {
            SqliteConnection.ClearPool(conexion);
        }

        for (var intento = 0; Directory.Exists(Raiz); intento++)
        {
            try
            {
                Directory.Delete(Raiz, recursive: true);
            }
            catch (IOException) when (intento < 10)
            {
                Thread.Sleep(100);
            }
        }
    }
}

/// <summary>
/// Levanta la API completa en memoria con una base temporal, un <see cref="FakeTimeProvider"/> y un proveedor
/// de logs que captura todo (nivel Trace) para poder buscar datos sensibles en la salida.
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<Program>
{
    public const string Entorno = "Pruebas";

    private readonly bool _directoriosPropios;
    private readonly Action<IServiceCollection>? _servicios;
    private readonly Action<IWebHostBuilder>? _host;

    public FabricaApi(
        DirectoriosPrueba? directorios = null,
        Action<IServiceCollection>? servicios = null,
        Action<IWebHostBuilder>? host = null)
    {
        _directoriosPropios = directorios is null;
        Directorios = directorios ?? new DirectoriosPrueba();
        _servicios = servicios;
        _host = host;
    }

    public DirectoriosPrueba Directorios { get; }

    public FakeTimeProvider Reloj { get; } = new(DateTimeOffset.UtcNow);

    public ProveedorLogsCapturados Logs { get; } = new();

    /// <summary>Tipos de servicio registrados en el contenedor de la aplicación (para revisar qué se habilitó).</summary>
    public IReadOnlyList<Type> TiposRegistrados { get; private set; } = [];

    public ClienteApi CrearCliente(string baseUrl = "http://localhost/") => new(this, new Uri(baseUrl));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Entorno);
        builder.UseSetting("Optica:RutaBase", Directorios.Datos);
        builder.UseSetting("Optica:DirectorioClaves", Directorios.Claves);
        builder.UseSetting("Optica:DirectorioAuditoria", Directorios.Auditoria);
        builder.UseSetting(WebHostDefaults.WebRootKey, Directorios.WebRoot);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Logs);
            logging.AddFilter<ProveedorLogsCapturados>(null, LogLevel.Trace);
        });
        builder.ConfigureTestServices(servicios =>
        {
            servicios.AddSingleton<TimeProvider>(Reloj);
            _servicios?.Invoke(servicios);
            TiposRegistrados = servicios.Select(d => d.ServiceType).ToList();
        });
        _host?.Invoke(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_directoriosPropios)
        {
            Directorios.Dispose();
        }
    }
}

/// <summary>Cliente HTTP con su propio almacén de cookies: cada instancia es un navegador distinto.</summary>
public sealed class ClienteApi : IDisposable
{
    public const string CookieSesion = "optica.sesion";
    public const string CookieXsrf = "XSRF-TOKEN";
    public const string HeaderXsrf = "X-XSRF-TOKEN";

    private readonly Uri _base;

    public ClienteApi(WebApplicationFactory<Program> fabrica, Uri baseUrl)
    {
        _base = baseUrl;
        Cookies = new CookieContainer();
        Http = fabrica.CreateDefaultClient(baseUrl, new CookieContainerHandler(Cookies));
    }

    public CookieContainer Cookies { get; }

    public HttpClient Http { get; }

    public string? TokenXsrf => Cookies.GetCookies(_base)[CookieXsrf]?.Value;

    public string? ValorCookieSesion => Cookies.GetCookies(_base)[CookieSesion]?.Value;

    public Task<HttpResponseMessage> ObtenerEstadoAsync() => Http.GetAsync("api/acceso/ingreso");

    public async Task<JsonElement> EstadoAsync()
    {
        using var respuesta = await ObtenerEstadoAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Hace lo mismo que el SPA al abrir: pide el estado, que emite la cookie del token antiforgery.</summary>
    public async Task PrepararAntiforgeryAsync()
    {
        using var respuesta = await ObtenerEstadoAsync();
    }

    public async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo, bool conToken = true)
    {
        if (conToken && TokenXsrf is null)
        {
            await PrepararAntiforgeryAsync();
        }

        var pedido = new HttpRequestMessage(metodo, ruta)
        {
            Content = cuerpo switch
            {
                null => null,
                string json => new StringContent(json, Encoding.UTF8, "application/json"),
                _ => JsonContent.Create(cuerpo),
            },
        };
        if (conToken && TokenXsrf is { } token)
        {
            pedido.Headers.Add(HeaderXsrf, token);
        }

        return await Http.SendAsync(pedido);
    }

    public Task<HttpResponseMessage> CrearContrasenaAsync(string contrasena) =>
        EnviarAsync(HttpMethod.Post, "api/acceso/contrasena-inicial", new { contrasena });

    public Task<HttpResponseMessage> IngresarAsync(string contrasena) =>
        EnviarAsync(HttpMethod.Post, "api/acceso/ingreso", new { contrasena });

    public Task<HttpResponseMessage> SalirAsync() => EnviarAsync(HttpMethod.Post, "api/acceso/salida", null);

    public Task<HttpResponseMessage> CambiarContrasenaAsync(string? contrasenaActual, string? contrasenaNueva) =>
        EnviarAsync(HttpMethod.Put, "api/acceso/contrasena", new { contrasenaActual, contrasenaNueva });

    public Task<HttpResponseMessage> ObtenerConfiguracionAsync() => Http.GetAsync("api/configuracion");

    public Task<HttpResponseMessage> ActualizarConfiguracionAsync(object cuerpo) =>
        EnviarAsync(HttpMethod.Put, "api/configuracion", cuerpo);

    /// <summary>Ingresa y exige 204 con la cookie de sesión emitida.</summary>
    public async Task IniciarSesionAsync(string contrasena)
    {
        using var respuesta = await IngresarAsync(contrasena);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.NotNull(ValorCookieSesion);
    }

    public void Dispose() => Http.Dispose();
}

/// <summary>Una entrada de log capturada con todo lo que un proveedor real podría escribir.</summary>
public sealed record EntradaLog(LogLevel Nivel, string Categoria, EventId Evento, string Texto)
{
    public override string ToString() => $"{Nivel} {Categoria} [{Evento.Id}:{Evento.Name}] {Texto}";
}

/// <summary>Proveedor de logs que guarda en memoria el mensaje, los valores del estado, los scopes y la excepción.</summary>
public sealed class ProveedorLogsCapturados : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<EntradaLog> _entradas = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<EntradaLog> Entradas => _entradas.ToArray();

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Registrador(ProveedorLogsCapturados proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => proveedor._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var texto = new StringBuilder(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> valores)
            {
                foreach (var (clave, valor) in valores)
                {
                    texto.Append(" | ").Append(clave).Append('=').Append(Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            proveedor._scopes.ForEachScope((scope, acumulado) => acumulado.Append(" | scope=").Append(scope), texto);
            if (exception is not null)
            {
                texto.Append(" | excepcion=").Append(exception);
            }

            proveedor._entradas.Enqueue(new EntradaLog(logLevel, categoria, eventId, texto.ToString()));
        }
    }
}

/// <summary>Ubicación del repo, para las pruebas que verifican que nada se cree adentro.</summary>
public static class Repo
{
    public static string Raiz()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Optica.sln")))
        {
            directorio = directorio.Parent;
        }

        return directorio?.FullName ?? throw new InvalidOperationException("No se encontró Optica.sln.");
    }
}

/// <summary>Resultado de ejecutar la API como proceso real.</summary>
public sealed record ResultadoProceso(bool Termino, int Codigo, string Salida, bool EscuchabaAlFinal);

/// <summary>
/// Ejecuta la API compilada como proceso real (el mismo <c>Optica.Api.dll</c> que cargan las pruebas, con su
/// <c>runtimeconfig.json</c> y su <c>appsettings.json</c>), en el entorno <see cref="FabricaApi.Entorno"/>.
/// </summary>
public static class ProcesoApi
{
    public static string RutaDll => typeof(Program).Assembly.Location;

    /// <summary>
    /// Espera a que el proceso termine (como máximo <paramref name="espera"/>). Si no terminó, lo mata. Antes de
    /// matarlo, y también después de que termine, prueba si <paramref name="puerto"/> acepta conexiones en 127.0.0.1.
    /// </summary>
    public static async Task<ResultadoProceso> EjecutarAsync(TimeSpan espera, int? puerto, params string[] argumentos)
    {
        var inicio = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(RutaDll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        inicio.ArgumentList.Add(RutaDll);
        foreach (var argumento in argumentos)
        {
            inicio.ArgumentList.Add(argumento);
        }

        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = FabricaApi.Entorno;
        inicio.Environment.Remove("DOTNET_ENVIRONMENT");

        using var proceso = System.Diagnostics.Process.Start(inicio)!;
        var errores = proceso.StandardError.ReadToEndAsync();
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var termino = proceso.WaitForExit(espera);
        var escuchaba = puerto is { } p && PuertoAbierto(p);
        if (!termino)
        {
            proceso.Kill(entireProcessTree: true);
        }

        await proceso.WaitForExitAsync();
        return new ResultadoProceso(termino, proceso.ExitCode, await errores + await salida, escuchaba);
    }

    public static int PuertoLibre()
    {
        var oyente = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        oyente.Start();
        var puerto = ((IPEndPoint)oyente.LocalEndpoint).Port;
        oyente.Stop();
        return puerto;
    }

    private static bool PuertoAbierto(int puerto)
    {
        try
        {
            using var cliente = new System.Net.Sockets.TcpClient();
            return cliente.ConnectAsync(IPAddress.Loopback, puerto).Wait(TimeSpan.FromSeconds(2)) && cliente.Connected;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
