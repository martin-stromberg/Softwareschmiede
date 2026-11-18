using FlaUI.Core.AutomationElements;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Ausführungsbegrenzung (Issue 410, Schritt 2): Das globale Parallel-Limit
/// in „Automatisierung → Aufgaben ausführen“ und das Plugin-Limit im KI-Plugin-Einstellungspanel
/// werden über die UI gesetzt, validiert und persistent gespeichert.
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt den Einstellungs-Benutzerfluss für beide Limit-Arten konsolidiert durch:
    /// globales Feld setzen/validieren/persistieren, Plugin-Feld setzen/validieren/persistieren
    /// (jeweils inkl. Speichern-Neuladen-Zyklus und AppEinstellung-DB-Nachweis), danach
    /// Validierungs-Negativpfad. Im finally werden die Werte wieder entfernt, damit
    /// Folgeszenarien von unbegrenzten Limits ausgehen.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void Einstellungen_AusfuehrungsLimitsGlobalUndProPlugin_E2E(Window mainWindow)
    {
        const string kiPrefix = "Softwareschmiede.ClaudeCli";
        var settings = new SettingsView(mainWindow).ForceShow();
        try
        {
            // Globales Limit: Feld im Expander „Aufgaben ausführen“, leer = unbegrenzt.
            settings.OpenAufgabenAusfuehren();
            Assert.Equal(string.Empty, settings.GetMaxParalleleAusfuehrungen());
            settings.SetMaxParalleleAusfuehrungen("1");

            // Plugin-Limit: hostseitiges Feld im KI-Plugin-Panel (Gruppe „Ausführungsbegrenzung“).
            settings.SwitchTab("Plugins");
            settings.SelectPluginEntry(kiPrefix);
            Assert.Equal(string.Empty, settings.GetPluginParallelLimit());
            settings.SetPluginParallelLimit("2");
            settings.SaveSettings();

            // Persistenz-Nachweis über UI-Neuladen und AppEinstellung-Schlüssel.
            var settingsReopened = settings.Menu.NavigateToSettings();
            settingsReopened.OpenAufgabenAusfuehren();
            Assert.Equal("1", settingsReopened.GetMaxParalleleAusfuehrungen());
            settingsReopened.SwitchTab("Plugins");
            settingsReopened.SelectPluginEntry(kiPrefix);
            Assert.Equal("2", settingsReopened.GetPluginParallelLimit());
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("1", db.AppEinstellungen
                    .Where(e => e.Schluessel == ScmEventExecutionLimitService.GlobalLimitKey)
                    .Select(e => e.Wert).FirstOrDefault());
                Assert.Equal("2", db.AppEinstellungen
                    .Where(e => e.Schluessel == ScmEventExecutionLimitService.PluginLimitKey(kiPrefix))
                    .Select(e => e.Wert).FirstOrDefault());
            }

            // Validierung: „0“ ist kein gültiges globales Limit → Fehler, nichts wird persistiert.
            settingsReopened.OpenAufgabenAusfuehren();
            settingsReopened.SetMaxParalleleAusfuehrungen("0");
            var fehler = settingsReopened.SaveSettingsExpectError();
            Assert.Contains("Limit paralleler CLI-Ausführungen", fehler);
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("1", db.AppEinstellungen
                    .Where(e => e.Schluessel == ScmEventExecutionLimitService.GlobalLimitKey)
                    .Select(e => e.Wert).FirstOrDefault());
            }
            settingsReopened.DiscardChanges();
        }
        finally
        {
            // Ursprungszustand wiederherstellen: Beide Felder leeren (leer = unbegrenzt),
            // damit Folgeszenarien nicht unbeabsichtigt begrenzt laufen.
            try
            {
                settings.ForceShow();
                settings.OpenAufgabenAusfuehren();
                settings.SetMaxParalleleAusfuehrungen(string.Empty);
                settings.SwitchTab("Plugins");
                settings.SelectPluginEntry(kiPrefix);
                settings.SetPluginParallelLimit(string.Empty);
                settings.SaveSettings();
                settings.Menu.NavigateToDashboard();
            }
            catch
            {
                // Cleanup-Fehler dürfen einen bestehenden Testfehler nicht maskieren.
                try
                {
                    using var db = OpenTestDbContext();
                    db.AppEinstellungen
                        .Where(e => e.Schluessel == ScmEventExecutionLimitService.GlobalLimitKey
                            || e.Schluessel == ScmEventExecutionLimitService.PluginLimitKey(kiPrefix))
                        .ToList()
                        .ForEach(e => e.Wert = null);
                    db.SaveChanges();
                }
                catch { }
            }
        }
    }
}
