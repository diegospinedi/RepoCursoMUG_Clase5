using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AccesoYConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionNegocio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AlicuotaIva = table.Column<long>(type: "INTEGER", nullable: false),
                    CondicionFiscal = table.Column<string>(type: "TEXT", nullable: false),
                    TopeIdentificacion = table.Column<long>(type: "INTEGER", nullable: true),
                    MultiploRedondeo = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionNegocio", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionNegocio_AlicuotaIva", "\"AlicuotaIva\" BETWEEN 0 AND 1000000");
                    table.CheckConstraint("CK_ConfiguracionNegocio_CondicionFiscal", "\"CondicionFiscal\" IN ('ResponsableInscripto','Monotributo')");
                    table.CheckConstraint("CK_ConfiguracionNegocio_FilaUnica", "\"Id\" = 1");
                    table.CheckConstraint("CK_ConfiguracionNegocio_MultiploRedondeo", "\"MultiploRedondeo\" >= 100");
                    table.CheckConstraint("CK_ConfiguracionNegocio_TopeIdentificacion", "\"TopeIdentificacion\" IS NULL OR \"TopeIdentificacion\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Credencial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: true),
                    SelloSeguridad = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BloqueadoHasta = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credencial", x => x.Id);
                    table.CheckConstraint("CK_Credencial_FilaUnica", "\"Id\" = 1");
                    table.CheckConstraint("CK_Credencial_IntentosFallidos", "\"IntentosFallidos\" BETWEEN 0 AND 4");
                });

            // Fila semilla de la credencial: sin contraseña y con un sello aleatorio propio de cada base
            // (GUID en el formato de texto que usa EF Core para SQLite).
            migrationBuilder.Sql(
                """
                INSERT INTO "Credencial" ("Id", "Hash", "SelloSeguridad", "IntentosFallidos", "BloqueadoHasta")
                SELECT 1, NULL,
                       substr(h, 1, 8) || '-' || substr(h, 9, 4) || '-' || substr(h, 13, 4) || '-' ||
                       substr(h, 17, 4) || '-' || substr(h, 21, 12),
                       0, NULL
                FROM (SELECT hex(randomblob(16)) AS h);
                """);

            // Configuración inicial (FR-15): 21 % / Responsable Inscripto / sin tope / 0,01, escalados ×10.000 (ADR-001).
            migrationBuilder.Sql(
                """
                INSERT INTO "ConfiguracionNegocio" ("Id", "AlicuotaIva", "CondicionFiscal", "TopeIdentificacion", "MultiploRedondeo")
                VALUES (1, 210000, 'ResponsableInscripto', NULL, 100);
                """);

            // WAL queda grabado en el archivo. No se puede cambiar dentro de una transacción, por eso va
            // fuera de la de la migración. EF Core ya lo activa cuando crea la base, pero no cuando el
            // archivo existía antes de migrar.
            migrationBuilder.Sql("PRAGMA journal_mode = WAL;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionNegocio");

            migrationBuilder.DropTable(
                name: "Credencial");
        }
    }
}
