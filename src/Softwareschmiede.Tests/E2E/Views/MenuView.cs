using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für das persistente Navigationsmenü der Anwendung (Dashboard/Projekte/Einstellungen).</summary>
public sealed class MenuView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public MenuView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("Dashboard"))
           && ElementExists(Window, cf => cf.ByName(" Projekte"))
           && ElementExists(Window, cf => cf.ByName(" Einstellungen"));

    /// <inheritdoc/>
    public override MenuView ForceShow() => this;

    /// <inheritdoc/>
    public override MenuView ForceClose(bool recurseToDashboard) => this;

    /// <summary>Klickt den "Dashboard"-Button und wartet auf den Dashboard-Seitentitel.</summary>
    public void NavigateToDashboard()
    {
        WaitForElement(Window, cf => cf.ByName("Dashboard"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Dashboard").And(cf.ByControlType(ControlType.Text)), Medium);
    }

    /// <summary>Klickt den "Projekte"-Button und wartet auf die Projektliste.</summary>
    public void NavigateToProjects()
    {
        WaitForElement(Window, cf => cf.ByName(" Projekte"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Neu"), Medium);
    }

    /// <summary>Klickt den "Einstellungen"-Button und wartet auf die Einstellungs-Tabs.</summary>
    public void NavigateToSettings()
    {
        WaitForElement(Window, cf => cf.ByName(" Einstellungen"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Plugins"), Medium);
    }
}
