using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Aufgabendetail-Ansicht.</summary>
public sealed class TaskDetailView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public TaskDetailView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("EditTitel"))
           && (ElementExists(Window, cf => cf.ByName("Speichern")) || ElementExists(Window, cf => cf.ByName("Zurück")));

    /// <returns>Diese Instanz, wenn die Ansicht bereits sichtbar ist.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Ansicht noch nicht sichtbar ist.</exception>
    public override TaskDetailView ForceShow()
    {
        if (IsVisible)
            return this;

        throw new InvalidOperationException(
            "TaskDetailView.ForceShow() kann ohne bekannten Aufgabentitel keine Ansicht öffnen. " +
            "Nutze ProjectDetailView.CreateTask() oder eine Navigation über die Aufgabenliste.");
    }

    /// <inheritdoc/>
    public override TaskDetailView ForceClose(bool recurseToDashboard)
    {
        GoBack();

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }

    /// <returns>Der aktuelle Aufgabentitel.</returns>
    public string GetTaskTitle() => WaitForElement(Window, cf => cf.ByName("EditTitel"), Short).AsTextBox().Text;

    /// <param name="title">Der neue Aufgabentitel.</param>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SetTaskTitle(string title)
    {
        var box = WaitForElement(Window, cf => cf.ByName("EditTitel"), Short);
        box.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(title);

        return this;
    }

    /// <summary>Klickt den "Speichern"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView SaveTask()
    {
        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Löscht die aktuelle Aufgabe über den "Löschen"-Button und bestätigt den nativen Löschdialog.</summary>
    /// <returns>Die Projektdetailansicht, die nach dem Löschen sichtbar wird.</returns>
    public ProjectDetailView DeleteTask()
    {
        WaitForElement(Window, cf => cf.ByName("Starten"), Short);

        WaitForElement(Window, cf => cf.ByName("Löschen"), Short).AsButton().Click();
        new DeleteConfirmationDialogView(Window).Confirm();

        WaitUntilGone(Window, cf => cf.ByName("Starten"), Short);

        return new ProjectDetailView(Window);
    }

    /// <summary>Klickt den "Zurück"-Button und wartet auf die Projektdetailansicht.</summary>
    /// <returns>Diese Instanz.</returns>
    public TaskDetailView GoBack()
    {
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("ProjektName"), Medium);

        return this;
    }
}
