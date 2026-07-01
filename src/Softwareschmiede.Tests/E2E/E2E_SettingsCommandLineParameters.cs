using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das CommandLineParameters-Einstellungsfeld und den Hilfe-Button in der KI-Plugin-Konfiguration.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_SettingsCommandLineParameters : WpfTestBase
{
    /// <summary>
    /// Öffnet die KI-Einstellungen für Codex CLI und prüft, dass das CommandLineParameters-Feld angezeigt wird.
    /// </summary>
    [Fact]
    public void Einstellungen_ZeigtCommandLineParametersTextBox_BeiCodexCliPlugin_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        OpenKiSettingsWithCodexCli(mainWindow);

        var commandLineParametersBox = WaitForElement(mainWindow, cf => cf.ByName("CommandLineParameters"), Short);
        Assert.NotNull(commandLineParametersBox);
    }

    /// <summary>
    /// Speichert einen CommandLineParameters-Wert für Codex CLI und prüft, dass er nach erneutem Öffnen
    /// der Einstellungen erhalten geblieben ist.
    /// </summary>
    [Fact]
    public void Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        var expectedValue = $"--test-{Guid.NewGuid():N}";

        OpenKiSettingsWithCodexCli(mainWindow);

        var commandLineParametersBox = WaitForElement(mainWindow, cf => cf.ByName("CommandLineParameters"), Short);
        commandLineParametersBox.AsTextBox().Text = expectedValue;

        SaveSettings(mainWindow);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        OpenKiSettingsWithCodexCli(mainWindow);

        var reloadedBox = WaitForElement(mainWindow, cf => cf.ByName("CommandLineParameters"), Short);
        Assert.Equal(expectedValue, reloadedBox.AsTextBox().Text);

        new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters");
    }

    /// <summary>
    /// Klickt den Hilfe-Button (?) bei CommandLineParameters und prüft, dass ein Dialog mit
    /// einem "Schließen"-Button erscheint, der den Dialog schließt.
    /// </summary>
    [Fact]
    public void Einstellungen_HilfeButton_OeffnetDialogDerMitSchliessen_GeschlossenWerdenKann_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        OpenKiSettingsWithCodexCli(mainWindow);

        var hilfeButton = WaitForElement(mainWindow, cf => cf.ByName("CliHilfeButton"), Short);
        hilfeButton.AsButton().Click();

        var hilfeDialog = WaitForWindow("Hilfe", Medium);
        Assert.NotNull(hilfeDialog);

        var schliessenButton = WaitForElement(hilfeDialog, cf => cf.ByName("Schließen"), Short);
        Assert.NotNull(schliessenButton);
        schliessenButton.AsButton().Click();

        WaitUntilGone(Automation.GetDesktop(), cf => cf.ByName("Hilfe").And(cf.ByControlType(ControlType.Window)), Short);
    }

    private void OpenKiSettingsWithCodexCli(AutomationElement mainWindow)
    {
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), Short);
        einstellungenButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var kiTab = WaitForElement(mainWindow, cf => cf.ByName("KI"), Short);
        kiTab.Click();

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
