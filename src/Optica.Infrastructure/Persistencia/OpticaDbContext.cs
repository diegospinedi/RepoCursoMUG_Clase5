using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Optica.Domain.Acceso;
using Optica.Domain.Configuracion;

namespace Optica.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core sobre SQLite. Toda propiedad <see cref="decimal"/> se guarda como entero escalado
/// ×10.000 (ADR-001). Las restricciones CHECK repiten en la base las reglas de fila única y de rangos.
/// </summary>
public sealed class OpticaDbContext(DbContextOptions<OpticaDbContext> opciones) : DbContext(opciones)
{
    private const string FormatoFechaUtc = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    // Fechas como texto ISO 8601 en UTC, comparables como texto.
    private static readonly ValueConverter<DateTimeOffset, string> ConversorFechaUtc = new(
        fecha => fecha.UtcDateTime.ToString(FormatoFechaUtc, CultureInfo.InvariantCulture),
        texto => DateTimeOffset.ParseExact(
            texto,
            FormatoFechaUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveConversion<ConversorDecimalEscalado>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CredencialAcceso>(credencial =>
        {
            credencial.ToTable("Credencial", tabla =>
            {
                tabla.HasCheckConstraint("CK_Credencial_FilaUnica", "\"Id\" = 1");
                tabla.HasCheckConstraint("CK_Credencial_IntentosFallidos", "\"IntentosFallidos\" BETWEEN 0 AND 4");
            });
            credencial.HasKey(c => c.Id);
            credencial.Property(c => c.Id).ValueGeneratedNever();
            credencial.Property(c => c.Hash);
            credencial.Property(c => c.SelloSeguridad).IsRequired();
            credencial.Property(c => c.IntentosFallidos).IsRequired().HasDefaultValue(0);
            credencial.Property(c => c.BloqueadoHasta).HasConversion(ConversorFechaUtc);
            credencial.Ignore(c => c.ContrasenaDefinida);
        });

        modelBuilder.Entity<ConfiguracionNegocio>(configuracion =>
        {
            configuracion.ToTable("ConfiguracionNegocio", tabla =>
            {
                tabla.HasCheckConstraint("CK_ConfiguracionNegocio_FilaUnica", "\"Id\" = 1");
                tabla.HasCheckConstraint("CK_ConfiguracionNegocio_AlicuotaIva", "\"AlicuotaIva\" BETWEEN 0 AND 1000000");
                tabla.HasCheckConstraint(
                    "CK_ConfiguracionNegocio_CondicionFiscal",
                    "\"CondicionFiscal\" IN ('ResponsableInscripto','Monotributo')");
                tabla.HasCheckConstraint(
                    "CK_ConfiguracionNegocio_TopeIdentificacion",
                    "\"TopeIdentificacion\" IS NULL OR \"TopeIdentificacion\" >= 0");
                tabla.HasCheckConstraint("CK_ConfiguracionNegocio_MultiploRedondeo", "\"MultiploRedondeo\" >= 100");
            });
            configuracion.HasKey(c => c.Id);
            configuracion.Property(c => c.Id).ValueGeneratedNever();
            configuracion.Property(c => c.AlicuotaIva).IsRequired();
            configuracion.Property(c => c.CondicionFiscal).IsRequired().HasConversion<string>();
            configuracion.Property(c => c.TopeIdentificacion);
            configuracion.Property(c => c.MultiploRedondeo).IsRequired();
        });
    }
}
