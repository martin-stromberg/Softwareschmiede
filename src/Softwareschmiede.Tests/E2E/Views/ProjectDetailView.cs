using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E.Views;

/// <summary>View für die Projektdetail-Ansicht.</summary>
public sealed class ProjectDetailView : BaseWindowView
{
    /// <param name="window">Das Hauptfenster der Anwendung.</param>
    public ProjectDetailView(Window window) : base(window)
    {
    }

    /// <inheritdoc/>
    public override bool IsVisible
        => ElementExists(Window, cf => cf.ByName("ProjektName")) && ElementExists(Window, cf => cf.ByName("AufgabeNeu"));

    /// <summary>
    /// Öffnet ein Projekt aus der Projektliste kann nur mit bekanntem Projektnamen erfolgen. Ist diese
    /// Ansicht noch nicht sichtbar, muss stattdessen <see cref="ProjectListView.OpenProject"/> genutzt werden.
    /// </summary>
    /// <returns>Diese Instanz, wenn die Ansicht bereits sichtbar ist.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Ansicht noch nicht sichtbar ist.</exception>
    public override ProjectDetailView ForceShow()
    {
        if (IsVisible)
            return this;

        throw new InvalidOperationException(
            "ProjectDetailView.ForceShow() kann ohne bekannten Projektnamen keine Ansicht öffnen. " +
            "Nutze ProjectListView.OpenProject(name), um zu einem bestimmten Projekt zu navigieren.");
    }

    /// <inheritdoc/>
    public override ProjectDetailView ForceClose(bool recurseToDashboard)
    {
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Neu"), Medium);

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }

    /// <returns>Der aktuelle Projektname.</returns>
    public string GetProjectName() => WaitForElement(Window, cf => cf.ByName("ProjektName"), Short).AsTextBox().Text;

    /// <summary>Erstellt über den "AufgabeNeu"-Button eine neue Aufgabe und navigiert in deren Detailansicht.</summary>
    /// <returns>Die Aufgabendetailansicht der neu angelegten Aufgabe.</returns>
    public TaskDetailView CreateTask()
    {
        WaitForElement(Window, cf => cf.ByName("AufgabeNeu"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("EditTitel"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Löscht das aktuell geöffnete Projekt über den "Löschen"-Button und bestätigt den nativen Löschdialog.</summary>
    /// <returns>Die Projektliste, die nach dem Löschen sichtbar wird.</returns>
    public ProjectListView DeleteProject()
    {
        WaitForElement(Window, cf => cf.ByName("AufgabeNeu"), Short);

        WaitForElement(Window, cf => cf.ByName("Löschen"), Short).AsButton().Click();
        new DeleteConfirmationDialogView(Window).Confirm();

        WaitUntilGone(Window, cf => cf.ByName("Speichern"), Short);

        return new ProjectListView(Window);
    }

    /// <returns>Die Aufgaben-Listenelemente der "OffeneAufgabenListe".</returns>
    public AutomationElement[] GetTaskElements()
    {
        var listBox = WaitForElement(Window, cf => cf.ByName("OffeneAufgabenListe"), Medium);
        return listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
    }
}
