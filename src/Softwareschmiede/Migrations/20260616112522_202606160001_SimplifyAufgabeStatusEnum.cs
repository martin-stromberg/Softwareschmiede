using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606160001_SimplifyAufgabeStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ArbeitsverzeichnisEingerichtet → Gestartet (Zwischenstatus entfernt)
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Gestartet' WHERE Status = 'ArbeitsverzeichnisEingerichtet'");

            // InArbeit → Gestartet (Zwischenstatus entfernt)
            migrationBuilder.Sql("UPDATE Aufgaben SET Status = 'Gestartet' WHERE Status = 'InArbeit'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keine Rückmigration möglich: ob eine Aufgabe vor der Migration ArbeitsverzeichnisEingerichtet,
            // Gestartet oder InArbeit war, ist nicht mehr unterscheidbar.
        }
    }
}
