using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Optica.Application.Comun;
using Optica.Domain.Acceso;
using Optica.Domain.Configuracion;
using Optica.Infrastructure.Persistencia;

namespace Optica.Infrastructure.Tests;

public class RepositoriosTests
{
    private const int CodigoRestriccionSqlite = 19; // SQLITE_CONSTRAINT

    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    // ---- Migración ----

    [Fact]
    public async Task Migracion_CreaTablasYSiembraFilas()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();
        using var conexion = baseTemporal.AbrirConexionCruda();

        Assert.Equal(1L, conexion.Escalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Credencial'"));
        Assert.Equal(1L, conexion.Escalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ConfiguracionNegocio'"));
        Assert.Equal(1L, conexion.Escalar("SELECT COUNT(*) FROM Credencial"));
        Assert.Equal(1L, conexion.Escalar("SELECT COUNT(*) FROM ConfiguracionNegocio"));
        Assert.Equal(
            1L,
            conexion.Escalar("SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%\\_AccesoYConfiguracion' ESCAPE '\\'"));

        // Fila semilla de la credencial: sin contraseña, sin fallos, sin bloqueo y con un sello válido.
        await using var alcance = baseTemporal.CrearAlcance();
        var credencial = await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>().Obtener();
        Assert.False(credencial.ContrasenaDefinida);
        Assert.Equal(0, credencial.IntentosFallidos);
        Assert.Null(credencial.BloqueadoHasta);
        Assert.NotEqual(Guid.Empty, credencial.SelloSeguridad);
    }

    [Fact]
    public async Task Migracion_SiembraConfiguracionInicial()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        using (var conexion = baseTemporal.AbrirConexionCruda())
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "SELECT AlicuotaIva, CondicionFiscal, TopeIdentificacion, MultiploRedondeo FROM ConfiguracionNegocio WHERE Id = 1";
            using var lector = comando.ExecuteReader();
            Assert.True(lector.Read(), "La migración no sembró la fila de configuración");
            Assert.Equal(210_000L, lector.GetInt64(0)); // 21 × 10.000 (ADR-001)
            Assert.Equal("ResponsableInscripto", lector.GetString(1));
            Assert.True(lector.IsDBNull(2));
            Assert.Equal(100L, lector.GetInt64(3)); // 0,01 × 10.000
        }

