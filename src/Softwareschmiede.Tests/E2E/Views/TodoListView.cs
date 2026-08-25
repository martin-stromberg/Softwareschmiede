using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>
/// View für die To-Do-Listen-Unteransicht innerhalb der <see cref="TaskDetailView"/> (Reiter
/// "TodoViewButton"). Sichtbar ist sie nur, wenn zuvor bereits eine Aufgabe geöffnet wurde.
/// </summary>
public sealed class TodoListView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public TodoListView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible => ElementExists(Window, cf => cf.ByName("TodosList"));

    /// <inheritdoc/>
    public override TodoListView ForceShow()
    {
        if (IsVisible)
            return this;

        WaitForElement(Window, cf => cf.ByName("TodoViewButton"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("TodosList"), Medium);

        return this;
    }

    /// <inheritdoc/>
    public override TodoListView ForceClose(bool recurseToDashboard)
    {
        new TaskDetailView(Window).ForceClose(recurseToDashboard);
        return this;
    }
}
