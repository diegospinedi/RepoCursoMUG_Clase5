using Microsoft.Data.Sqlite;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Clasificación de errores de persistencia para las capas de arriba, sin que tengan que conocer SQLite.
/// </summary>
public static class ErroresPersistencia
{
    /// <summary>SQLITE_BUSY: otra conexión retiene el lock y se agotó el busy timeout (E2-4).</summary>
    public const int CodigoBaseOcupada = 5;

    /// <summary>
    /// Indica si la excepción, o alguna de su cadena de <see cref="Exception.InnerException"/> (EF Core puede
    /// envolverla, por ejemplo en una <c>DbUpdateException</c>), es una <see cref="SqliteException"/> por base
    /// ocupada.
    /// </summary>
    public static bool EsBaseOcupada(Exception? excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            if (actual is SqliteException { SqliteErrorCode: CodigoBaseOcupada })
            {
                return true;
            }
        }

        return false;
    }
}
