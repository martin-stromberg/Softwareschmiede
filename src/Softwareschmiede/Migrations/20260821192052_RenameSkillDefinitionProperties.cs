using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class RenameSkillDefinitionProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SkillVersion",
                table: "SkillDefinitionen",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "SkillStatus",
                table: "SkillDefinitionen",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "SkillName",
                table: "SkillDefinitionen",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "SkillContent",
                table: "SkillDefinitionen",
                newName: "Content");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Version",
                table: "SkillDefinitionen",
                newName: "SkillVersion");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "SkillDefinitionen",
                newName: "SkillStatus");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "SkillDefinitionen",
                newName: "SkillName");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "SkillDefinitionen",
                newName: "SkillContent");
        }
    }
}
