using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <summary>
    /// Entfernt die Tabellen <c>AutonomAufgabeKonfigurationen</c>, <c>UnteragentSpezifikationen</c> und
    /// <c>SkillDefinitionen</c>. Zugehörige Aufgaben bleiben als reguläre Aufgaben mit zurückgesetztem
    /// Laufzustand erhalten (abgeschlossene Aufgaben behalten ihren <c>AusfuehrungsStatus</c>); die Einstellung <c>autonomeaufgaben.enabled</c> wird gelöscht.
    /// <c>Down()</c> stellt nur das Schema wieder her, nicht die Daten.
    /// </summary>
    public partial class RemoveAutonomAufgabeModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Aufgaben
                SET AusfuehrungsStatus = CASE
                        WHEN Status IN ('Beendet', 'Archiviert') THEN AusfuehrungsStatus
                        ELSE 'NichtGestartet'
                    END,
                    AktiveRunId = NULL,
                    PausiertBisUtc = NULL,
                    LastHeartbeatUtc = NULL,
                    LetzterCliStartUtc = NULL,
                    LaufStatus = NULL
                WHERE Id IN (SELECT AufgabeId FROM AutonomAufgabeKonfigurationen);
                """);

            migrationBuilder.Sql("""
                DELETE FROM AppEinstellungen WHERE Schluessel = 'autonomeaufgaben.enabled';
                """);

            migrationBuilder.DropTable(
                name: "SkillDefinitionen");

            migrationBuilder.DropTable(
                name: "UnteragentSpezifikationen");

            migrationBuilder.DropTable(
                name: "AutonomAufgabeKonfigurationen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutonomAufgabeKonfigurationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AktiveUnteragenten = table.Column<int>(type: "INTEGER", nullable: true),
                    ArbeitsverzeichnisPfad = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ExplizitGestoppt = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitialPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    LaufzeitLimitMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionsJsonPfad = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PersistenzModus = table.Column<string>(type: "TEXT", nullable: false),
                    ProjektBranchName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ProjektleiterAgentId = table.Column<string>(type: "TEXT", nullable: true),
                    SessionPauseUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    SkillAutogeneration = table.Column<bool>(type: "INTEGER", nullable: false),
                    TokenBudget = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenBudgetErweitert = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomAufgabeKonfigurationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutonomAufgabeKonfigurationen_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillDefinitionen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutonomAufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ErstellungsDatum = table.Column<long>(type: "INTEGER", nullable: false),
                    FreigabeDatum = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillDefinitionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillDefinitionen_AutonomAufgabeKonfigurationen_AutonomAufgabeId",
                        column: x => x.AutonomAufgabeId,
                        principalTable: "AutonomAufgabeKonfigurationen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnteragentSpezifikationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutonomAufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AbschlussDatum = table.Column<long>(type: "INTEGER", nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ClonePfad = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ErzeugungsDatum = table.Column<long>(type: "INTEGER", nullable: false),
                    ExterneAgentId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    VerzeichnisPfad = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnteragentSpezifikationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnteragentSpezifikationen_AutonomAufgabeKonfigurationen_AutonomAufgabeId",
                        column: x => x.AutonomAufgabeId,
                        principalTable: "AutonomAufgabeKonfigurationen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomAufgabeKonfigurationen_AufgabeId",
                table: "AutonomAufgabeKonfigurationen",
                column: "AufgabeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillDefinitionen_AutonomAufgabeId",
                table: "SkillDefinitionen",
                column: "AutonomAufgabeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnteragentSpezifikationen_AutonomAufgabeId",
                table: "UnteragentSpezifikationen",
                column: "AutonomAufgabeId");
        }
    }
}
