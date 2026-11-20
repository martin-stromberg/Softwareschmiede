using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das CommandLineParameters-Einstellungsfeld und den Hilfe-Button in der KI-Plugin-Konfiguration.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Öffnet die KI--Einstellungen für Codex CLI und prüft, dass das CommandLineParameters-
    /// Feld angezeigt wird; speichert einen Wert und prüft, dass er nach erneutem Öffnen der
    /// Settings erhalten geblieben ist; klickt anschließend den Hilfe-Button (?) und prüft, dass
    /// ein Dialog mit einem "Schließen"-Button erscheint, der den Dialog schließt.
    /// </summary>
    protected void CommandLineParameter_TextBoxSavesValueAndHelpDialogWorks_E2E(Window mainWindow)
    {
        var expectedValue = $"--test-{Guid.NewGuid():N}";

        var settings = new SettingsView(mainWindow).ForceShow();
        settings.SelectDefaultAiPlugin("Codex CLI");

        var oldValue = settings.GetCommandLineParameter();
        try
        {
            // Wert setzen, speichern, Seite verlassen und erneut betreten - Wert bleibt erhalten
            settings.SetCommandLineParameter(expectedValue);
            settings.SaveSettings();
            settings.Menu.NavigateToDashboard();

            var settingsReopened = new SettingsView(mainWindow).ForceShow();
            settingsReopened.SelectDefaultAiPlugin("Codex CLI");
            Assert.Equal(expectedValue, settingsReopened.GetCommandLineParameter());

            // Hilfe-Button öffnet Dialog, der über "Schließen" wieder geschlossen werden kann
            var helpDialog = settingsReopened.OpenCliHelp();
            Assert.True(helpDialog.IsVisible);
            helpDialog.Close();
        }
        finally
        {
            try
            {
                settings.ForceShow();
                settings.SetCommandLineParameter(oldValue);
                settings.SaveSettings();
                settings.Menu.NavigateToDashboard();
            }
            catch
            {
                // Cleanup-Fehler dürfen einen bestehenden Testfehler nicht maskieren.
            }
        }
    }
}
