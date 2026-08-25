using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Einstellungen-Ansicht.</summary>
public sealed class SettingsView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public SettingsView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("Plugins"));

    /// <inheritdoc/>
    public override SettingsView ForceShow()
    {
        if (IsVisible)
            return this;

        Menu.NavigateToSettings();
        return this;
    }

    /// <inheritdoc/>
    public override SettingsView ForceClose(bool recurseToDashboard)
    {
        Menu.NavigateToDashboard();
        return this;
    }

    /// <returns>Der Name des aktuell aktiven Tabs.</returns>
    public string GetActiveTab()
    {
        var tabControl = WaitForElement(Window, cf => cf.ByControlType(ControlType.Tab), Short);
        return tabControl.AsTab().SelectedTabItem?.Name ?? string.Empty;
    }

    /// <param name="tabName">Der Name des zu aktivierenden Tabs.</param>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SwitchTab(string tabName)
    {
        WaitForElement(Window, cf => cf.ByName(tabName), Short).Click();
        return this;
    }

    /// <summary>Klickt den "Speichern"-Button und wartet auf die Bestätigung "Einstellungen gespeichert.".</summary>
    /// <returns>Diese Instanz.</returns>
    public SettingsView SaveSettings()
    {
        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Einstellungen gespeichert."), Short);

        return this;
    }
}
