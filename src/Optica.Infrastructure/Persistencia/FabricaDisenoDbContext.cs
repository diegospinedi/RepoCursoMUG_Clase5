using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Fábrica para las herramientas de diseño (<c>dotnet ef</c>). Apunta a una base en el directorio
/// temporal del sistema, nunca dentro del árbol del repo: la base real la crea la aplicación en
/// <c>Optica:RutaBase</c>.
/// </summary>
public sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<OpticaDbContext>
{
    public OpticaDbContext CreateDbContext(string[] args)
    {
        var rutaBase = Path.Combine(Path.GetTempPath(), "optica-diseno");
        Directory.CreateDirectory(rutaBase);

        var opciones = new DbContextOptionsBuilder<OpticaDbContext>();
        RegistroInfraestructura.ConfigurarContexto(opciones, rutaBase);
        return new OpticaDbContext(opciones.Options);
    }
}
