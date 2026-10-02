using FlaUI.Core.AutomationElements;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test fuer das Speichern des Standard-KI-Plugins und der plugin-spezifischen Codex-Einstellungen.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Speichert Codex CLI als Standard-KI-Plugin mit ExecutablePath und prueft,
    /// dass beide Werte nach erneutem Oeffnen der Einstellungen erhalten bleiben.
    /// Der vorherige Default-Plugin-Wert wird gelesen und im finally-Block wiederhergestellt,
    /// damit nachfolgende Szenarien nicht ungewollt mit dem Codex-Default starten (dessen
    /// Executable im Direct-Start-Pfad von Issue #271 fehlen kann → NotFound-Fehler).
    /// </summary>
    protected void Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E(Window mainWindow)
    {
        var codexPath = $@"C:\tools\codex-{Guid.NewGuid():N}.exe";

        // Vorherigen Default-Plugin-Wert direkt aus der Test-DB lesen (Robustheit ohne UI-Feld).
        string? vorherigesDefault;
        using (var db = OpenTestDbContext())
        {
            vorherigesDefault = db.AppEinstellungen
                .Where(e => e.Schluessel == AppEinstellungService.DefaultKiPluginKey)
                .Select(e => e.Wert)
                .FirstOrDefault();
        }

        var settings = new SettingsView(mainWindow).ForceShow();
        // Das Codex-Einstellungspanel mit "ExecutablePath" existiert erst, nachdem Codex CLI als
        // Standard-KI-Plugin gewählt wurde (SelectedPluginSettings wird erst dann befüllt).
        settings.SelectDefaultKiPlugin("Codex CLI");
        var oldValue = settings.GetExecutablePath();
        try
        {
            settings.SetExecutablePath(codexPath);
            settings.SaveSettings();
            settings.Menu.NavigateToDashboard();

            var settingsReopened = new SettingsView(mainWindow).ForceShow();
            settingsReopened.SelectDefaultKiPlugin("Codex CLI");
            Assert.Equal(codexPath, settingsReopened.GetExecutablePath());

            settingsReopened.Menu.NavigateToDashboard();
        }
        finally
        {
            try
            {
                settings.ForceShow();
                settings.SelectDefaultKiPlugin("Codex CLI");
                settings.SetExecutablePath(oldValue);
                settings.SaveSettings();
                settings.Menu.NavigateToDashboard();
            }
            catch
            {
                // Cleanup-Fehler dürfen einen bestehenden Testfehler nicht maskieren.
            }

            // Vorherigen Default-Plugin-Wert direkt in der Test-DB wiederherstellen — die App
            // liest den Schlüssel pro Start frisch, kein UI-Reste-Effekt auf Folgeszenarien.
            try
            {
                using var db = OpenTestDbContext();
                var eintrag = db.AppEinstellungen
                    .FirstOrDefault(e => e.Schluessel == AppEinstellungService.DefaultKiPluginKey);
                if (eintrag is null && vorherigesDefault is not null)
                {
                    db.AppEinstellungen.Add(new Softwareschmiede.Domain.Entities.AppEinstellung
                    {
                        Id = Guid.NewGuid(),
                        Schluessel = AppEinstellungService.DefaultKiPluginKey,
                    });
                    eintrag = db.AppEinstellungen.Local.FirstOrDefault(e => e.Schluessel == AppEinstellungService.DefaultKiPluginKey);
                }
                if (eintrag is not null)
                {
                    eintrag.Wert = vorherigesDefault;
                    eintrag.AktualisiertAm = DateTimeOffset.UtcNow;
                    db.SaveChanges();
                }
            }
            catch
            {
                // Cleanup-Fehler dürfen einen bestehenden Testfehler nicht maskieren.
            }
        }
    }
}
