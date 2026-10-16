using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Optica.Api.Configuracion;
using Optica.Api.Herramientas;
using Optica.Application.Acceso;

namespace Optica.Api.Tests;

/// <summary>
/// Estas pruebas modifican variables de entorno del proceso: corren solas, sin otras colecciones en paralelo.
/// </summary>
[CollectionDefinition(nameof(ColeccionSinParalelismo), DisableParallelization = true)]
public sealed class ColeccionSinParalelismo;

/// <remarks>
/// <para>
/// Ningún test de esta clase puede llegar a las rutas reales de una instalación (<c>%ProgramData%\OpticaSistema</c>):
/// si el comando las resolviera por error (por ejemplo, con el entorno equivocado), blanquearía la contraseña real.
/// Hay dos protecciones:
/// </para>
/// <list type="number">
/// <item>Antes de cada blanqueo, <see cref="AssertApuntaAlTemporal"/> resuelve las rutas con
/// <see cref="ComandoBlanqueo.ResolverOpciones"/> y falla sin ejecutar nada si no caen en el temporal del test.</item>
/// <item>Red de seguridad: durante cada test, <c>ProgramData</c> y <c>LocalAppData</c> apuntan a un directorio
/// temporal, así que aun las rutas por defecto de Production y Development caerían fuera de cualquier dato real.</item>
/// </list>
/// </remarks>
[Collection(nameof(ColeccionSinParalelismo))]
public sealed class ComandoBlanqueoTests : IDisposable
{
    private static readonly Regex LineaAuditoria = new(@"^(?<fecha>\S+) \| (?<usuario>.+) \| (?<resultado>.+)$", RegexOptions.CultureInvariant);

    private static readonly string[] CarpetasRedirigidas = ["ProgramData", "LocalAppData"];

