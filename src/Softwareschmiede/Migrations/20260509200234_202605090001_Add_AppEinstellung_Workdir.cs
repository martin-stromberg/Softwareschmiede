using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605090001_Add_AppEinstellung_Workdir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppEinstellungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Schluessel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Wert = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AktualisiertAm = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEinstellungen", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEinstellungen_Schluessel",
                table: "AppEinstellungen",
                column: "Schluessel",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEinstellungen");
        }
    }
}
