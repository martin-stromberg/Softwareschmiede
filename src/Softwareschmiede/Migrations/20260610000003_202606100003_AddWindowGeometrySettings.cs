using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606100003_AddWindowGeometrySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fenstergeometrie-Einstellungen werden als Schlüssel-Wert-Paare in AppEinstellungen gespeichert.
            // EF Core verwaltet diese als Daten, nicht als Schema – keine Spaltenänderungen nötig.
            // Die tatsächlichen Werte werden beim ersten App-Start durch AppEinstellungService erzeugt.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM AppEinstellungen WHERE Schluessel IN ('WindowPosition.X', 'WindowPosition.Y', 'WindowPosition.Width', 'WindowPosition.Height', 'DarkModeEnabled')");
        }
    }
}
