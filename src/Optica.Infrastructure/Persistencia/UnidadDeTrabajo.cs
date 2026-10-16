using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Optica.Application.Comun;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Ejecuta una operación dentro de una transacción SQLite sobre la conexión del <see cref="OpticaDbContext"/>:
/// si la operación termina sin excepción, confirma; solo si lanza, revierte y propaga la excepción sin cambios.
/// <c>IniciarSesion</c> depende de esto: registra el fallo dentro de la unidad y lanza el rechazo afuera.
/// </summary>
/// <remarks>
/// <para>
/// La transacción se abre con <c>BEGIN IMMEDIATE</c>: toma el lock de escritura de la base <b>al empezar</b>,
/// antes de la primera lectura. Así se evita la actualización perdida sobre la credencial: un ingreso lee la
/// credencial, verifica el hash y guarda la entidad completa; si otra conexión (por ejemplo, el comando de
/// blanqueo desde otro proceso) quiere cambiar o blanquear la contraseña en ese intervalo, espera a que el
/// ingreso confirme y recién entonces lee el valor ya actualizado, en lugar de intercalarse y ser pisada.
/// </para>
/// <para>
/// La espera la acota el busy timeout de <see cref="RegistroInfraestructura.SegundosEsperaBaseOcupada"/> (5 s);
/// si se agota, se propaga la <see cref="SqliteException"/> (SQLITE_BUSY, E2-4).
/// </para>
/// <para>
/// Como el lock se toma al empezar, la regla general es que la operación sea breve. Hay una excepción aceptada
/// a propósito: <c>IniciarSesion</c> y <c>CambiarContrasena</c> verifican (y <c>CambiarContrasena</c> además
/// calcula) el hash PBKDF2 dentro de la unidad, porque la lectura, la verificación y el guardado tienen que ser
/// atómicos para no perder actualizaciones. El costo es que cada PBKDF2 (600.000 iteraciones, del orden de
/// cientos de milisegundos) retiene el lock de escritura ese tiempo; otro escritor espera, y como cada
/// operación queda muy por debajo de los 5 s del busy timeout, solo fallaría con varias escrituras encoladas.
/// En modo WAL las lecturas no se bloquean.
/// </para>
/// <para>
/// <c>BEGIN IMMEDIATE</c> se ejecuta con <see cref="SqliteConnection.BeginTransaction(bool)"/>, que es
/// sincrónico: si la base está ocupada, el hilo que llama queda bloqueado hasta 5 s esperando el lock.
/// </para>
/// <para>No admite unidades anidadas sobre el mismo contexto.</para>
/// </remarks>
public sealed class UnidadDeTrabajo(OpticaDbContext contexto) : IUnidadDeTrabajo
{
    public async Task EjecutarAsync(Func<Task> operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Ya hay una unidad de trabajo en curso sobre este contexto.");
        }

        await contexto.Database.OpenConnectionAsync();
        try
        {
            var conexion = (SqliteConnection)contexto.Database.GetDbConnection();

            // deferred: false → BEGIN IMMEDIATE (toma el lock de escritura ya). Llamada sincrónica.
            await using var transaccion = conexion.BeginTransaction(deferred: false);
            await contexto.Database.UseTransactionAsync(transaccion);
            try
            {
                await operacion();
                await transaccion.CommitAsync();
            }
            catch
            {
                await RevertirSinOcultarAsync(transaccion);
                throw;
            }
            finally
            {
                await contexto.Database.UseTransactionAsync(null);
            }
        }
        finally
        {
            await contexto.Database.CloseConnectionAsync();
        }
    }

    // Revierte y deja limpio el seguimiento de EF. Si el rollback mismo falla, se descarta ese error: la
    // transacción se deshace igual al cerrar la conexión y lo que se propaga es la excepción original.
    private async Task RevertirSinOcultarAsync(SqliteTransaction transaccion)
    {
        try
        {
            await transaccion.RollbackAsync();
        }
        catch (Exception)
        {
            // Intencional: no ocultar la excepción original de la operación.
        }
        finally
        {
            contexto.ChangeTracker.Clear();
        }
    }
}
