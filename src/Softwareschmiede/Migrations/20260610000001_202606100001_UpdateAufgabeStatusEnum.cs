using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606100001_UpdateAufgabeStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Offen → Neu
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Neu' WHERE Status = 'Offen'");

            // InBearbeitung → ArbeitsverzeichnisEingerichtet (wenn BranchName null) oder Gestartet (wenn BranchName gesetzt)
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'ArbeitsverzeichnisEingerichtet' WHERE Status = 'InBearbeitung' AND (BranchName IS NULL OR BranchName = '')");
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Gestartet' WHERE Status = 'InBearbeitung' AND BranchName IS NOT NULL AND BranchName != ''");

            // KiAktiv → InArbeit
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'InArbeit' WHERE Status = 'KiAktiv'");

            // TestsLaufen → InArbeit
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'InArbeit' WHERE Status = 'TestsLaufen'");

            // Abgeschlossen → Beendet
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Beendet' WHERE Status = 'Abgeschlossen'");

            // Fehlgeschlagen → Beendet
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Beendet' WHERE Status = 'Fehlgeschlagen'");

            // Archiviert bleibt Archiviert
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Neu → Offen
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Offen' WHERE Status = 'Neu'");

            // ArbeitsverzeichnisEingerichtet → InBearbeitung
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'InBearbeitung' WHERE Status = 'ArbeitsverzeichnisEingerichtet'");

            // Gestartet → InBearbeitung
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'InBearbeitung' WHERE Status = 'Gestartet'");

            // InArbeit → KiAktiv
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'KiAktiv' WHERE Status = 'InArbeit'");

            // Wartend → InBearbeitung
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'InBearbeitung' WHERE Status = 'Wartend'");

            // Beendet → Abgeschlossen
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Abgeschlossen' WHERE Status = 'Beendet'");
        }
    }
}
