using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// En cada apertura de conexión activa las claves foráneas y fija el <c>busy_timeout</c> en
/// <see cref="RegistroInfraestructura.SegundosEsperaBaseOcupada"/>: una escritura que encuentra la base
/// ocupada espera en lugar de fallar en el acto (E2-4).
/// </summary>
public sealed class InterceptorConexionSqlite : DbConnectionInterceptor
{
    public const int MilisegundosBusyTimeout = RegistroInfraestructura.SegundosEsperaBaseOcupada * 1000;

    private static readonly string Pragmas =
        $"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = {MilisegundosBusyTimeout.ToString(CultureInfo.InvariantCulture)};";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = Pragmas;
        comando.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var comando = connection.CreateCommand();
        comando.CommandText = Pragmas;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}
