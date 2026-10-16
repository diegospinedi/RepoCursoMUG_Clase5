using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Optica.Infrastructure.Persistencia;

namespace Optica.Infrastructure.Tests;

public class RegistroYErroresTests
{
    [Fact]
    public async Task AplicarMigraciones_BaseNueva_CreaTablasYEsIdempotente()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "optica-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var servicios = new ServiceCollection().AddInfraestructura(directorio).BuildServiceProvider();
            await RegistroInfraestructura.AplicarMigracionesAsync(servicios);
            await RegistroInfraestructura.AplicarMigracionesAsync(servicios);

            await using var alcance = servicios.CreateAsyncScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<OpticaDbContext>();
            Assert.Empty(await contexto.Database.GetPendingMigrationsAsync());
            Assert.Single(await contexto.Database.GetAppliedMigrationsAsync());
            Assert.Equal(1, await contexto.Set<Optica.Domain.Acceso.CredencialAcceso>().CountAsync());
        }
        finally
        {
            using (var conexion = new SqliteConnection(RegistroInfraestructura.CadenaConexion(directorio)))
            {
                SqliteConnection.ClearPool(conexion);
            }

            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    public void EsBaseOcupada_SqliteBusyEnLaCadena_DevuelveTrue()
    {
        var ocupada = new SqliteException("database is locked", ErroresPersistencia.CodigoBaseOcupada);

        Assert.True(ErroresPersistencia.EsBaseOcupada(ocupada));
        Assert.True(ErroresPersistencia.EsBaseOcupada(new DbUpdateException("guardar", ocupada)));
        Assert.True(ErroresPersistencia.EsBaseOcupada(
            new InvalidOperationException("externa", new DbUpdateException("guardar", ocupada))));
    }

    [Fact]
    public void EsBaseOcupada_OtroError_DevuelveFalse()
    {
        Assert.False(ErroresPersistencia.EsBaseOcupada(null));
        Assert.False(ErroresPersistencia.EsBaseOcupada(new SqliteException("unable to open database file", 14)));
        Assert.False(ErroresPersistencia.EsBaseOcupada(new DbUpdateException("guardar", new SqliteException("constraint", 19))));
        Assert.False(ErroresPersistencia.EsBaseOcupada(new InvalidOperationException("otra")));
    }
}
