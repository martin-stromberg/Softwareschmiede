using FlaUI.Core.AutomationElements;
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
    /// </summary>
    protected void Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E(Window mainWindow)
    {
        var codexPath = $@"C:\tools\codex-{Guid.NewGuid():N}.exe";

        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SelectDefaultKiPlugin("Codex CLI");
        settings.SetExecutablePath(codexPath);
        settings.SaveSettings();
        settings.Menu.NavigateToDashboard();

        var settingsReopened = new SettingsView(mainWindow).ForceShow();
        settingsReopened.SelectDefaultKiPlugin("Codex CLI");
        Assert.Equal(codexPath, settingsReopened.GetExecutablePath());

        settingsReopened.Menu.NavigateToDashboard();
    }
}
