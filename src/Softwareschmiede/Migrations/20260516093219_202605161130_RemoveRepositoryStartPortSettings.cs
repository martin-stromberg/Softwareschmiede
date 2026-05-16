using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605161130_RemoveRepositoryStartPortSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortBereichBis",
                table: "RepositoryStartKonfigurationen");

            migrationBuilder.DropColumn(
                name: "PortBereichVon",
                table: "RepositoryStartKonfigurationen");

            migrationBuilder.DropColumn(
                name: "PortModus",
                table: "RepositoryStartKonfigurationen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PortBereichBis",
                table: "RepositoryStartKonfigurationen",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PortBereichVon",
                table: "RepositoryStartKonfigurationen",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortModus",
                table: "RepositoryStartKonfigurationen",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
