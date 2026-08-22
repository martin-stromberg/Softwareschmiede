using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class VerschiebeProjektleiterFelderZuAutonomKonfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Zielspalten auf AutonomAufgabeKonfigurationen zuerst anlegen, damit die anschließende
            // Datenübernahme (siehe unten) und der spätere Drop der Quellspalten auf Aufgaben in der
            // richtigen Reihenfolge laufen und keine Bestandsdaten verloren gehen (Upgrade-Safety).
            migrationBuilder.AddColumn<int>(
                name: "AktiveUnteragenten",
                table: "AutonomAufgabeKonfigurationen",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjektleiterAgentId",
                table: "AutonomAufgabeKonfigurationen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SessionPauseUtc",
                table: "AutonomAufgabeKonfigurationen",
                type: "INTEGER",
                nullable: true);

            // Bestandsdaten aus Aufgaben in die neuen Spalten auf AutonomAufgabeKonfigurationen übernehmen,
            // bevor die alten Spalten auf Aufgaben entfernt werden (1:1-Zuordnung über AufgabeId).
            migrationBuilder.Sql(
                """
                UPDATE AutonomAufgabeKonfigurationen
                SET ProjektleiterAgentId = (SELECT a.ProjektleiterAgentId FROM Aufgaben a WHERE a.Id = AutonomAufgabeKonfigurationen.AufgabeId),
                    SessionPauseUtc = (SELECT a.SessionPauseUtc FROM Aufgaben a WHERE a.Id = AutonomAufgabeKonfigurationen.AufgabeId),
                    AktiveUnteragenten = (SELECT a.AktiveUnteragenten FROM Aufgaben a WHERE a.Id = AutonomAufgabeKonfigurationen.AufgabeId);
                """);

            migrationBuilder.DropColumn(
                name: "AktiveUnteragenten",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "ProjektleiterAgentId",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "SessionPauseUtc",
                table: "Aufgaben");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AktiveUnteragenten",
                table: "Aufgaben",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjektleiterAgentId",
                table: "Aufgaben",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SessionPauseUtc",
                table: "Aufgaben",
                type: "INTEGER",
                nullable: true);

            // Bestandsdaten zurück auf Aufgaben übernehmen, bevor die Spalten auf
            // AutonomAufgabeKonfigurationen entfernt werden.
            migrationBuilder.Sql(
                """
                UPDATE Aufgaben
                SET ProjektleiterAgentId = (SELECT k.ProjektleiterAgentId FROM AutonomAufgabeKonfigurationen k WHERE k.AufgabeId = Aufgaben.Id),
                    SessionPauseUtc = (SELECT k.SessionPauseUtc FROM AutonomAufgabeKonfigurationen k WHERE k.AufgabeId = Aufgaben.Id),
                    AktiveUnteragenten = (SELECT k.AktiveUnteragenten FROM AutonomAufgabeKonfigurationen k WHERE k.AufgabeId = Aufgaben.Id)
                WHERE EXISTS (SELECT 1 FROM AutonomAufgabeKonfigurationen k WHERE k.AufgabeId = Aufgaben.Id);
                """);

            migrationBuilder.DropColumn(
                name: "AktiveUnteragenten",
                table: "AutonomAufgabeKonfigurationen");

            migrationBuilder.DropColumn(
                name: "ProjektleiterAgentId",
                table: "AutonomAufgabeKonfigurationen");

            migrationBuilder.DropColumn(
                name: "SessionPauseUtc",
                table: "AutonomAufgabeKonfigurationen");
        }
    }
}
