using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605230001_AddTaskRecoveryIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AktiveRunId",
                table: "Aufgaben",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastHeartbeatUtc",
                table: "Aufgaben",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryVersion",
                table: "Aufgaben",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AktiveRunId",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatUtc",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "RecoveryVersion",
                table: "Aufgaben");
        }
    }
}
