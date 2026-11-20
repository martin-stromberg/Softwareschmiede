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
    protected void Settings_ExecutionLimitGlobalAndPerPlugin_E2E(Window mainWindow)
    {
        const string aiPrefix = "Softwareschmiede.ClaudeCli";
        var settings = new SettingsView(mainWindow).ForceShow();
        try
        {
            // Globales Limit: Feld im Expander „Aufgaben ausführen“, leer = unbegrenzt.
            settings.ExecuteOpenWorkItems();
            Assert.Equal(string.Empty, settings.GetMaxParallelExecutions());
            settings.SetMaxParallelExecutions("1");

            // Plugin-Limit: hostseitiges Feld im KI-Plugin-Panel (Gruppe „Ausführungsbegrenzung“).
            settings.SwitchTab("Plugins");
            settings.SelectPluginEntry(aiPrefix);
            Assert.Equal(string.Empty, settings.GetPluginParallelLimit());
            settings.SetPluginParallelLimit("2");
            settings.SaveSettings();

            // Persistenz-Nachweis über UI-Neuladen und AppEinstellung-Schlüssel.
            var settingsReopened = settings.Menu.NavigateToSettings();
            settingsReopened.ExecuteOpenWorkItems();
            Assert.Equal("1", settingsReopened.GetMaxParallelExecutions());
            settingsReopened.SwitchTab("Plugins");
            settingsReopened.SelectPluginEntry(aiPrefix);
            Assert.Equal("2", settingsReopened.GetPluginParallelLimit());
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("1", db.AppSettings
                    .Where(e => e.Key == ScmEventExecutionLimitService.GlobalLimitKey)
                    .Select(e => e.Value).FirstOrDefault());
                Assert.Equal("2", db.AppSettings
                    .Where(e => e.Key == ScmEventExecutionLimitService.PluginLimitKey(aiPrefix))
                    .Select(e => e.Value).FirstOrDefault());
            }

            // Validierung: „0“ ist kein gültiges globales Limit → Fehler, nichts wird persistiert.
            settingsReopened.ExecuteOpenWorkItems();
            settingsReopened.SetMaxParallelExecutions("0");
            var error = settingsReopened.SaveSettingsExpectError();
            Assert.Contains("Limit paralleler CLI-Ausführungen", error);
            using (var db = OpenTestDbContext())
            {
                Assert.Equal("1", db.AppSettings
                    .Where(e => e.Key == ScmEventExecutionLimitService.GlobalLimitKey)
                    .Select(e => e.Value).FirstOrDefault());
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
                settings.ExecuteOpenWorkItems();
                settings.SetMaxParallelExecutions(string.Empty);
                settings.SwitchTab("Plugins");
                settings.SelectPluginEntry(aiPrefix);
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
                    db.AppSettings
                        .Where(e => e.Key == ScmEventExecutionLimitService.GlobalLimitKey
                            || e.Key == ScmEventExecutionLimitService.PluginLimitKey(aiPrefix))
                        .ToList()
                        .ForEach(e => e.Value = null);
                    db.SaveChanges();
                }
                catch { }
            }
        }
    }
}
