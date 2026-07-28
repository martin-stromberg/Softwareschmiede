using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202607270001_AddAlertReferenz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertReferenzen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 700, nullable: false),
                    AlertUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    RuleId = table.Column<string>(type: "TEXT", nullable: true),
                    ToolName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertReferenzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertReferenzen_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertReferenzen_AufgabeId",
                table: "AlertReferenzen",
                column: "AufgabeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertReferenzen_SourceKey",
                table: "AlertReferenzen",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertReferenzen");
        }
    }
}
