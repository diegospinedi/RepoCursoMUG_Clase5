using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Optica.Api.Configuracion;
using Optica.Application.Acceso;
using Optica.Application.Comun;
using Optica.Domain.Acceso;
using Optica.Domain.Configuracion;
using Optica.Infrastructure.Persistencia;

namespace Optica.Api.Tests;

public class SeguridadApiTests
{
    private const string TituloAntiforgery = "Token antiforgery inválido";

    // Endpoints anónimos permitidos (FR-01): ingreso, creación inicial y el fallback del SPA.
    private static readonly HashSet<(string Metodo, string Ruta)> ListaBlanca =
    [
        ("GET", "api/acceso/ingreso"),
        ("POST", "api/acceso/ingreso"),
        ("POST", "api/acceso/contrasena-inicial"),
    ];

    // Parámetros string sueltos de acciones ("Controlador.Accion(parametro)") revisados explícitamente. Vacía.
    private static readonly HashSet<string> ParametrosTextoPermitidos = [];

    private static readonly HashSet<(string Metodo, string Ruta)> EndpointsDocumentados =
    [
        ("GET", "api/acceso/ingreso"),
        ("POST", "api/acceso/contrasena-inicial"),
        ("POST", "api/acceso/ingreso"),
        ("POST", "api/acceso/salida"),
        ("PUT", "api/acceso/contrasena"),
        ("GET", "api/configuracion"),
        ("PUT", "api/configuracion"),
    ];

    // ---- FR-01 / AC-01 y AC-08 ----

    [Fact]
    public async Task Endpoints_SinSesion_Devuelven401_SalvoListaBlanca()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        var endpoints = Endpoints(fabrica);

