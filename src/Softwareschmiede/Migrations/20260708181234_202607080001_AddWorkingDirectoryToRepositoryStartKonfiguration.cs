using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202607080001_AddWorkingDirectoryToRepositoryStartKonfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkingDirectoryRelativePath",
                table: "RepositoryStartKonfigurationen",
                type: "TEXT",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkingDirectoryRelativePath",
                table: "RepositoryStartKonfigurationen");
        }
    }
}
