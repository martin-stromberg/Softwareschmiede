using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605161100_RemoveStartScriptArguments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartScriptArgumentsTemplate",
                table: "RepositoryStartKonfigurationen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartScriptArgumentsTemplate",
                table: "RepositoryStartKonfigurationen",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }
    }
}
