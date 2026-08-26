using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
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

    /// <summary>Öffnet die erste Aufgabe aus der "OffeneAufgabenListe" per Doppelklick (fensterumfassend).</summary>
    /// <returns>Die Aufgabendetailansicht der geöffneten Aufgabe.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die Aufgabenliste kein Element enthält.</exception>
    public TaskDetailView OpenFirstTask()
    {
        var items = GetTaskElements();
        if (items.Length == 0)
            throw new InvalidOperationException("OffeneAufgabenListe enthielt kein Element.");

        items[0].DoubleClick();
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Öffnet eine Aufgabe aus der "OffeneAufgabenListe" anhand ihres Titels per Doppelklick (fensterumfassend).</summary>
    /// <param name="title">Der Titel der zu öffnenden Aufgabe.</param>
    /// <returns>Die Aufgabendetailansicht der geöffneten Aufgabe.</returns>
    public TaskDetailView OpenTask(string title)
    {
        WaitForElement(Window, cf => cf.ByName(title).And(cf.ByControlType(ControlType.ListItem)), Medium).DoubleClick();
        WaitForElement(Window, cf => cf.ByName("Zurück"), Short);

        return new TaskDetailView(Window);
    }

    /// <summary>Wartet, bis eine Aufgabe mit dem angegebenen Titel in der "OffeneAufgabenListe" erscheint.</summary>
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


    /// <summary>Setzt den Projektnamen. Markiert zuvor den vorhandenen Inhalt (Strg+A), damit ein bereits
    /// vorhandener Name (z. B. beim erneuten Bearbeiten eines bestehenden Projekts) ersetzt statt an der
    /// Cursorposition eingefügt wird.</summary>
    /// <param name="name">Der neue Projektname.</param>
    public void SetProjectName(string name)
    {
        var nameBox = WaitForElement(Window, cf => cf.ByName("ProjektName"), Short);
        nameBox.Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(name);
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
    /// "Speichern" (Neuanlage-Pfad), oder das "ProjektName"-Feld zeigt wieder den zuvor eingetragenen
    /// Namen (Bearbeitungs-Pfad - Beleg für den abgeschlossenen Speichern-und-Neuladen-Roundtrip).
    /// </summary>
    /// <returns>Diese Instanz.</returns>
    /// <exception cref="TimeoutException">Wird geworfen, wenn keines der beiden Abschlusssignale rechtzeitig eintritt.</exception>
    public ProjectDetailView SaveChanges()
    {
        var expectedName = GetProjectName().Trim();

        WaitForElement(Window, cf => cf.ByName("Speichern"), Short).AsButton().Click();

        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            if (!ElementExists(Window, cf => cf.ByName("Speichern")))
                return this;

            var nameBox = Window.FindFirstDescendant(cf => cf.ByName("ProjektName"));
            if (nameBox is not null && string.Equals(nameBox.AsTextBox().Text, expectedName, StringComparison.Ordinal))
                return this;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Speichern wurde nicht innerhalb von {Medium.TotalSeconds}s abgeschlossen (weder verschwand 'Speichern', " +
            $"noch zeigte 'ProjektName' wieder '{expectedName}').");
    }

    /// <returns>Der aktuell angezeigte Basis-Branch ("BasisBranchAnzeige").</returns>
    public string GetBaseBranch() => WaitForElement(Window, cf => cf.ByAutomationId("BasisBranchAnzeige"), Short).Name;

    /// <summary>Öffnet den Bearbeitungsmodus für den Basis-Branch über den "BasisBranchBearbeiten"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView EditBaseBranch()
    {
        WaitForElement(Window, cf => cf.ByName("BasisBranchBearbeiten"), Short).AsButton().Click();
        return this;
    }

    /// <summary>Trägt einen neuen Basis-Branch im Bearbeitungsmodus ein ("BasisBranchBearbeitenEingabe").</summary>
    /// <param name="baseBranch">Der einzutragende Basis-Branch.</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SetBaseBranch(string baseBranch)
    {
        WaitForElement(Window, cf => cf.ByName("BasisBranchBearbeitenEingabe"), Short).AsTextBox().Text = baseBranch;
        return this;
    }

    /// <summary>Speichert den bearbeiteten Basis-Branch über den "BasisBranchSpeichern"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SaveBaseBranch()
    {
        WaitForElement(Window, cf => cf.ByName("BasisBranchSpeichern"), Short).AsButton().Click();
        return this;
    }

    /// <returns><c>true</c>, wenn der "Öffnen"-Button sichtbar ist.</returns>
    public bool HasOpenButton() => ElementExists(Window, cf => cf.ByName("Öffnen"));

    /// <summary>Öffnet das Filter-Overlay über den "Filter"-Button.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView OpenFilter()
    {
        WaitForElement(Window, cf => cf.ByName("Filter"), Short).AsButton().Click();
        WaitForElement(Window, cf => cf.ByName("Aufgaben filtern"), Short);
        return this;
    }

    /// <summary>Wählt eine Filter-Option (RadioButton) im geöffneten Filter-Overlay aus.</summary>
    /// <param name="optionName">Der Name der Filter-Option (z. B. "Aktiv").</param>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView SelectFilterOption(string optionName)
    {
        WaitForElement(Window, cf => cf.ByName(optionName).And(cf.ByControlType(ControlType.RadioButton)), Short).Click();
        return this;
    }

    /// <summary>Schließt das Filter-Overlay erneut über den "Filter"-Button und wartet, bis es verschwindet.</summary>
    /// <returns>Diese Instanz.</returns>
    public ProjectDetailView CloseFilter()
    {
        WaitForElement(Window, cf => cf.ByName("Filter"), Short).AsButton().Click();
        WaitUntilGone(Window, cf => cf.ByName("Aufgaben filtern"), Short);
        return this;
    }

    /// <returns><c>true</c>, wenn der "BeendeteAufgabenExpander" eingeklappt (Collapsed) ist.</returns>
    public bool IsFinishedTasksExpanderCollapsed()
    {
        var expander = WaitForElement(Window, cf => cf.ByName("BeendeteAufgabenExpander"), Short);
        return expander.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Collapsed;
    }

    /// <summary>Klappt den "BeendeteAufgabenExpander" auf und gibt die Listenelemente der beendeten Aufgaben zurück.</summary>
    /// <returns>Die Listenelemente der "BeendeteAufgabenListe".</returns>
    public AutomationElement[] ExpandAndGetFinishedTasks()
    {
        var expander = WaitForElement(Window, cf => cf.ByName("BeendeteAufgabenExpander"), Short);
        expander.Patterns.ExpandCollapse.Pattern.Expand();

        var liste = WaitForElement(expander, cf => cf.ByName("BeendeteAufgabenListe"), Short);
        return liste.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
    }
}
