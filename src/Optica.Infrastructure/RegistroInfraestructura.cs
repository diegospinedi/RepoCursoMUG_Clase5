using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Optica.Application.Comun;
using Optica.Domain.Acceso;
using Optica.Domain.Configuracion;
using Optica.Infrastructure.Persistencia;
using Optica.Infrastructure.Seguridad;

namespace Optica.Infrastructure;

/// <summary>
/// Registra la persistencia SQLite, los repositorios, la unidad de trabajo y el hasher de contraseñas.
/// La base es el archivo <c>optica.db</c> dentro de <c>rutaBase</c>; validar que esa ruta quede fuera
/// del árbol de fuentes es responsabilidad del composition root (Block 4).
/// </summary>
public static class RegistroInfraestructura
{
    public const string NombreArchivoBase = "optica.db";

    /// <summary>
    /// Tiempo máximo que se espera a que se libere la base ocupada por otra conexión (E2-4). Es la única
    /// fuente del valor: de acá salen el <c>DefaultTimeout</c> de la cadena de conexión (reintentos de
    /// Microsoft.Data.Sqlite) y el <c>PRAGMA busy_timeout</c> de <see cref="InterceptorConexionSqlite"/>.
    /// </summary>
    public const int SegundosEsperaBaseOcupada = 5;

    public static IServiceCollection AddInfraestructura(this IServiceCollection servicios, string rutaBase)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaBase);

        Directory.CreateDirectory(Path.GetFullPath(rutaBase));

        servicios.AddDbContext<OpticaDbContext>(opciones => ConfigurarContexto(opciones, rutaBase));
        servicios.AddScoped<IRepositorioCredencial, RepositorioCredencial>();
        servicios.AddScoped<IRepositorioConfiguracion, RepositorioConfiguracion>();
        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        servicios.AddSingleton<IHasherContrasena, HasherPbkdf2>();

        return servicios;
    }

    /// <summary>Cadena de conexión al archivo <c>optica.db</c> dentro de <paramref name="rutaBase"/>.</summary>
    public static string CadenaConexion(string rutaBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaBase);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(Path.GetFullPath(rutaBase), NombreArchivoBase),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true,
            DefaultTimeout = SegundosEsperaBaseOcupada,
        }.ToString();
    }

    internal static void ConfigurarContexto(DbContextOptionsBuilder opciones, string rutaBase) =>
        opciones
            .UseSqlite(CadenaConexion(rutaBase))
            .AddInterceptors(new InterceptorConexionSqlite());
}
