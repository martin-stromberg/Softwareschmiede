using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddAufgabeAusfuehrungsStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AusfuehrungsStatus",
                table: "Aufgaben",
                type: "TEXT",
                nullable: false,
                defaultValue: "NichtGestartet");

            migrationBuilder.Sql("""
                UPDATE Aufgaben
                SET AusfuehrungsStatus = CASE
                    WHEN Status = 'Neu' THEN 'NichtGestartet'
                    WHEN Status IN ('Gestartet', 'Wartend')
                        AND AktiveRunId IS NOT NULL
                        AND length(trim(AktiveRunId)) > 0 THEN 'Aktiv'
                    WHEN Status IN ('Gestartet', 'Wartend') THEN 'Beendet'
                    WHEN Status IN ('Beendet', 'Archiviert') THEN 'Beendet'
                    ELSE 'NichtGestartet'
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AusfuehrungsStatus",
                table: "Aufgaben");
        }
    }
}