        var api = endpoints.SelectMany(e => Metodos(e).Select(m => (Metodo: m, Ruta: e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => e.Ruta.StartsWith("api/", StringComparison.Ordinal) && !EsComodin(e.Ruta))
            .ToHashSet();
        Assert.Superset(EndpointsDocumentados, api);

        var revisados = 0;
        foreach (var endpoint in endpoints)
        {
            var ruta = endpoint.RoutePattern.RawText!.TrimStart('/');
            var url = UrlDe(endpoint.RoutePattern);
            foreach (var metodo in Metodos(endpoint))
            {
                using var pedido = new HttpRequestMessage(new HttpMethod(metodo), url);
                if (metodo is "POST" or "PUT")
                {
                    pedido.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }

                using var respuesta = await cliente.Http.SendAsync(pedido);
                revisados++;

                var esFallbackSpa = EsComodin(ruta) && !ruta.StartsWith("api", StringComparison.Ordinal);
                if (ListaBlanca.Contains((metodo, ruta)) || esFallbackSpa)
                {
                    Assert.True(
                        respuesta.StatusCode != HttpStatusCode.Unauthorized,
                        $"{metodo} /{url} está en la lista blanca y respondió 401");
                }
                else
                {
                    Assert.True(
                        respuesta.StatusCode == HttpStatusCode.Unauthorized,
                        $"{metodo} /{url} respondió {(int)respuesta.StatusCode} sin sesión; se esperaba 401");
                    Assert.Null(respuesta.Headers.Location);
                }
            }
        }

        Assert.True(revisados >= EndpointsDocumentados.Count);

        // El fallback del SPA sirve la pantalla (sin datos) a cualquiera; una ruta /api desconocida no.
        using var pantalla = await cliente.Http.GetAsync("ingreso");
        Assert.Equal(HttpStatusCode.OK, pantalla.StatusCode);
        Assert.Equal(DirectoriosPrueba.ContenidoIndex, await pantalla.Content.ReadAsStringAsync());
        using var apiDesconocida = await cliente.Http.GetAsync("api/clientes");
        Assert.Equal(HttpStatusCode.Unauthorized, apiDesconocida.StatusCode);
    }

    [Fact]
    public async Task Endpoints_NingunoPermiteBlanquear()
    {
        await using var fabrica = new FabricaApi();
        var endpoints = Endpoints(fabrica);

        Assert.DoesNotContain(endpoints, e => e.RoutePattern.RawText!.Contains("blanque", StringComparison.OrdinalIgnoreCase));

        // Bajo /api/acceso solo existen los 5 endpoints documentados.
        var acceso = endpoints
            .SelectMany(e => Metodos(e).Select(m => (Metodo: m, Ruta: e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => e.Ruta.StartsWith("api/acceso", StringComparison.Ordinal))
            .ToHashSet();
        Assert.Equal(EndpointsDocumentados.Where(e => e.Ruta.StartsWith("api/acceso", StringComparison.Ordinal)).ToHashSet(), acceso);

        // Ninguna acción depende del blanqueo ni del repositorio de la credencial; los casos de uso que escriben
        // la credencial solo los usan las 3 acciones de acceso documentadas.
        var escritoras = new Dictionary<(string, string), Type>
        {
            [("POST", "api/acceso/contrasena-inicial")] = typeof(CrearContrasenaInicial),
            [("POST", "api/acceso/ingreso")] = typeof(IniciarSesion),
            [("PUT", "api/acceso/contrasena")] = typeof(CambiarContrasena),
        };
        Type[] prohibidas = [typeof(BlanquearContrasena), typeof(IRepositorioCredencial), typeof(IHasherContrasena), typeof(IUnidadDeTrabajo)];
        var vistas = new HashSet<(string, string)>();
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } accion)
            {
                continue;
            }

            var dependencias = Dependencias(accion);
            Assert.Empty(dependencias.Intersect(prohibidas));
            foreach (var metodo in Metodos(endpoint))
            {
                var clave = (metodo, endpoint.RoutePattern.RawText!.TrimStart('/'));
                var usadas = dependencias.Intersect(escritoras.Values).ToArray();
                if (escritoras.TryGetValue(clave, out var esperada))
                {
                    Assert.Equal([esperada], usadas);
                    vistas.Add(clave);
                }
                else
                {
                    Assert.Empty(usadas);
                }
            }
        }

        Assert.Equal(escritoras.Keys.ToHashSet(), vistas);

        // Recorrido con sesión y token: ningún pedido deja el sistema sin contraseña.
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);
        foreach (var (metodo, ruta) in EndpointsDocumentados.OrderBy(e => e.Ruta == "api/acceso/salida"))
        {
            using var respuesta = await cliente.EnviarAsync(new HttpMethod(metodo), ruta, metodo == "GET" ? null : "{}");
        }