        await using var alcance = baseTemporal.CrearAlcance();
        var configuracion = await alcance.ServiceProvider.GetRequiredService<IRepositorioConfiguracion>().Obtener();
        Assert.Equal(21m, configuracion.AlicuotaIva);
        Assert.Equal(CondicionFiscal.ResponsableInscripto, configuracion.CondicionFiscal);
        Assert.Null(configuracion.TopeIdentificacion);
        Assert.Equal(0.01m, configuracion.MultiploRedondeo);
    }

    [Fact]
    public async Task Migracion_ActivaWalYForeignKeys()
    {
        // EF Core ya crea en WAL las bases que crea él; la migración tiene que activarlo también
        // cuando el archivo existía antes de migrar.
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync(archivoPreexistente: true);
        Assert.True(File.Exists(baseTemporal.RutaArchivo));
        await using var alcance = baseTemporal.CrearAlcance();
        var contexto = alcance.ServiceProvider.GetRequiredService<OpticaDbContext>();

        await contexto.Database.OpenConnectionAsync();
        var conexion = (SqliteConnection)contexto.Database.GetDbConnection();

        Assert.Equal("wal", conexion.Escalar("PRAGMA journal_mode"));
        Assert.Equal(1L, conexion.Escalar("PRAGMA foreign_keys"));
    }

    // ---- Credencial ----

    [Fact]
    public async Task CrearCondicional_SinHash_DefineLaContrasena()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();
        var sello = Guid.NewGuid();

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var creada = await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>()
                .CrearSiNoExiste("PBKDF2-SHA256$600000$sal$hash", sello);
            Assert.True(creada);
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var credencial = await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>().Obtener();
            Assert.Equal("PBKDF2-SHA256$600000$sal$hash", credencial.Hash);
            Assert.Equal(sello, credencial.SelloSeguridad);
        }
    }

    [Fact]
    public async Task CrearCondicional_ConHashExistente_NoModificaFilas()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();
        var selloOriginal = Guid.NewGuid();

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>();
            Assert.True(await repositorio.CrearSiNoExiste("hash-original", selloOriginal));
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>();
            Assert.False(await repositorio.CrearSiNoExiste("hash-intruso", Guid.NewGuid()));
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var credencial = await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>().Obtener();
            Assert.Equal("hash-original", credencial.Hash);
            Assert.Equal(selloOriginal, credencial.SelloSeguridad);
        }
    }

    [Fact]
    public async Task Credencial_SegundaFila_ViolaCheck()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();
        using var conexion = baseTemporal.AbrirConexionCruda();

        var segundaFila = Assert.Throws<SqliteException>(() => conexion.Ejecutar(
            "INSERT INTO Credencial (Id, Hash, SelloSeguridad, IntentosFallidos, BloqueadoHasta) " +
            "VALUES (2, 'hash-de-otra-credencial', '6F9619FF-8B86-D011-B42D-00C04FC964FF', 0, NULL)"));
        Assert.Equal(CodigoRestriccionSqlite, segundaFila.SqliteErrorCode);
        Assert.Contains("CHECK", segundaFila.Message, StringComparison.OrdinalIgnoreCase);

        var intentosDeMas = Assert.Throws<SqliteException>(() => conexion.Ejecutar(
            "UPDATE Credencial SET IntentosFallidos = 5 WHERE Id = 1"));
        Assert.Equal(CodigoRestriccionSqlite, intentosDeMas.SqliteErrorCode);

        Assert.Equal(1L, conexion.Escalar("SELECT COUNT(*) FROM Credencial"));
    }

    // ---- Configuración ----

    [Fact]
    public async Task Configuracion_ValorFueraDeCheck_LanzaDbUpdateException()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            // El dominio no deja asignar un múltiplo inválido: se fuerza por EF para probar la defensa de la base.
            var contexto = alcance.ServiceProvider.GetRequiredService<OpticaDbContext>();
            var configuracion = await contexto.Set<ConfiguracionNegocio>().SingleOrDefaultAsync();
            Assert.NotNull(configuracion);
            contexto.Entry(configuracion).Property(c => c.MultiploRedondeo).CurrentValue = 0m;

            await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var guardada = await alcance.ServiceProvider.GetRequiredService<IRepositorioConfiguracion>().Obtener();
            Assert.Equal(0.01m, guardada.MultiploRedondeo);
        }
    }

    [Fact]
    public async Task RepositorioConfiguracion_GuardaYLee()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioConfiguracion>();
            var configuracion = await repositorio.Obtener();
            configuracion.Actualizar(10.5m, CondicionFiscal.Monotributo, 100000m, 50m);
            await repositorio.Guardar(configuracion);
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var leida = await alcance.ServiceProvider.GetRequiredService<IRepositorioConfiguracion>().Obtener();
            Assert.Equal(10.5m, leida.AlicuotaIva);
            Assert.Equal(CondicionFiscal.Monotributo, leida.CondicionFiscal);
            Assert.Equal(100000m, leida.TopeIdentificacion);
            Assert.Equal(50m, leida.MultiploRedondeo);
        }

        // Persistido como enteros escalados ×10.000 (ADR-001) y la condición como texto.
        using var conexion = baseTemporal.AbrirConexionCruda();
        Assert.Equal(105_000L, conexion.Escalar("SELECT AlicuotaIva FROM ConfiguracionNegocio"));
        Assert.Equal("Monotributo", conexion.Escalar("SELECT CondicionFiscal FROM ConfiguracionNegocio"));
        Assert.Equal(1_000_000_000L, conexion.Escalar("SELECT TopeIdentificacion FROM ConfiguracionNegocio"));
        Assert.Equal(500_000L, conexion.Escalar("SELECT MultiploRedondeo FROM ConfiguracionNegocio"));
    }

    // ---- Datos sensibles ----

    [Fact]
    public async Task Base_NoContieneLaContrasenaEnTextoPlano()
    {
        const string contrasena = "Secreta-Optica-7f3c9a2e";
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var servicios = alcance.ServiceProvider;
            var hash = servicios.GetRequiredService<IHasherContrasena>().Hashear(ContrasenaNueva.Crear(contrasena));
            Assert.True(await servicios.GetRequiredService<IRepositorioCredencial>().CrearSiNoExiste(hash, Guid.NewGuid()));

            // Un ingreso fallido y otro exitoso, para que la credencial se escriba varias veces.
            var repositorio = servicios.GetRequiredService<IRepositorioCredencial>();
            var credencial = await repositorio.Obtener();
            credencial.RegistrarFallo(Ahora);
            await repositorio.Guardar(credencial);
            credencial = await repositorio.Obtener();
            credencial.RegistrarIngresoExitoso();
            await repositorio.Guardar(credencial);
            Assert.StartsWith("PBKDF2-SHA256$600000$", (await repositorio.Obtener()).Hash, StringComparison.Ordinal);
        }

        // Se leen los archivos con la conexión del pool todavía abierta, para que el -wal y el -shm existan.
        var archivos = new[] { baseTemporal.RutaArchivo, baseTemporal.RutaArchivo + "-wal", baseTemporal.RutaArchivo + "-shm" }
            .Where(File.Exists)
            .ToArray();
        Assert.Contains(baseTemporal.RutaArchivo, archivos);
        Assert.Contains(baseTemporal.RutaArchivo + "-wal", archivos); // si no, lo escrito en WAL no se revisaría

        var buscados = new[] { Encoding.UTF8.GetBytes(contrasena), Encoding.Unicode.GetBytes(contrasena) };
        foreach (var archivo in archivos)
        {
            var contenido = LeerCompartido(archivo);
            foreach (var bytes in buscados)
            {
                Assert.True(
                    contenido.AsSpan().IndexOf(bytes) < 0,
                    $"La contraseña en texto plano aparece en {Path.GetFileName(archivo)}");
            }
        }
    }

    // ---- Unidad de trabajo ----

    [Fact]
    public async Task UnidadDeTrabajo_ExcepcionRevierte()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        // 1. Sin excepción, la unidad confirma: el bloqueo queda persistido (ISO 8601 UTC).
        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var servicios = alcance.ServiceProvider;
            var repositorio = servicios.GetRequiredService<IRepositorioCredencial>();
            await servicios.GetRequiredService<IUnidadDeTrabajo>().EjecutarAsync(async () =>
            {
                var credencial = await repositorio.Obtener();
                for (var i = 0; i < CredencialAcceso.MaximoIntentosFallidos; i++)
                {
                    credencial.RegistrarFallo(Ahora);
                }

                await repositorio.Guardar(credencial);
            });
        }

        using (var conexion = baseTemporal.AbrirConexionCruda())
        {
            Assert.Equal("2026-10-16T12:05:00.0000000Z", conexion.Escalar("SELECT BloqueadoHasta FROM Credencial"));
        }

        // 2. Si la operación lanza (acá, un CHECK violado), se revierte también lo que ya se había escrito.
        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var servicios = alcance.ServiceProvider;
            var repositorio = servicios.GetRequiredService<IRepositorioCredencial>();
            var contexto = servicios.GetRequiredService<OpticaDbContext>();

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                servicios.GetRequiredService<IUnidadDeTrabajo>().EjecutarAsync(async () =>
                {
                    var credencial = await repositorio.Obtener();
                    credencial.Blanquear();
                    await repositorio.Guardar(credencial);

                    var configuracion = await contexto.Set<ConfiguracionNegocio>().SingleAsync();
                    contexto.Entry(configuracion).Property(c => c.AlicuotaIva).CurrentValue = 101m;
                    await contexto.SaveChangesAsync();
                }));
        }

        await using (var alcance = baseTemporal.CrearAlcance())
        {
            var credencial = await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>().Obtener();
            Assert.Equal(Ahora.AddMinutes(5), credencial.BloqueadoHasta);
            Assert.Equal(TimeSpan.Zero, credencial.BloqueadoHasta!.Value.Offset);
        }
    }

    /// <summary>
    /// Riesgo de actualización perdida: una unidad de trabajo toma el lock de escritura al empezar
    /// (BEGIN IMMEDIATE), así que otra conexión que quiere escribir espera (busy_timeout) en lugar de
    /// intercalarse entre la lectura y la escritura de la primera.
    /// </summary>
    [Fact]
    public async Task ConexionOcupada_EsperaBusyTimeout()
    {
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();
        await using (var alcance = baseTemporal.CrearAlcance())
        {
            Assert.True(await alcance.ServiceProvider.GetRequiredService<IRepositorioCredencial>()
                .CrearSiNoExiste("hash-vigente", Guid.NewGuid()));

            var contexto = alcance.ServiceProvider.GetRequiredService<OpticaDbContext>();
            await contexto.Database.OpenConnectionAsync();
            Assert.Equal(5000L, ((SqliteConnection)contexto.Database.GetDbConnection()).Escalar("PRAGMA busy_timeout"));
        }

        await using var alcanceIngreso = baseTemporal.CrearAlcance();
        await using var alcanceBlanqueo = baseTemporal.CrearAlcance();
        var repositorioIngreso = alcanceIngreso.ServiceProvider.GetRequiredService<IRepositorioCredencial>();
        var repositorioBlanqueo = alcanceBlanqueo.ServiceProvider.GetRequiredService<IRepositorioCredencial>();

        var ingresoLeyo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarIngreso = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blanqueoEmpezo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intentosVistosPorElBlanqueo = -1;

        // Orden de los eventos: cada uno toma el siguiente número de la secuencia.
        var secuencia = 0;
        var ordenIngresoGuardo = 0;
        var ordenLiberacion = 0;
        var ordenInicioBlanqueo = 0;

        var ingreso = Task.CompletedTask;
        var blanqueo = Task.CompletedTask;
        try
        {
            // Ingreso: lee la credencial y "verifica el hash" (simulado con una espera) antes de guardar el fallo.
            ingreso = Task.Run(() => alcanceIngreso.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().EjecutarAsync(async () =>
            {
                var credencial = await repositorioIngreso.Obtener();
                ingresoLeyo.TrySetResult();
                await liberarIngreso.Task;
                credencial.RegistrarFallo(Ahora);
                await repositorioIngreso.Guardar(credencial);
                ordenIngresoGuardo = Interlocked.Increment(ref secuencia);
            }));
            await ingresoLeyo.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Blanqueo desde otra conexión (como el comando de otro proceso) mientras el ingreso sigue abierto.
            blanqueo = Task.Run(() => alcanceBlanqueo.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().EjecutarAsync(async () =>
            {
                ordenInicioBlanqueo = Interlocked.Increment(ref secuencia);
                blanqueoEmpezo.TrySetResult();
                var credencial = await repositorioBlanqueo.Obtener();
                intentosVistosPorElBlanqueo = credencial.IntentosFallidos;
                credencial.Blanquear();
                await repositorioBlanqueo.Guardar(credencial);
            }));

            var primero = await Task.WhenAny(blanqueoEmpezo.Task, blanqueo, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.False(
                primero == blanqueoEmpezo.Task || primero == blanqueo,
                "La segunda transacción de escritura empezó mientras la primera tenía la credencial leída");

            ordenLiberacion = Interlocked.Increment(ref secuencia);
            liberarIngreso.TrySetResult();
            await ingreso.WaitAsync(TimeSpan.FromSeconds(10));
            await blanqueo.WaitAsync(TimeSpan.FromSeconds(10)); // esperó al lock (busy_timeout) y no falló
        }
        finally
        {
            // Si una aserción falla, el ingreso no puede quedar colgado con el lock tomado.
            liberarIngreso.TrySetResult();
            await Task.WhenAny(Task.WhenAll(ingreso, blanqueo), Task.Delay(TimeSpan.FromSeconds(15)));
        }

        // El blanqueo empezó recién después de que el ingreso guardó (y, por lo tanto, después de liberarlo).
        Assert.True(
            ordenLiberacion < ordenIngresoGuardo && ordenIngresoGuardo < ordenInicioBlanqueo,
            $"Orden inesperado: liberación={ordenLiberacion}, ingreso guardó={ordenIngresoGuardo}, blanqueo empezó={ordenInicioBlanqueo}");

        // El blanqueo leyó el fallo ya confirmado y su resultado no fue pisado por el ingreso.
        Assert.Equal(1, intentosVistosPorElBlanqueo);
        await using var alcanceFinal = baseTemporal.CrearAlcance();
        var final = await alcanceFinal.ServiceProvider.GetRequiredService<IRepositorioCredencial>().Obtener();
        Assert.False(final.ContrasenaDefinida);
        Assert.Equal(0, final.IntentosFallidos);
    }

    /// <summary>
    /// E2-4: si otra conexión retiene el lock de escritura más que el busy_timeout, la unidad de trabajo
    /// no espera indefinidamente: propaga <see cref="SqliteException"/> (SQLITE_BUSY) a los ~5 s.
    /// </summary>
    [Fact]
    public async Task ConexionOcupada_AgotaBusyTimeout_PropagaSqliteException()
    {
        const int codigoBaseOcupada = 5; // SQLITE_BUSY
        await using var baseTemporal = await BaseTemporal.CrearMigradaAsync();

        // Otra conexión (como otro proceso) toma el lock de escritura y no lo suelta.
        using var otraConexion = baseTemporal.AbrirConexionCruda();
        otraConexion.Ejecutar("BEGIN IMMEDIATE;");
        try
        {
            await using var alcance = baseTemporal.CrearAlcance();
            var unidad = alcance.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>();
            var operacionEjecutada = false;

            var reloj = Stopwatch.StartNew();
            var error = await Assert.ThrowsAsync<SqliteException>(() =>
                Task.Run(() => unidad.EjecutarAsync(() =>
                {
                    operacionEjecutada = true;
                    return Task.CompletedTask;
                })).WaitAsync(TimeSpan.FromSeconds(60)));
            reloj.Stop();

            Assert.Equal(codigoBaseOcupada, error.SqliteErrorCode);
            Assert.False(operacionEjecutada);
            Assert.InRange(reloj.Elapsed, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(12));
        }
        finally
        {
            otraConexion.Ejecutar("ROLLBACK;");
        }
    }

    private static byte[] LeerCompartido(string ruta)
    {
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }
}
