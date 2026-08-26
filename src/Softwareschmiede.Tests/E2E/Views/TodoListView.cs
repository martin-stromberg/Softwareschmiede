using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

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

    /// <returns><c>true</c>, wenn das Badge mit der Anzahl offener To-Dos sichtbar ist.</returns>
    public bool HasOpenCountBadge() => ElementExists(Window, cf => cf.ByName("OffeneTodoCountBadge"));

    /// <returns>Der aktuelle Text des Offene-Todos-Badges ("OffeneTodoCountBadge").</returns>
    public string GetOpenCount() => GetHelpTextOrName(WaitForElement(Window, cf => cf.ByName("OffeneTodoCountBadge"), Short));

    /// <summary>Wartet, bis das Offene-Todos-Badge verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public TodoListView WaitUntilBadgeGone()
    {
        WaitUntilGone(Window, cf => cf.ByName("OffeneTodoCountBadge"), Short);
        return this;
    }

    /// <summary>Erstellt ein neues To-Do über das Eingabefeld "NeuesTodoBeschreibung" und den "TodoHinzufuegen"-Button.</summary>
    /// <param name="description">Die Beschreibung des neuen To-Dos.</param>
    /// <returns>Diese Instanz.</returns>
    public TodoListView CreateTodo(string description)
    {
        var eingabeFeld = WaitForElement(Window, cf => cf.ByName("NeuesTodoBeschreibung"), Short);
        eingabeFeld.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(description);
        WaitForElement(Window, cf => cf.ByName("TodoHinzufuegen"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Hakt das To-Do mit der angegebenen Beschreibung über seine Checkbox ab.</summary>
    /// <param name="description">Die Beschreibung des To-Dos.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Checkbox nicht gefunden wird.</exception>
    public TodoListView CheckOff(string description)
    {
        var eintrag = WaitForElement(Window, cf => cf.ByName(description), Short);
        var container = eintrag.Parent;
        var checkbox = container?.FindFirstDescendant(cf => cf.ByName("TodoErledigtCheckbox"))
            ?? throw new InvalidOperationException($"Checkbox für To-Do '{description}' nicht gefunden.");
        checkbox.AsCheckBox().Click();
        return this;
    }

    /// <summary>Löscht das To-Do mit der angegebenen Beschreibung über seinen Löschen-Button.</summary>
    /// <param name="description">Die Beschreibung des To-Dos.</param>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn der Löschen-Button nicht gefunden wird.</exception>
    public TodoListView DeleteTodo(string description)
    {
        var eintrag = WaitForElement(Window, cf => cf.ByName(description), Short);
        var container = eintrag.Parent;
        var loeschenButton = container?.FindFirstDescendant(cf => cf.ByName("TodoLoeschen"))
            ?? throw new InvalidOperationException($"Löschen-Button für To-Do '{description}' nicht gefunden.");
        loeschenButton.AsButton().Click();
        return this;
    }

    /// <param name="description">Der zu prüfende Todo-Text.</param>
    /// <returns><c>true</c>, wenn ein To-Do mit diesem Text aktuell sichtbar ist.</returns>
    public bool HasTodo(string description) => ElementExists(Window, cf => cf.ByName(description));

    /// <summary>Wartet, bis ein To-Do mit dem angegebenen Text sichtbar ist.</summary>
    /// <param name="description">Der erwartete Todo-Text.</param>
    /// <returns>Diese Instanz.</returns>
    public TodoListView WaitForTodo(string description)
    {
        WaitForElement(Window, cf => cf.ByName(description), Short);
        return this;
    }
}