        await using var alcance = fabrica.Services.CreateAsyncScope();
        Assert.True((await alcance.ServiceProvider.GetRequiredService<ObtenerEstadoAcceso>().EjecutarAsync()).ContrasenaDefinida);
    }

    // ---- Antiforgery, host y tamaño ----

    [Fact]
    public async Task Post_SinTokenAntiforgery_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var anonimo = fabrica.CrearCliente();
        await anonimo.PrepararAntiforgeryAsync();

        // Creación inicial sin token: no se crea nada.
        using (var creacion = await anonimo.EnviarAsync(HttpMethod.Post, "api/acceso/contrasena-inicial", new { contrasena = AccesoApiTests.Contrasena }, conToken: false))
        {
            await AssertAntiforgeryAsync(creacion);
        }

        Assert.False((await anonimo.EstadoAsync()).GetProperty("contrasenaDefinida").GetBoolean());

        using var conSesion = await AccesoApiTests.ConSesionAsync(fabrica);

        // Ingreso sin token o con un token inválido: no se verifica la contraseña ni se cuenta el intento.
        using (var ingreso = await anonimo.EnviarAsync(HttpMethod.Post, "api/acceso/ingreso", new { contrasena = AccesoApiTests.Contrasena }, conToken: false))
        {
            await AssertAntiforgeryAsync(ingreso);
        }

        using (var pedido = new HttpRequestMessage(HttpMethod.Post, "api/acceso/ingreso") { Content = JsonContent.Create(new { contrasena = AccesoApiTests.ContrasenaErronea }) })
        {
            pedido.Headers.Add(ClienteApi.HeaderXsrf, "no-es-un-token");
            using var invalido = await anonimo.Http.SendAsync(pedido);
            await AssertAntiforgeryAsync(invalido);
        }

        Assert.Null(anonimo.ValorCookieSesion);

        // Configuración, cambio de contraseña y salida con sesión pero sin token (o con el token de otro navegador).
        using (var configuracion = await conSesion.EnviarAsync(HttpMethod.Put, "api/configuracion", new { alicuotaIva = 10.5m, condicionFiscal = "Monotributo", topeIdentificacion = (decimal?)null, multiploRedondeo = 50m }, conToken: false))
        {
            await AssertAntiforgeryAsync(configuracion);
        }

        using (var pedido = new HttpRequestMessage(HttpMethod.Put, "api/configuracion") { Content = JsonContent.Create(new { alicuotaIva = 10.5m, condicionFiscal = "Monotributo", topeIdentificacion = (decimal?)null, multiploRedondeo = 50m }) })
        {
            pedido.Headers.Add(ClienteApi.HeaderXsrf, anonimo.TokenXsrf);
            using var ajeno = await conSesion.Http.SendAsync(pedido);
            await AssertAntiforgeryAsync(ajeno);
        }

        using (var cambio = await conSesion.EnviarAsync(HttpMethod.Put, "api/acceso/contrasena", new { contrasenaActual = AccesoApiTests.Contrasena, contrasenaNueva = AccesoApiTests.OtraContrasena }, conToken: false))
        {
            await AssertAntiforgeryAsync(cambio);
        }

        using (var salida = await conSesion.EnviarAsync(HttpMethod.Post, "api/acceso/salida", null, conToken: false))
        {
            await AssertAntiforgeryAsync(salida);
        }

        // Nada cambió: la sesión sigue, la configuración es la inicial y la contraseña es la misma.
        using (var lectura = await conSesion.ObtenerConfiguracionAsync())
        {
            Assert.Equal(HttpStatusCode.OK, lectura.StatusCode);
            Assert.Equal(21m, (await lectura.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alicuotaIva").GetDecimal());
        }

        using (var intento = await anonimo.IngresarAsync(AccesoApiTests.ContrasenaErronea))
        {
            var problema = await Problemas.LeerAsync(intento, HttpStatusCode.Unauthorized);
            Assert.Equal(4, problema.GetProperty("intentosRestantes").GetInt32());
        }

        await anonimo.IniciarSesionAsync(AccesoApiTests.Contrasena);
    }

    [Fact]
    public async Task HostNoPermitido_Devuelve400()
    {
        await using var fabrica = new FabricaApi();
        using var atacante = fabrica.CrearCliente("http://atacante.example/");

        using (var estado = await atacante.ObtenerEstadoAsync())
        {
            Assert.Equal(HttpStatusCode.BadRequest, estado.StatusCode);
        }

        using (var creacion = await atacante.CrearContrasenaAsync(AccesoApiTests.Contrasena))
        {
            Assert.Equal(HttpStatusCode.BadRequest, creacion.StatusCode);
        }

        using (var conPuerto = fabrica.CrearCliente("http://atacante.example:5080/"))
        using (var estado = await conPuerto.ObtenerEstadoAsync())
        {
            Assert.Equal(HttpStatusCode.BadRequest, estado.StatusCode);
        }

        foreach (var permitido in new[] { "http://localhost:5080/", "http://127.0.0.1:5080/" })
        {
            using var cliente = fabrica.CrearCliente(permitido);
            Assert.False((await cliente.EstadoAsync()).GetProperty("contrasenaDefinida").GetBoolean());
        }
    }

    [Fact]
    public async Task CuerpoMayorA4KB_Devuelve413()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConContrasenaAsync(fabrica);
        await cliente.PrepararAntiforgeryAsync();
        var grande = JsonDeTamano(5 * 1024);

        // Con Content-Length.
        using (var conLargo = await cliente.EnviarAsync(HttpMethod.Post, "api/acceso/ingreso", grande))
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, conLargo.StatusCode);
        }

        // Sin Content-Length (el largo se descubre al leer).
        using (var pedido = new HttpRequestMessage(HttpMethod.Post, "api/acceso/ingreso"))
        {
            pedido.Content = new StreamContent(new FlujoSinLargo(Encoding.UTF8.GetBytes(grande)));
            pedido.Content.Headers.ContentType = new("application/json");
            pedido.Headers.Add(ClienteApi.HeaderXsrf, cliente.TokenXsrf);
            using var sinLargo = await cliente.Http.SendAsync(pedido);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, sinLargo.StatusCode);
        }

        // Exactamente 4 KB no se rechaza por tamaño (la contraseña es demasiado larga: 400).
        using (var justo = await cliente.EnviarAsync(HttpMethod.Post, "api/acceso/ingreso", JsonDeTamano(4 * 1024)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, justo.StatusCode);
        }

        // Contraseñas de más de 128 caracteres: 400 por campo, sin contar como intento de ingreso.
        using (var ingresoLargo = await cliente.IngresarAsync(new string('x', 129)))
        {
            var errores = await Problemas.ErroresAsync(ingresoLargo);
            Assert.Equal(["La contraseña puede tener como máximo 128 caracteres"], errores["contrasena"]);
        }

        using (var creacionLarga = await cliente.CrearContrasenaAsync(new string('x', 129)))
        {
            var errores = await Problemas.ErroresAsync(creacionLarga);
            Assert.Equal(["La contraseña puede tener como máximo 128 caracteres"], errores["contrasena"]);
        }

        using (var fallo = await cliente.IngresarAsync(AccesoApiTests.ContrasenaErronea))
        {
            Assert.Equal(4, (await Problemas.LeerAsync(fallo, HttpStatusCode.Unauthorized)).GetProperty("intentosRestantes").GetInt32());
        }

        // También en /api/configuracion.
        await cliente.IniciarSesionAsync(AccesoApiTests.Contrasena);
        using var configuracion = await cliente.ActualizarConfiguracionAsync(JsonDeTamano(5 * 1024));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, configuracion.StatusCode);
    }

    // ---- Errores de infraestructura ----

    [Fact]
    public async Task BaseOcupada_Devuelve503()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);
        var cuerpo = new { alicuotaIva = 10.5m, condicionFiscal = "Monotributo", topeIdentificacion = (decimal?)null, multiploRedondeo = 50m };

        using (var otraConexion = fabrica.Directorios.AbrirConexionCruda())
        {
            using (var comando = otraConexion.CreateCommand())
            {
                comando.CommandText = "BEGIN IMMEDIATE;";
                comando.ExecuteNonQuery();
            }

            using var ocupada = await cliente.ActualizarConfiguracionAsync(cuerpo);
            var problema = await Problemas.LeerAsync(ocupada, HttpStatusCode.ServiceUnavailable);
            Assert.Equal("La base está ocupada, intentá de nuevo", problema.GetProperty("title").GetString());

            using var liberar = otraConexion.CreateCommand();
            liberar.CommandText = "ROLLBACK;";
            liberar.ExecuteNonQuery();
        }

        using var libre = await cliente.ActualizarConfiguracionAsync(cuerpo);
        Assert.Equal(HttpStatusCode.OK, libre.StatusCode);
    }

    [Fact]
    public async Task ErrorInesperado_Devuelve500SinStackTrace()
    {
        await using var fabrica = new FabricaApi(servicios: s => s.AddScoped<IRepositorioConfiguracion, RepositorioConfiguracionQueFalla>());
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);

        using var respuesta = await cliente.ObtenerConfiguracionAsync();

        var problema = await Problemas.LeerAsync(respuesta, HttpStatusCode.InternalServerError);
        Assert.False(string.IsNullOrWhiteSpace(problema.GetProperty("title").GetString()));
        var cuerpo = problema.GetRawText();
        Assert.DoesNotContain(RepositorioConfiguracionQueFalla.Mensaje, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", cuerpo, StringComparison.Ordinal);
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.False(problema.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task Respuestas_IncluyenEncabezadosDeSeguridad()
    {
        await using var fabrica = new FabricaApi();
        using var anonimo = fabrica.CrearCliente();
        using var conSesion = await AccesoApiTests.ConSesionAsync(fabrica);

        var respuestas = new List<HttpResponseMessage>
        {
            await anonimo.ObtenerEstadoAsync(),
            await anonimo.ObtenerConfiguracionAsync(),
            await anonimo.Http.GetAsync("/"),
            await anonimo.Http.GetAsync("configuracion"),
            await conSesion.ObtenerConfiguracionAsync(),
            await conSesion.ActualizarConfiguracionAsync("""{ "alicuotaIva": 101, "condicionFiscal": "Monotributo", "topeIdentificacion": null, "multiploRedondeo": 50 }"""),
        };
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.BadRequest],
            respuestas.Select(r => r.StatusCode).ToArray());

        foreach (var respuesta in respuestas)
        {
            var ruta = respuesta.RequestMessage!.RequestUri!.AbsolutePath;
            Assert.Equal("DENY", Encabezado(respuesta, "X-Frame-Options", ruta));
            Assert.Equal("nosniff", Encabezado(respuesta, "X-Content-Type-Options", ruta));
            Assert.Equal("no-referrer", Encabezado(respuesta, "Referrer-Policy", ruta));
            Assert.Equal(
                "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'",
                Encabezado(respuesta, "Content-Security-Policy", ruta));
            respuesta.Dispose();
        }
    }

    // ---- AC-11: la contraseña no aparece en logs ni en configuración ----

    [Fact]
    public async Task Logs_NoContienenLaContrasena()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        var secretos = new List<string> { AccesoApiTests.Contrasena, AccesoApiTests.OtraContrasena, AccesoApiTests.ContrasenaErronea };

        using (var creacion = await cliente.CrearContrasenaAsync(AccesoApiTests.Contrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, creacion.StatusCode);
        }

        secretos.AddRange(SecretosDeLaCredencial(fabrica));
        using (var fallo = await cliente.IngresarAsync(AccesoApiTests.ContrasenaErronea))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
        }

        await cliente.IniciarSesionAsync(AccesoApiTests.Contrasena);
        secretos.Add(cliente.ValorCookieSesion!);
        secretos.AddRange(SecretosDeLaCredencial(fabrica));
        using (var configuracion = await cliente.ActualizarConfiguracionAsync(new { alicuotaIva = 10.5m, condicionFiscal = "Monotributo", topeIdentificacion = (decimal?)null, multiploRedondeo = 50m }))
        {
            Assert.Equal(HttpStatusCode.OK, configuracion.StatusCode);
        }

        using (var cambio = await cliente.CambiarContrasenaAsync(AccesoApiTests.Contrasena, AccesoApiTests.OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        }

        secretos.Add(cliente.ValorCookieSesion!);
        secretos.AddRange(SecretosDeLaCredencial(fabrica));
        using (var cambioFallido = await cliente.CambiarContrasenaAsync(AccesoApiTests.ContrasenaErronea, "1234567"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, cambioFallido.StatusCode);
        }

        using (var salida = await cliente.SalirAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, salida.StatusCode);
        }

        await cliente.PrepararAntiforgeryAsync();
        await cliente.IniciarSesionAsync(AccesoApiTests.OtraContrasena);
        secretos.Add(cliente.ValorCookieSesion!);

        var logs = fabrica.Logs.Entradas;
        Assert.NotEmpty(logs);
        foreach (var secreto in secretos.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            var conSecreto = logs.FirstOrDefault(e => e.Texto.Contains(secreto, StringComparison.OrdinalIgnoreCase));
            Assert.True(conSecreto is null, $"Un log contiene un dato sensible: {conSecreto}");
        }

        // Los eventos de acceso y configuración sí quedan registrados.
        foreach (var evento in new[] { "ContrasenaCreada", "IngresoFallido", "IngresoExitoso", "ConfiguracionActualizada", "ContrasenaCambiada" })
        {
            Assert.Contains(logs, e => e.Evento.Name == evento);
        }

        // Nunca se habilita el logging de datos sensibles de EF ni el logging HTTP.
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var opciones = alcance.ServiceProvider.GetRequiredService<DbContextOptions<OpticaDbContext>>().FindExtension<CoreOptionsExtension>();
        Assert.False(opciones?.IsSensitiveDataLoggingEnabled ?? false);
        Assert.False(opciones?.DetailedErrorsEnabled ?? false);
        Assert.DoesNotContain(fabrica.TiposRegistrados, t => t.FullName?.Contains("HttpLogging", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Appsettings_NoContienenLaContrasena()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = await AccesoApiTests.ConSesionAsync(fabrica);
        using (var cambio = await cliente.CambiarContrasenaAsync(AccesoApiTests.Contrasena, AccesoApiTests.OtraContrasena))
        {
            Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        }

        var raizContenido = fabrica.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var configuraciones = Directory.GetFiles(raizContenido, "appsettings*.json")
            .Concat(Directory.GetFiles(Path.Combine(raizContenido, "Properties"), "*.json"))
            .ToArray();
        Assert.Contains(configuraciones, a => Path.GetFileName(a) == "appsettings.json");
        Assert.Contains(configuraciones, a => Path.GetFileName(a) == "appsettings.Development.json");

        var otrosArchivos = new[] { fabrica.Directorios.Claves, fabrica.Directorios.Auditoria }
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*", SearchOption.AllDirectories));
        foreach (var archivo in configuraciones.Concat(otrosArchivos))
        {
            var contenido = File.ReadAllBytes(archivo);
            foreach (var contrasena in new[] { AccesoApiTests.Contrasena, AccesoApiTests.OtraContrasena })
            {
                Assert.False(
                    contenido.AsSpan().IndexOf(Encoding.UTF8.GetBytes(contrasena)) >= 0,
                    $"{archivo} contiene la contraseña en texto plano");
            }
        }

        // Ninguna clave de configuración guarda contraseñas.
        foreach (var archivo in configuraciones)
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(archivo), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var claves = Claves(documento.RootElement).ToArray();
            Assert.DoesNotContain(claves, c =>
                c.Contains("contrasena", StringComparison.OrdinalIgnoreCase)
                || c.Contains("contraseña", StringComparison.OrdinalIgnoreCase)
                || c.Contains("password", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---- E4-11 ----

    [Fact]
    public async Task Arranque_RutaBaseDentroDelRepo_NoArranca()
    {
        var raiz = Repo.Raiz();
        var dentro = Path.Combine(raiz, "tests", "Optica.Api.Tests", "bin", $"base-dentro-del-repo-{Guid.NewGuid():N}");
        using var fuera = new DirectoriosPrueba();

        ResultadoProceso resultado;
        bool seCreoElDirectorio;
        try
        {
            resultado = await ProcesoApi.EjecutarAsync(
                TimeSpan.FromSeconds(30),
                puerto: null,
                $"--Optica:RutaBase={dentro}",
                $"--Optica:DirectorioClaves={fuera.Claves}",
                $"--Optica:DirectorioAuditoria={fuera.Auditoria}",
                "--Urls=http://127.0.0.1:0");
        }
        finally
        {
            seCreoElDirectorio = Directory.Exists(dentro);
            if (seCreoElDirectorio)
            {
                Directory.Delete(dentro, recursive: true);
            }
        }

        Assert.True(resultado.Termino, "El servidor arrancó con Optica:RutaBase dentro del repo");
        Assert.NotEqual(0, resultado.Codigo);
        Assert.Contains("Optica:RutaBase", resultado.Salida, StringComparison.Ordinal);
        Assert.False(seCreoElDirectorio, "Se creó el directorio de la base dentro del repo antes de validar la ruta");

        // Lo mismo vale para las claves de Data Protection y la auditoría.
        foreach (var clave in new[] { "DirectorioClaves", "DirectorioAuditoria" })
        {
            var valores = new Dictionary<string, string?>
            {
                ["Optica:RutaBase"] = fuera.Datos,
                ["Optica:DirectorioClaves"] = fuera.Claves,
                ["Optica:DirectorioAuditoria"] = fuera.Auditoria,
                [$"Optica:{clave}"] = Path.Combine(raiz, "datos-locales"),
            };
            var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
            var error = Assert.Throws<OpcionesOpticaInvalidas>(() => OpcionesOptica.Cargar(configuracion));
            Assert.Contains($"Optica:{clave}", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Arranque_FueraDeLoopback_NoQuedaEscuchando()
    {
        using var directorios = new DirectoriosPrueba();
        var puerto = ProcesoApi.PuertoLibre();

        var resultado = await ProcesoApi.EjecutarAsync(
            TimeSpan.FromSeconds(30),
            puerto,
            $"--Optica:RutaBase={directorios.Datos}",
            $"--Optica:DirectorioClaves={directorios.Claves}",
            $"--Optica:DirectorioAuditoria={directorios.Auditoria}",
            $"--Urls=http://0.0.0.0:{puerto}");

        Assert.True(resultado.Termino, "El servidor siguió corriendo escuchando en 0.0.0.0");
        Assert.NotEqual(0, resultado.Codigo);
        Assert.False(resultado.EscuchabaAlFinal, "El puerto quedó escuchando");
        Assert.Contains("loopback", resultado.Salida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dtos_ToStringNoMuestraTextos()
    {
        // MVC loguea los argumentos de las acciones en nivel Trace con ToString(). Ningún cuerpo de pedido puede
        // mostrar sus textos (contraseñas hoy; datos personales de clientes después).
        const string centinela = "centinela-secreto-4b1d";
        var controladores = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .ToArray();
        Assert.NotEmpty(controladores);

        var parametros = controladores
            .SelectMany(c => c.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && !m.IsDefined(typeof(Microsoft.AspNetCore.Mvc.NonActionAttribute), inherit: true))
            .SelectMany(m => m.GetParameters())
            .Where(p => !p.IsDefined(typeof(Microsoft.AspNetCore.Mvc.FromServicesAttribute), inherit: true))
            .ToArray();

        // Un string suelto (ruta o query, por ejemplo {dni}) también se loguea tal cual. Cada uno tiene que estar
        // en la lista blanca, agregado a conciencia después de decidir cómo evitar que el dato aparezca en logs.
        var sueltos = parametros
            .Where(p => p.ParameterType == typeof(string))
            .Select(p => $"{p.Member.DeclaringType!.Name}.{p.Member.Name}({p.Name})")
            .Where(nombre => !ParametrosTextoPermitidos.Contains(nombre))
            .ToArray();
        Assert.True(
            sueltos.Length == 0,
            $"Parámetros string sueltos que no están en la lista blanca: {string.Join(", ", sueltos)}");

        var tipos = parametros
            .Select(p => p.ParameterType)
            .Where(t => t.IsClass && t != typeof(string))
            .Distinct()
            .ToArray();

        var conTextos = 0;
        foreach (var tipo in tipos)
        {
            var (instancia, textos) = InstanciarConCentinela(tipo, centinela);
            conTextos += textos > 0 ? 1 : 0;
            var texto = instancia.ToString() ?? string.Empty;
            Assert.False(
                texto.Contains(centinela, StringComparison.Ordinal),
                $"{tipo.Name}.ToString() muestra el contenido de sus textos: {texto}");
        }

        // No es vacuo: hoy hay al menos dos cuerpos con contraseñas.
        Assert.True(conTextos >= 2, $"Se esperaban al menos 2 cuerpos con textos y se encontraron {conTextos}");
    }

    // ---- Ayudas ----

    private static async Task AssertAntiforgeryAsync(HttpResponseMessage respuesta)
    {
        var problema = await Problemas.LeerAsync(respuesta, HttpStatusCode.BadRequest);
        Assert.Equal(TituloAntiforgery, problema.GetProperty("title").GetString());
    }

    private static List<RouteEndpoint> Endpoints(FabricaApi fabrica) =>
        fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    private static IEnumerable<string> Metodos(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } metodos ? metodos : ["GET"];

    private static bool EsComodin(string ruta) => ruta.Contains("{*", StringComparison.Ordinal);

    private static string UrlDe(RoutePattern patron) => string.Join(
        '/',
        patron.PathSegments.Select(segmento => string.Concat(segmento.Parts.Select(parte => parte switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separador => separador.Content,
            _ => "x",
        }))));

    private static HashSet<Type> Dependencias(ControllerActionDescriptor accion)
    {
        var tipos = accion.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Services)
            .Select(p => p.ParameterType)
            .ToHashSet();
        foreach (var constructor in accion.ControllerTypeInfo.GetConstructors())
        {
            tipos.UnionWith(constructor.GetParameters().Select(p => p.ParameterType));
        }

        return tipos;
    }

    private static string? Encabezado(HttpResponseMessage respuesta, string nombre, string ruta)
    {
        Assert.True(respuesta.Headers.TryGetValues(nombre, out var valores), $"{ruta}: falta el encabezado {nombre}");
        return Assert.Single(valores);
    }

    private static (object Instancia, int Textos) InstanciarConCentinela(Type tipo, string centinela)
    {
        var constructor = tipo.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var parametros = constructor.GetParameters();
        var argumentos = parametros
            .Select(p => p.ParameterType == typeof(string) ? centinela
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();
        var instancia = constructor.Invoke(argumentos);
        var textos = parametros.Count(p => p.ParameterType == typeof(string));
        foreach (var propiedad in tipo.GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite))
        {
            propiedad.SetValue(instancia, centinela);
            textos++;
        }

        return (instancia, textos);
    }

    private static string JsonDeTamano(int bytes)
    {
        const string prefijo = "{\"contrasena\":\"";
        const string sufijo = "\"}";
        return prefijo + new string('a', bytes - prefijo.Length - sufijo.Length) + sufijo;
    }

    private static IEnumerable<string> SecretosDeLaCredencial(FabricaApi fabrica)
    {
        using var conexion = fabrica.Directorios.AbrirConexionCruda();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Hash, SelloSeguridad FROM Credencial WHERE Id = 1";
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        Assert.False(lector.IsDBNull(0), "La credencial no tiene hash: no se creó la contraseña");
        var hash = lector.GetString(0);
        var sello = Guid.Parse(lector.GetString(1));
        var partes = hash.Split('$');

        return [hash, partes[^2], partes[^1], sello.ToString("D"), sello.ToString("N")];
    }

    private static IEnumerable<string> Claves(JsonElement elemento)
    {
        if (elemento.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var propiedad in elemento.EnumerateObject())
        {
            yield return propiedad.Name;
            foreach (var anidada in Claves(propiedad.Value))
            {
                yield return anidada;
            }
        }
    }

    private sealed class FlujoSinLargo(byte[] contenido) : MemoryStream(contenido)
    {
        public override bool CanSeek => false;
    }
}

/// <summary>Repositorio que falla siempre con un error no previsto (para el 500).</summary>
public sealed class RepositorioConfiguracionQueFalla : IRepositorioConfiguracion
{
    public const string Mensaje = "falla-simulada-7f3a";

    public Task<ConfiguracionNegocio> Obtener() => throw new InvalidOperationException(Mensaje);

    public Task Guardar(ConfiguracionNegocio configuracion) => throw new InvalidOperationException(Mensaje);
}
