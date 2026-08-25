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
}
