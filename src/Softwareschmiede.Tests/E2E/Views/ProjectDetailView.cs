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
        => ElementExists(Window, cf => cf.ByName("ProjectName")) && ElementExists(Window, cf => cf.ByName("NewWorkItem"));

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
        WaitForElement(Window, cf => cf.ByName("Back"), Short).AsButton().ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("New"), Medium);

        if (recurseToDashboard)
            RecurseToDashboard();

        return this;
    }

    /// <returns>Der aktuelle Projektname.</returns>
    public string GetProjectName() => WaitForElement(Window, cf => cf.ByName("ProjectName"), Short).AsTextBox().Text;

    /// <summary>Erstellt über den "AufgabeNeu"-Button eine neue Aufgabe und navigiert in deren Detailansicht.</summary>
    /// <returns>Die Aufgabendetailansicht der neu angelegten Aufgabe.</returns>
    public TaskDetailView CreateTask()
    {
        WaitForElement(Window, cf => cf.ByName("NewWorkItem"), Short).AsButton().ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("EditTitle"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Löscht das aktuell geöffnete Projekt über den "Löschen"-Button und bestätigt den nativen Löschdialog.</summary>
    /// <returns>Die Projektliste, die nach dem Löschen sichtbar wird.</returns>
    public ProjectListView DeleteProject()
    {
        WaitForElement(Window, cf => cf.ByName("NewWorkItem"), Short);

        WaitForElement(Window, cf => cf.ByName("Delete"), Short).AsButton().ClickInForeground();
        new DeleteConfirmationDialogView(Window).Confirm();

        WaitUntilGone(Window, cf => cf.ByName("Save"), Short);

        return new ProjectListView(Window);
    }

    /// <returns>Die Aufgaben-Listenelemente der "OpenWorkItemsList".</returns>
    public AutomationElement[] GetTaskElements()
    {
        var listBox = WaitForElement(Window, cf => cf.ByName("OpenWorkItemsList"), Medium);
        return listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
    }

    /// <summary>Öffnet die erste Aufgabe aus der "OpenWorkItemsList" per Doppelklick (fensterumfassend).</summary>
    /// <returns>Die Aufgabendetailansicht der geöffneten Aufgabe.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Aufgabenliste kein Element enthält.</exception>
    public TaskDetailView OpenFirstTask()
    {
        var items = GetTaskElements();
        if (items.Length == 0)
            throw new InvalidOperationException("OpenWorkItemsList enthielt kein Element.");

        items[0].DoubleClickInForeground();
        WaitForElement(Window, cf => cf.ByName("Back"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Öffnet eine Aufgabe aus der "OpenWorkItemsList" anhand ihres Titels per Doppelklick (fensterumfassend).</summary>
    /// <param name="title">Der Titel der zu öffnenden Aufgabe.</param>
    /// <returns>Die Aufgabendetailansicht der geöffneten Aufgabe.</returns>
    public TaskDetailView OpenTask(string title)
    {
        WaitForElement(Window, cf => cf.ByName(title).And(cf.ByControlType(ControlType.ListItem)), Medium).DoubleClickInForeground();
        WaitForElement(Window, cf => cf.ByName("Back"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Wartet, bis eine Aufgabe mit dem angegebenen Titel in der "OpenWorkItemsList" erscheint.</summary>
    /// <param name="title">Der erwartete Aufgabentitel.</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView WaitForTask(string title)
    {
        WaitForElement(Window, cf => cf.ByName(title), Short);
        return this;
    }

    /// <param name="title">Der zu prüfende Aufgabentitel.</param>
    /// <returns><c>true</c>, wenn eine Aufgabe mit diesem Titel aktuell sichtbar ist.</returns>
    public bool HasTask(string title) => ElementExists(Window, cf => cf.ByName(title));

    /// <summary>Wartet, bis die Aufgabenliste (List-Control) sichtbar ist.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView WaitForTaskListVisible()
    {
        WaitForElement(Window, cf => cf.ByControlType(ControlType.List), Medium);
        return this;
    }


    /// <summary>Ersetzt den Projektnamen und prüft den vollständigen Feldwert vor dem Speichern.</summary>
    /// <param name="name">Der neue Projektname.</param>
    public void SetProjectName(string name)
    {
        var nameBox = WaitForElement(Window, cf => cf.ByName("ProjectName"), Short);
        ElementWaitHelper.ReplaceTextBoxText(nameBox, name);
    }

    /// <summary>
    /// Speichert die Änderungen am Projekt über den "Speichern"-Button. Das Abschlussverhalten
    /// unterscheidet sich je nach ViewModel-Zustand (<c>ProjectDetailViewModel.ProjektSpeichernAsync</c>):
    /// Bei der Neuanlage (noch kein persistiertes Projekt) navigiert das ViewModel nach dem Speichern
    /// automatisch zur Projektliste zurück, wodurch die gesamte Ansicht inkl. "Speichern"-Button
    /// verschwindet. Bei der Bearbeitung eines bereits bestehenden Projekts bleibt die Ansicht dagegen
    /// geöffnet (das ViewModel lädt die Daten lediglich neu) - der "Speichern"-Button ist ein statischer
    /// Ribbon-Eintrag ohne Sichtbarkeitsbindung (siehe ProjectDetailView.xaml) und verschwindet daher
    /// NICHT. Wartet deshalb auf eines von zwei möglichen Abschlusssignalen: entweder verschwindet
    /// "Speichern" (Neuanlage-Pfad), oder das "ProjectName"-Feld zeigt wieder den zuvor eingetragenen
    /// Namen (Bearbeitungs-Pfad - Beleg für den abgeschlossenen Speichern-und-Neuladen-Roundtrip).
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn keines der beiden Abschlusssignale rechtzeitig eintritt.</exception>
    public ProjectDetailView SaveChanges()
    {
        var expectedName = GetProjectName().Trim();

        ElementWaitHelper.WaitForElement(Window, cf => cf.ByName("Save"), Short,
            element => !element.IsOffscreen && element.IsEnabled).AsButton().ClickInForeground();

        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            if (!ElementExists(Window, cf => cf.ByName("Save")))
                return this;

            var nameBox = Window.FindFirstDescendant(cf => cf.ByName("ProjectName"));
            if (nameBox is not null && string.Equals(nameBox.AsTextBox().Text, expectedName, StringComparison.Ordinal))
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Speichern wurde nicht innerhalb von {Medium.TotalSeconds}s abgeschlossen (weder verschwand 'Speichern', " +
            $"noch zeigte 'ProjectName' wieder '{expectedName}').");
    }

    /// <returns>Der aktuell angezeigte Basis-Branch ("BaseBranchDisplay").</returns>
    public string GetBaseBranch() => WaitForElement(Window, cf => cf.ByAutomationId("BaseBranchDisplay"), Short).Name;

    /// <summary>Öffnet den Bearbeitungsmodus für den Basis-Branch über den "EditBaseBranch"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView EditBaseBranch()
    {
        WaitForElement(Window, cf => cf.ByName("EditBaseBranch"), Short).AsButton().ClickInForeground();
        return this;
    }

    /// <summary>Trägt einen neuen Basis-Branch im Bearbeitungsmodus ein ("EditBaseBranchInput").</summary>
    /// <param name="baseBranch">Der einzutragende Basis-Branch.</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SetBaseBranch(string baseBranch)
    {
        WaitForElement(Window, cf => cf.ByName("EditBaseBranchInput"), Short).AsTextBox().Text = baseBranch;
        return this;
    }

    /// <summary>Speichert den bearbeiteten Basis-Branch über den "SaveBaseBranch"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SaveBaseBranch()
    {
        WaitForElement(Window, cf => cf.ByName("SaveBaseBranch"), Short).AsButton().ClickInForeground();
        return this;
    }

    /// <returns><c>true</c>, wenn der "Öffnen"-Button sichtbar ist.</returns>
    public bool HasOpenButton() => ElementExists(Window, cf => cf.ByName("Open"));

    /// <summary>Öffnet das Filter-Overlay über den "Filter"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView OpenFilter()
    {
        WaitForElement(Window, cf => cf.ByName("Filter"), Short).AsButton().ClickInForeground();
        WaitForElement(Window, cf => cf.ByName("Aufgaben filtern"), Short);
        return this;
    }

    /// <summary>Wählt eine Filter-Option (RadioButton) im geöffneten Filter-Overlay aus.</summary>
    /// <param name="optionName">Der Name der Filter-Option (z. B. "Aktiv").</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SelectFilterOption(string optionName)
    {
        WaitForElement(Window, cf => cf.ByName(optionName).And(cf.ByControlType(ControlType.RadioButton)), Short).ClickInForeground();
        return this;
    }

    /// <summary>Schließt das Filter-Overlay erneut über den "Filter"-Button und wartet, bis es verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView CloseFilter()
    {
        WaitForElement(Window, cf => cf.ByName("Filter"), Short).AsButton().ClickInForeground();
        WaitUntilGone(Window, cf => cf.ByName("Aufgaben filtern"), Short);
        return this;
    }

    /// <returns><c>true</c>, wenn der "CompletedWorkItemsExpander" eingeklappt (Collapsed) ist.</returns>
    public bool IsFinishedTasksExpanderCollapsed()
    {
        var expander = WaitForElement(Window, cf => cf.ByName("CompletedWorkItemsExpander"), Short);
        return expander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Collapsed;
    }

    /// <summary>Klappt den "CompletedWorkItemsExpander" auf und gibt die Listenelemente der beendeten Aufgaben zurück.</summary>
    /// <returns>Die Listenelemente der "CompletedWorkItemsList".</returns>
    public AutomationElement[] ExpandAndGetFinishedTasks()
    {
        var expander = WaitForElement(Window, cf => cf.ByName("CompletedWorkItemsExpander"), Short);
        expander.Patterns.ExpandCollapse.Pattern.Expand();

        var list = WaitForElement(expander, cf => cf.ByName("CompletedWorkItemsList"), Short);
        return list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
    }
}
