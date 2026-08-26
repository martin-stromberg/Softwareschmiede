using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>
/// View für die Datei-Explorer-Unteransicht innerhalb der <see cref="TaskDetailView"/> (Reiter
/// "DateiViewButton"). Sichtbar ist sie nur, wenn zuvor bereits eine Aufgabe geöffnet wurde.
/// </summary>
public sealed class FileExplorerView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public FileExplorerView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("FileExplorerBaum"));

    /// <inheritdoc/>
    public override FileExplorerView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("DateiViewButton"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("FileExplorerBaum"), Medium);

        return this;
    }

    /// <inheritdoc/>
    public override FileExplorerView ForceClose(bool recurseToDashboard)
    {
        new TaskDetailView(Window).ForceClose(recurseToDashboard);
        return this;
    }

    /// <returns><c>true</c>, wenn alle Modus-Buttons (Standard/Vergleich/Aktualisieren/Öffnen) sichtbar sind.</returns>
    public bool HasModeButtons()
        => ElementExists(Window, cf => cf.ByName("DateiStandard"))
           && ElementExists(Window, cf => cf.ByName("DateiVergleich"))
           && ElementExists(Window, cf => cf.ByName("DateiAktualisieren"))
           && ElementExists(Window, cf => cf.ByName("DateiOeffnen"));

    /// <returns><c>true</c>, wenn die Ribbon-Gruppe "Dateien" (Standard-Button) sichtbar ist.</returns>
    public bool HasFileRibbonGroup() => ElementExists(Window, cf => cf.ByName("DateiStandard"));

    /// <summary>Klappt den Baumknoten mit dem angegebenen Namen auf.</summary>
    /// <param name="nodeName">Der Automation-Name des Baumknotens.</param>
    /// <returns>Diese Instanz.</returns>
    public FileExplorerView ExpandNode(string nodeName)
    {
        WaitForElement(Window, cf => cf.ByName(nodeName), Short).Patterns.ExpandCollapse.Pattern.Expand();
        return this;
    }

    /// <summary>Klappt den Baumknoten mit dem angegebenen Namen zu.</summary>
    /// <param name="nodeName">Der Automation-Name des Baumknotens.</param>
    /// <returns>Diese Instanz.</returns>
    public FileExplorerView CollapseNode(string nodeName)
    {
        WaitForElement(Window, cf => cf.ByName(nodeName), Short).Patterns.ExpandCollapse.Pattern.Collapse();
        return this;
    }

    /// <param name="nodeName">Der zu prüfende Automation-Name.</param>
    /// <returns><c>true</c>, wenn ein Element mit diesem Namen aktuell sichtbar ist.</returns>
    public bool HasNode(string nodeName) => ElementExists(Window, cf => cf.ByName(nodeName));

    /// <summary>Wartet, bis ein Element mit dem angegebenen Namen sichtbar ist.</summary>
    /// <param name="nodeName">Der erwartete Automation-Name.</param>
    /// <returns>Diese Instanz.</returns>
    public FileExplorerView WaitForNode(string nodeName)
    {
        WaitForElement(Window, cf => cf.ByName(nodeName), Short);
        return this;
    }

    /// <summary>Wartet, bis ein Element mit dem angegebenen Namen verschwindet.</summary>
    /// <param name="nodeName">Der Automation-Name, dessen Verschwinden erwartet wird.</param>
    /// <returns>Diese Instanz.</returns>
    public FileExplorerView WaitUntilNodeGone(string nodeName)
    {
        WaitUntilGone(Window, cf => cf.ByName(nodeName), Short);
        return this;
    }
}
