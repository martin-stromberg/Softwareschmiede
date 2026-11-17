using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAufbereitung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Zweck",
                table: "ScmIncomingEvents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IssueAufbereitungAktiv",
                table: "GitRepositories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Zweck",
                table: "Aufgaben",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Zweck",
                table: "ScmIncomingEvents");

            migrationBuilder.DropColumn(
                name: "IssueAufbereitungAktiv",
                table: "GitRepositories");

            migrationBuilder.DropColumn(
                name: "Zweck",
                table: "Aufgaben");
        }
    }
}
