using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test fuer das Speichern des Standard-KI-Plugins und der plugin-spezifischen Codex-Einstellungen.
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_SettingsKiPluginPersistence : WpfTestBase
{
    /// <summary>
    /// Speichert Codex CLI als Standard-KI-Plugin mit ExecutablePath und prueft,
    /// dass beide Werte nach erneutem Oeffnen der Einstellungen erhalten bleiben.
    /// </summary>
    [Fact]
    public void Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E()
    {
        var app = LaunchApp();
        var mainWindow = app.GetMainWindow(Automation, Long)!;
        var codexPath = $@"C:\tools\codex-{Guid.NewGuid():N}.exe";

        OpenKiSettings(mainWindow);

        var kiPluginBox = SelectDefaultKiPlugin(mainWindow, "Codex CLI");
        WaitForSelectedCodexPlugin(kiPluginBox, Short);

        var executablePathBox = WaitForElement(mainWindow, cf => cf.ByName("ExecutablePath"), Short);
        executablePathBox.AsTextBox().Text = codexPath;

        SaveSettings(mainWindow);

        var dashboardButton = WaitForElement(mainWindow, cf => cf.ByName("Dashboard"), Short);
        dashboardButton.AsButton().Click();

        OpenKiSettings(mainWindow);

        var reloadedKiPluginBox = FindDefaultKiPluginComboBox(mainWindow);
        WaitForSelectedCodexPlugin(reloadedKiPluginBox, Short);

        var reloadedExecutablePathBox = WaitForElement(mainWindow, cf => cf.ByName("ExecutablePath"), Short);
        Assert.Equal(codexPath, reloadedExecutablePathBox.AsTextBox().Text);
    }

    private static void OpenKiSettings(AutomationElement mainWindow)
    {
        var einstellungenButton = WaitForElement(mainWindow, cf => cf.ByName(" Einstellungen"), Short);
        einstellungenButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);

        var kiTab = WaitForElement(mainWindow, cf => cf.ByName("KI"), Short);
        kiTab.Click();
    }

    private static AutomationElement SelectDefaultKiPlugin(AutomationElement mainWindow, string pluginName)
    {
        var comboBox = FindDefaultKiPluginComboBox(mainWindow);
        SelectComboBoxItemByClick(comboBox, pluginName, Short);
        return comboBox;
    }

    private static AutomationElement FindDefaultKiPluginComboBox(AutomationElement mainWindow)
        => WaitForElement(mainWindow, cf => cf.ByName("DefaultKiPlugin"), Short);

    private static void SaveSettings(AutomationElement mainWindow)
    {
        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);
    }

    private static void WaitForSelectedCodexPlugin(AutomationElement comboBoxElement, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? selectedItemName = null;
        while (DateTime.UtcNow < deadline)
        {
            selectedItemName = comboBoxElement.AsComboBox().SelectedItem?.Name;
            if (string.Equals(selectedItemName, "Codex CLI", StringComparison.Ordinal)
                || string.Equals(selectedItemName, "Softwareschmiede.Infrastructure.Plugins.CodexPlugin", StringComparison.Ordinal))
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"ComboBox zeigte nicht innerhalb von {timeout.TotalSeconds}s das Codex-Plugin. Aktuell: '{selectedItemName}'.");
    }
}
