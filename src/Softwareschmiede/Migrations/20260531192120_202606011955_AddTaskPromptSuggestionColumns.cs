using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606011955_AddTaskPromptSuggestionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VorschlagAusfuehrenAbUtc",
                table: "Aufgaben",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VorschlagPrompt",
                table: "Aufgaben",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VorschlagAusfuehrenAbUtc",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "VorschlagPrompt",
                table: "Aufgaben");
        }
    }
}
