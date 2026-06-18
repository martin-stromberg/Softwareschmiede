using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606100002_UpdateBenachrichtigungsEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BenachrichtigungsModus: NurAufgabenseite → Banner, Global → Ton
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET ToastModus = 'Banner' WHERE ToastModus = 'NurAufgabenseite'");
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET ToastModus = 'Banner' WHERE ToastModus = 'Global'");
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET TonModus = 'Banner' WHERE TonModus = 'NurAufgabenseite'");
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET TonModus = 'Ton' WHERE TonModus = 'Global'");

            // BenachrichtigungsKanal in DispatchLogs: Toast → Banner
            migrationBuilder.Sql("UPDATE BenachrichtigungsDispatchLogs SET Kanal = 'Banner' WHERE Kanal = 'Toast'");

            // AppEinstellung-Schlüssel NotificationMode: Immer → Banner, Nie → Deaktiviert, NurBeiFehler → Banner
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Banner' WHERE Schluessel = 'NotificationMode' AND Wert = 'Immer'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Deaktiviert' WHERE Schluessel = 'NotificationMode' AND Wert = 'Nie'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Banner' WHERE Schluessel = 'NotificationMode' AND Wert = 'NurBeiFehler'");

            // AppEinstellung-Schlüssel NotificationChannel: Audio → Ton, System → Banner
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Ton' WHERE Schluessel = 'NotificationChannel' AND Wert = 'Audio'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Banner' WHERE Schluessel = 'NotificationChannel' AND Wert = 'System'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Banner → NurAufgabenseite (ToastModus), Ton → Global (TonModus)
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET ToastModus = 'NurAufgabenseite' WHERE ToastModus = 'Banner'");
            migrationBuilder.Sql("UPDATE BenachrichtigungsEinstellungen SET TonModus = 'Global' WHERE TonModus = 'Ton'");
            migrationBuilder.Sql("UPDATE BenachrichtigungsDispatchLogs SET Kanal = 'Toast' WHERE Kanal = 'Banner'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Immer' WHERE Schluessel = 'NotificationMode' AND Wert = 'Banner'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Nie' WHERE Schluessel = 'NotificationMode' AND Wert = 'Deaktiviert'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'Audio' WHERE Schluessel = 'NotificationChannel' AND Wert = 'Ton'");
            migrationBuilder.Sql("UPDATE AppEinstellungen SET Wert = 'System' WHERE Schluessel = 'NotificationChannel' AND Wert = 'Banner'");
        }
    }
}
