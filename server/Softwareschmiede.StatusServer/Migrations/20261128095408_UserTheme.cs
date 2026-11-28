using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.StatusServer.Migrations;

/// <inheritdoc />
public partial class _20261128095408_UserTheme : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Theme",
            table: "AspNetUsers",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Theme",
            table: "AspNetUsers");
    }
}
