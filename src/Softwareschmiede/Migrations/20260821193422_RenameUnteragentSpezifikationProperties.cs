using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class RenameUnteragentSpezifikationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AgentScope",
                table: "UnteragentSpezifikationen",
                newName: "Scope");

            migrationBuilder.RenameColumn(
                name: "AgentPrompt",
                table: "UnteragentSpezifikationen",
                newName: "Prompt");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "UnteragentSpezifikationen",
                newName: "ExterneAgentId");

            migrationBuilder.RenameColumn(
                name: "AgentDirectory",
                table: "UnteragentSpezifikationen",
                newName: "VerzeichnisPfad");

            migrationBuilder.RenameColumn(
                name: "AgentClone",
                table: "UnteragentSpezifikationen",
                newName: "ClonePfad");

            migrationBuilder.RenameColumn(
                name: "AgentBranch",
                table: "UnteragentSpezifikationen",
                newName: "Branch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VerzeichnisPfad",
                table: "UnteragentSpezifikationen",
                newName: "AgentDirectory");

            migrationBuilder.RenameColumn(
                name: "Scope",
                table: "UnteragentSpezifikationen",
                newName: "AgentScope");

            migrationBuilder.RenameColumn(
                name: "Prompt",
                table: "UnteragentSpezifikationen",
                newName: "AgentPrompt");

            migrationBuilder.RenameColumn(
                name: "ExterneAgentId",
                table: "UnteragentSpezifikationen",
                newName: "AgentId");

            migrationBuilder.RenameColumn(
                name: "ClonePfad",
                table: "UnteragentSpezifikationen",
                newName: "AgentClone");

            migrationBuilder.RenameColumn(
                name: "Branch",
                table: "UnteragentSpezifikationen",
                newName: "AgentBranch");
        }
    }
}
