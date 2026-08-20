using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomAufgabeModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "AutonomAufgabeKonfigurationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjektBranchName = table.Column<string>(type: "TEXT", nullable: false),
                    InitialPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    PermissionsJsonPfad = table.Column<string>(type: "TEXT", nullable: false),
                    TokenBudget = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenBudgetErweitert = table.Column<int>(type: "INTEGER", nullable: true),
                    LaufzeitLimitMinuten = table.Column<int>(type: "INTEGER", nullable: false),
                    PersistenzModus = table.Column<string>(type: "TEXT", nullable: false),
                    SkillAutogeneration = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArbeitsverzeichnisPfad = table.Column<string>(type: "TEXT", nullable: false)
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
                    SkillName = table.Column<string>(type: "TEXT", nullable: false),
                    SkillVersion = table.Column<string>(type: "TEXT", nullable: false),
                    SkillContent = table.Column<string>(type: "TEXT", nullable: false),
                    SkillStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ErstellungsDatum = table.Column<long>(type: "INTEGER", nullable: false),
                    FreigabeDatum = table.Column<long>(type: "INTEGER", nullable: true)
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
                    AgentId = table.Column<string>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", nullable: false),
                    AgentScope = table.Column<string>(type: "TEXT", nullable: false),
                    AgentPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    AgentDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    AgentBranch = table.Column<string>(type: "TEXT", nullable: false),
                    AgentClone = table.Column<string>(type: "TEXT", nullable: false),
                    ErzeugungsDatum = table.Column<long>(type: "INTEGER", nullable: false),
                    AbschlussDatum = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillDefinitionen");

            migrationBuilder.DropTable(
                name: "UnteragentSpezifikationen");

            migrationBuilder.DropTable(
                name: "AutonomAufgabeKonfigurationen");

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
    }
}
