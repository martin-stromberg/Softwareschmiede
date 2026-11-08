using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddZeitgesteuerterPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZeitgesteuertePrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptText = table.Column<string>(type: "TEXT", nullable: false),
                    ZielZeitpunktUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ErstelltAmUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    AktualisiertAmUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZeitgesteuertePrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZeitgesteuertePrompts_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZeitgesteuertePrompts_AufgabeId",
                table: "ZeitgesteuertePrompts",
                column: "AufgabeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZeitgesteuertePrompts");
        }
    }
}
