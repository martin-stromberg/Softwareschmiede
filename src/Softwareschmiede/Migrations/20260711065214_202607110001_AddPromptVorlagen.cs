using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202607110001_AddPromptVorlagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptVorlagen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Prompttext = table.Column<string>(type: "TEXT", nullable: false),
                    Sortierung = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<long>(type: "INTEGER", nullable: false),
                    AktualisiertAm = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVorlagen", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVorlagen_Sortierung",
                table: "PromptVorlagen",
                column: "Sortierung");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptVorlagen");
        }
    }
}
