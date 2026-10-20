using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddAufgabeScmAbschluss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScmAbschlussErkanntUtc",
                table: "Aufgaben",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScmAbschlussReferenz",
                table: "Aufgaben",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScmAbschlussErkanntUtc",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "ScmAbschlussReferenz",
                table: "Aufgaben");
        }
    }
}