    private readonly string _redDeSeguridad = Path.Combine(Path.GetTempPath(), "optica-red-seguridad", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _valoresAnteriores = [];

    public ComandoBlanqueoTests()
    {
        foreach (var carpeta in CarpetasRedirigidas)
        {
            _valoresAnteriores[carpeta] = Environment.GetEnvironmentVariable(carpeta);
            Environment.SetEnvironmentVariable(carpeta, Path.Combine(_redDeSeguridad, carpeta));
        }
    }

    public void Dispose()
    {
        foreach (var (carpeta, valor) in _valoresAnteriores)
        {
            Environment.SetEnvironmentVariable(carpeta, valor);
        }

        if (Directory.Exists(_redDeSeguridad))
        {
            Directory.Delete(_redDeSeguridad, recursive: true);
        }
    }

    [Fact]
    public void Opciones_CarpetasConocidas_UsanLaVariableDeEntorno()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Optica:RutaBase"] = "%ProgramData%/OpticaSistema",
            ["Optica:DirectorioClaves"] = "%LocalAppData%/OpticaSistema/claves",
            ["Optica:DirectorioAuditoria"] = "%ProgramData%/OpticaSistema/auditoria",
        }).Build();

        var opciones = OpcionesOptica.Cargar(configuracion);

        Assert.Equal(Path.GetFullPath(Path.Combine(_redDeSeguridad, "ProgramData", "OpticaSistema")), opciones.RutaBase);
        Assert.Equal(Path.GetFullPath(Path.Combine(_redDeSeguridad, "LocalAppData", "OpticaSistema", "claves")), opciones.DirectorioClaves);
        Assert.False(Directory.Exists(_redDeSeguridad), "Cargar no tiene que crear directorios");
    }

    [Fact]
    public async Task ComandoBlanqueo_DejaSinContrasena_EstadoInformaNoDefinida()
    {
        using var directorios = new DirectoriosPrueba();
        await using var fabrica = new FabricaApi(directorios);
        await CrearContrasenaAsync(fabrica, AccesoApiTests.Contrasena);
        Assert.True((await EstadoAsync(fabrica)).ContrasenaDefinida);

        var (codigo, salida, errores) = await BlanquearAsync(directorios);

        Assert.Equal(0, codigo);
        Assert.Equal(string.Empty, errores);
        Assert.Contains("blanque", salida, StringComparison.OrdinalIgnoreCase);
        Assert.False((await EstadoAsync(fabrica)).ContrasenaDefinida);

        // En el ingreso siguiente se muestra la creación: el estado lo informa y se puede volver a crear.
        using var cliente = fabrica.CrearCliente();
        Assert.False((await cliente.EstadoAsync()).GetProperty("contrasenaDefinida").GetBoolean());
        using var creacion = await cliente.CrearContrasenaAsync(AccesoApiTests.OtraContrasena);
        Assert.Equal(HttpStatusCode.NoContent, creacion.StatusCode);
        await cliente.IniciarSesionAsync(AccesoApiTests.OtraContrasena);
    }

    [Fact]
    public async Task ComandoBlanqueo_InvalidaSesionesAbiertas()
    {
        using var directorios = new DirectoriosPrueba();
        await using var fabrica = new FabricaApi(directorios);
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);
        using (var antes = await cliente.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        var (codigo, _, _) = await BlanquearAsync(directorios);
        Assert.Equal(0, codigo);

        using var despues = await cliente.ObtenerConfiguracionAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
        Assert.Null(cliente.ValorCookieSesion);
        var estado = await cliente.EstadoAsync();
        Assert.False(estado.GetProperty("sesionActiva").GetBoolean());
        Assert.False(estado.GetProperty("contrasenaDefinida").GetBoolean());
    }

    [Fact]
    public async Task ComandoBlanqueo_EscribeAuditoria()
    {
        using var directorios = new DirectoriosPrueba();
        await using var fabrica = new FabricaApi(directorios);
        await CrearContrasenaAsync(fabrica, AccesoApiTests.Contrasena);
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);

        Assert.Equal(0, (await BlanquearAsync(directorios)).Codigo);
        Assert.Equal(0, (await BlanquearAsync(directorios)).Codigo);

        var despues = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.True(File.Exists(directorios.ArchivoAuditoria), "No se escribió auditoria-blanqueo.log");
        var lineas = File.ReadAllLines(directorios.ArchivoAuditoria, Encoding.UTF8);
        Assert.Equal(2, lineas.Length);
        foreach (var linea in lineas)
        {
            var partes = LineaAuditoria.Match(linea);
            Assert.True(partes.Success, $"Línea de auditoría con formato inesperado: {linea}");
            var fecha = partes.Groups["fecha"].Value;
            Assert.EndsWith("Z", fecha, StringComparison.Ordinal);
            Assert.InRange(DateTimeOffset.Parse(fecha, CultureInfo.InvariantCulture), antes, despues);
            Assert.Contains(Environment.UserName, partes.Groups["usuario"].Value, StringComparison.Ordinal);
            Assert.StartsWith("OK", partes.Groups["resultado"].Value, StringComparison.Ordinal);
        }

        Assert.Contains("blanqueada", lineas[0], StringComparison.Ordinal);
        Assert.Contains("no había contraseña", lineas[1], StringComparison.Ordinal);
        Assert.DoesNotContain(AccesoApiTests.Contrasena, File.ReadAllText(directorios.ArchivoAuditoria), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComandoBlanqueo_BaseInaccesible_SaleConCodigo1()
    {
        using var directorios = new DirectoriosPrueba();

        // La ruta del archivo de la base es un directorio: SQLite no la puede abrir.
        Directory.CreateDirectory(Path.Combine(directorios.Datos, "optica.db"));

        var (codigo, _, errores) = await BlanquearAsync(directorios);

        Assert.Equal(1, codigo);
        Assert.False(string.IsNullOrWhiteSpace(errores), "El comando falló sin mensaje en la consola");
        Assert.True(File.Exists(directorios.ArchivoAuditoria), "El error no quedó en la auditoría");
        var linea = Assert.Single(File.ReadAllLines(directorios.ArchivoAuditoria, Encoding.UTF8));
        Assert.Matches(LineaAuditoria, linea);
        Assert.StartsWith("ERROR", LineaAuditoria.Match(linea).Groups["resultado"].Value, StringComparison.Ordinal);

        // Con la base dentro del repo tampoco arranca, y no crea el directorio.
        var dentro = Path.Combine(Repo.Raiz(), "tests", "Optica.Api.Tests", "bin", $"blanqueo-dentro-del-repo-{Guid.NewGuid():N}");
        try
        {
            string[] argumentosDentro =
                [ComandoBlanqueo.Flag, $"--Optica:RutaBase={dentro}", $"--Optica:DirectorioClaves={directorios.Claves}", $"--Optica:DirectorioAuditoria={directorios.Auditoria}"];

            // Seguro de ejecutar: estas rutas se rechazan antes de abrir cualquier base.
            Assert.Throws<OpcionesOpticaInvalidas>(() => ComandoBlanqueo.ResolverOpciones(argumentosDentro));
            var salidaDentro = new StringWriter();
            var erroresDentro = new StringWriter();
            var codigoDentro = await ComandoBlanqueo.EjecutarAsync(argumentosDentro, salidaDentro, erroresDentro);

            Assert.Equal(1, codigoDentro);
            Assert.Contains("Optica:RutaBase", erroresDentro.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(dentro), "El comando creó el directorio de la base dentro del repo");
        }
        finally
        {
            if (Directory.Exists(dentro))
            {
                Directory.Delete(dentro, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Arranque_SinFlag_NuncaBlanquea()
    {
        // Solo el argumento exacto dispara el blanqueo.
        Assert.True(ComandoBlanqueo.EsInvocacion([ComandoBlanqueo.Flag]));
        Assert.True(ComandoBlanqueo.EsInvocacion(["--Optica:RutaBase=/datos", ComandoBlanqueo.Flag]));
        Assert.False(ComandoBlanqueo.EsInvocacion([]));
        Assert.False(ComandoBlanqueo.EsInvocacion(["--blanquear-contrasena=true"]));
        Assert.False(ComandoBlanqueo.EsInvocacion(["--BLANQUEAR-CONTRASENA"]));
        Assert.False(ComandoBlanqueo.EsInvocacion(["blanquear-contrasena"]));
        Assert.False(ComandoBlanqueo.EsInvocacion(["--Optica:BlanquearContrasena=true"]));

        // Reiniciar el servidor sobre la misma base no toca la contraseña.
        using var directorios = new DirectoriosPrueba();
        await using (var primera = new FabricaApi(directorios))
        {
            await CrearContrasenaAsync(primera, AccesoApiTests.Contrasena);
        }

        await using var segunda = new FabricaApi(directorios);
        Assert.True((await EstadoAsync(segunda)).ContrasenaDefinida);
        using var cliente = segunda.CrearCliente();
        await cliente.IniciarSesionAsync(AccesoApiTests.Contrasena);
    }

    [Fact]
    public async Task Blanqueo_NoSeDisparaPorConfiguracion()
    {
        using var directorios = new DirectoriosPrueba();
        await using (var primera = new FabricaApi(directorios))
        {
            await CrearContrasenaAsync(primera, AccesoApiTests.Contrasena);
        }

        var variables = new[]
        {
            "blanquear-contrasena", "BLANQUEAR_CONTRASENA", "DOTNET_blanquear-contrasena", "ASPNETCORE_blanquear-contrasena",
            "Optica__BlanquearContrasena", "DOTNET_BLANQUEAR_CONTRASENA",
        };
        var appsettings = Path.Combine(directorios.Raiz, "appsettings.blanqueo.json");
        File.WriteAllText(
            appsettings,
            """{ "blanquear-contrasena": true, "--blanquear-contrasena": true, "BlanquearContrasena": true, "Optica": { "BlanquearContrasena": true } }""");
        try
        {
            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, "true");
            }

            await using var segunda = new FabricaApi(directorios, host: builder =>
            {
                builder.ConfigureAppConfiguration(configuracion => configuracion.AddJsonFile(appsettings));
                builder.UseSetting("blanquear-contrasena", "true");
                builder.UseSetting("--blanquear-contrasena", "true");
            });

            Assert.True((await EstadoAsync(segunda)).ContrasenaDefinida, "El servidor blanqueó la contraseña por configuración");
            using var cliente = segunda.CrearCliente();
            await cliente.IniciarSesionAsync(AccesoApiTests.Contrasena);

            // Control: con el mismo entorno, el único disparador es el argumento explícito.
            Assert.Equal(0, (await BlanquearAsync(directorios)).Codigo);
            Assert.False((await EstadoAsync(segunda)).ContrasenaDefinida);
        }
        finally
        {
            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }

    [Fact]
    public async Task ComandoBlanqueo_SinDotnetEnvironment_UsaElEntornoDeAspNetCore()
    {
        using var directorios = new DirectoriosPrueba();
        await using (var fabrica = new FabricaApi(directorios))
        {
            await CrearContrasenaAsync(fabrica, AccesoApiTests.Contrasena);
        }

        // Las rutas solo están en el appsettings del entorno: si el comando no lo carga, usa las de producción.
        var entorno = $"PruebaEntorno{Guid.NewGuid():N}"[..24];
        var archivo = Path.Combine(AppContext.BaseDirectory, $"appsettings.{entorno}.json");
        File.WriteAllText(archivo, System.Text.Json.JsonSerializer.Serialize(new
        {
            Optica = new { RutaBase = directorios.Datos, DirectorioClaves = directorios.Claves, DirectorioAuditoria = directorios.Auditoria },
        }));
        var dotnetAnterior = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var aspNetAnterior = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            // Como el servicio: solo ASPNETCORE_ENVIRONMENT definido.
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", entorno);
            var (codigo, _, errores) = await BlanquearSoloConFlagAsync(directorios);
            Assert.True(codigo == 0, $"El comando no usó el entorno de ASPNETCORE_ENVIRONMENT: {errores}");

            // Si DOTNET_ENVIRONMENT está definido, tiene prioridad.
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", entorno);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            var (codigoDotnet, _, erroresDotnet) = await BlanquearSoloConFlagAsync(directorios);
            Assert.True(codigoDotnet == 0, $"El comando no usó el entorno de DOTNET_ENVIRONMENT: {erroresDotnet}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnetAnterior);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", aspNetAnterior);
            File.Delete(archivo);
        }

        Assert.Equal(2, File.ReadAllLines(directorios.ArchivoAuditoria).Length);
        await using var despues = new FabricaApi(directorios);
        Assert.False((await EstadoAsync(despues)).ContrasenaDefinida);
    }

    // ---- Ayudas ----

    private static Task<(int Codigo, string Salida, string Errores)> BlanquearAsync(DirectoriosPrueba directorios) =>
        BlanquearVerificadoAsync(directorios, [ComandoBlanqueo.Flag, .. directorios.ArgumentosRutas()]);

    private static Task<(int Codigo, string Salida, string Errores)> BlanquearSoloConFlagAsync(DirectoriosPrueba directorios) =>
        BlanquearVerificadoAsync(directorios, [ComandoBlanqueo.Flag]);

    private static async Task<(int Codigo, string Salida, string Errores)> BlanquearVerificadoAsync(DirectoriosPrueba directorios, string[] argumentos)
    {
        AssertApuntaAlTemporal(directorios, argumentos);

        var salida = new StringWriter();
        var errores = new StringWriter();
        var codigo = await ComandoBlanqueo.EjecutarAsync(argumentos, salida, errores);
        return (codigo, salida.ToString(), errores.ToString());
    }

    /// <summary>Falla, sin ejecutar el blanqueo, si las rutas que usaría el comando no están en el temporal del test.</summary>
    private static void AssertApuntaAlTemporal(DirectoriosPrueba directorios, string[] argumentos)
    {
        var opciones = ComandoBlanqueo.ResolverOpciones(argumentos);
        foreach (var (nombre, ruta) in new[] { ("RutaBase", opciones.RutaBase), ("DirectorioAuditoria", opciones.DirectorioAuditoria) })
        {
            var relativa = Path.GetRelativePath(directorios.Raiz, ruta);
            Assert.False(
                relativa == ".." || relativa.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relativa),
                $"El comando resolvió Optica:{nombre} = {ruta}, fuera del temporal del test ({directorios.Raiz}). No se ejecuta el blanqueo.");
        }
    }

    private static async Task CrearContrasenaAsync(FabricaApi fabrica, string contrasena)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<CrearContrasenaInicial>().EjecutarAsync(contrasena);
    }

    private static async Task<EstadoAcceso> EstadoAsync(FabricaApi fabrica)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<ObtenerEstadoAcceso>().EjecutarAsync();
    }
}
