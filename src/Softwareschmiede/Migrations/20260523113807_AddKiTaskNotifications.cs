using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddKiTaskNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenachrichtigungsAudioDateien",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OriginalDateiname = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroesseBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    Inhalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    HochgeladenAm = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungsAudioDateien", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenachrichtigungsDispatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EreignisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kanal = table.Column<string>(type: "TEXT", nullable: false),
                    Modus = table.Column<string>(type: "TEXT", nullable: false),
                    Entscheidung = table.Column<string>(type: "TEXT", nullable: false),
                    Grund = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ErstelltAm = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungsDispatchLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenachrichtigungsEinstellungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ToastModus = table.Column<string>(type: "TEXT", nullable: false),
                    TonModus = table.Column<string>(type: "TEXT", nullable: false),
                    AktualisiertAm = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungsEinstellungen", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungsAudioDateien_BenutzerId",
                table: "BenachrichtigungsAudioDateien",
                column: "BenutzerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungsDispatchLogs_AufgabeId",
                table: "BenachrichtigungsDispatchLogs",
                column: "AufgabeId");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungsDispatchLogs_EreignisId_BenutzerId_Kanal",
                table: "BenachrichtigungsDispatchLogs",
                columns: new[] { "EreignisId", "BenutzerId", "Kanal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungsDispatchLogs_ErstelltAm",
                table: "BenachrichtigungsDispatchLogs",
                column: "ErstelltAm");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungsEinstellungen_BenutzerId",
                table: "BenachrichtigungsEinstellungen",
                column: "BenutzerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenachrichtigungsAudioDateien");

            migrationBuilder.DropTable(
                name: "BenachrichtigungsDispatchLogs");

            migrationBuilder.DropTable(
                name: "BenachrichtigungsEinstellungen");
        }
    }
}
