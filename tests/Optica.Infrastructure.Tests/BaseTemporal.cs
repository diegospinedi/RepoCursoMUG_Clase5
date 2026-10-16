using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Optica.Infrastructure.Persistencia;

namespace Optica.Infrastructure.Tests;

/// <summary>
/// Base SQLite en un directorio temporal único (fuera del repo), armada con el mismo registro que usa
/// la aplicación. Al descartarla se cierran las conexiones del pool y se borra el directorio completo
/// (<c>optica.db</c>, <c>-wal</c> y <c>-shm</c>).
/// </summary>
public sealed class BaseTemporal : IAsyncDisposable
{
    private readonly ServiceProvider _servicios;

    private BaseTemporal()
    {
        Directorio = Path.Combine(Path.GetTempPath(), "optica-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Directorio);
        _servicios = new ServiceCollection().AddInfraestructura(Directorio).BuildServiceProvider();
    }

    public string Directorio { get; }

    public string RutaArchivo => Path.Combine(Directorio, "optica.db");

    /// <summary>
    /// Crea la base y le aplica las migraciones. Con <paramref name="archivoPreexistente"/> el archivo
    /// ya existe (vacío) antes de migrar, como cuando lo crea otra herramienta: EF Core no ejecuta
    /// entonces su propia creación de la base.
    /// </summary>
    public static async Task<BaseTemporal> CrearMigradaAsync(bool archivoPreexistente = false)
    {
        var baseTemporal = new BaseTemporal();
        if (archivoPreexistente)
        {
            using var conexion = baseTemporal.AbrirConexionCruda();
        }

        await using var alcance = baseTemporal.CrearAlcance();
        await alcance.ServiceProvider.GetRequiredService<OpticaDbContext>().Database.MigrateAsync();
        return baseTemporal;
    }

    /// <summary>Cada alcance tiene su propio <see cref="OpticaDbContext"/> y, por lo tanto, su propia conexión.</summary>
    public AsyncServiceScope CrearAlcance() => _servicios.CreateAsyncScope();

    /// <summary>Abre una conexión cruda (sin EF) para inspeccionar el esquema y los valores guardados.</summary>
    public SqliteConnection AbrirConexionCruda()
    {
        var conexion = new SqliteConnection(RegistroInfraestructura.CadenaConexion(Directorio));
        conexion.Open();
        return conexion;
    }

    public async ValueTask DisposeAsync()
    {
        await _servicios.DisposeAsync();
        using (var conexion = new SqliteConnection(RegistroInfraestructura.CadenaConexion(Directorio)))
        {
            SqliteConnection.ClearPool(conexion);
        }

        for (var intento = 0; Directory.Exists(Directorio); intento++)
        {
            try
            {
                Directory.Delete(Directorio, recursive: true);
            }
            catch (IOException) when (intento < 10)
            {
                await Task.Delay(100);
            }
        }
    }
}

internal static class ExtensionesSqlite
{
    public static object? Escalar(this SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }

    public static int Ejecutar(this SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteNonQuery();
    }
}
