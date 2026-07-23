using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das CommandLineParameters-Einstellungsfeld und den Hilfe-Button in der KI-Plugin-Konfiguration.
///
/// Konsolidierung (Issue #153): Alle drei Szenarien teilen exakt dieselbe Vorbedingung
/// (<see cref="OpenKiSettingsWithCodexCli"/>) und laufen deshalb als Phasen in einem App-Lifecycle.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_SettingsCommandLineParameters : WpfTestBase
{
    /// <summary>
    /// Szenario: Öffnet die KI-Einstellungen für Codex CLI und prüft, dass das CommandLineParameters-
    /// Feld angezeigt wird; speichert einen Wert und prüft, dass er nach erneutem Öffnen der
    /// Einstellungen erhalten geblieben ist; klickt anschließend den Hilfe-Button (?) und prüft, dass
    /// ein Dialog mit einem "Schließen"-Button erscheint, der den Dialog schließt.
    /// </summary>
    [Fact]
    public void CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        var expectedValue = $"--test-{Guid.NewGuid():N}";

        OpenKiSettingsWithCodexCli(mainWindow);

        var commandLineParametersBox = WaitForElement(mainWindow, cf => cf.ByName("CommandLineParameters"), Short);

        // Wert setzen, speichern, Seite verlassen und erneut betreten - Wert bleibt erhalten
        commandLineParametersBox.AsTextBox().Text = expectedValue;
        SaveSettings(mainWindow);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        OpenKiSettingsWithCodexCli(mainWindow);

        var reloadedBox = WaitForElement(mainWindow, cf => cf.ByName("CommandLineParameters"), Short);
        Assert.Equal(expectedValue, reloadedBox.AsTextBox().Text);

        // Hilfe-Button öffnet Dialog, der über "Schließen" wieder geschlossen werden kann
        var hilfeButton = WaitForElement(mainWindow, cf => cf.ByName("CliHilfeButton"), Short);
        hilfeButton.AsButton().Click();

        var hilfeDialog = WaitForWindow("Hilfe", Medium);

        var schliessenButton = WaitForElement(hilfeDialog, cf => cf.ByName("Schließen"), Short);
        schliessenButton.AsButton().Click();

        WaitUntilGone(Automation.GetDesktop(), cf => cf.ByName("Hilfe").And(cf.ByControlType(ControlType.Window)), Short);
    }

    private void OpenKiSettingsWithCodexCli(AutomationElement mainWindow)
    {
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), Short);
        einstellungenButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var pluginsTab = WaitForElement(mainWindow, cf => cf.ByName("Plugins"), Short);
        pluginsTab.Click();

        var kiPluginBox = WaitForElement(mainWindow, cf => cf.ByName("DefaultKiPlugin"), Short);
        SelectComboBoxItemByClick(kiPluginBox, "Codex CLI", Short);

        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var selected = kiPluginBox.AsComboBox().SelectedItem?.Name;
            if (string.Equals(selected, "Codex CLI", StringComparison.Ordinal)
                || string.Equals(selected, "Softwareschmiede.Infrastructure.Plugins.CodexPlugin", StringComparison.Ordinal))
                return;
            Thread.Sleep(200);
        }
    }

    private static void SaveSettings(AutomationElement mainWindow)
    {
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);
    }
}
