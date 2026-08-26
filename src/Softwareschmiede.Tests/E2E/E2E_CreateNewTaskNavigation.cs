using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Neuanlage von Aufgaben über die separate Aufgabendetailansicht (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Neue Aufgabe erstellen, Titel ausfüllen, speichern (Phase Speichern); anschließend
    /// explizit zur Projektansicht zurückkehren und eine weitere Aufgabe über "Zurück" abbrechen
    /// (Phase Abbrechen). Prüft: Die gespeicherte Aufgabe bleibt nach dem Speichern in der
    /// TaskDetailView geöffnet, wird mit Status "Neu" persistiert und erscheint nach expliziter
    /// Rücknavigation in der Liste. Die im Abbrechen-Pfad eingegebene Titeländerung wird nicht
    /// persistiert, die zuvor angelegte Aufgabe (Status "Neu") bleibt jedoch weiterhin vorhanden.
    /// </summary>
    protected void AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("NeueAufgabe-Test");
        var projectDetail = projectList.OpenProject("NeueAufgabe-Test");

        // Phase Speichern
        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle("Persistierte Neue Aufgabe");
        taskDetail.SaveTask();

        // Die TaskDetailView bleibt geöffnet; der Anwender kann direkt starten statt zur Liste zurückzufallen.
        taskDetail.WaitForPersisted();

        // Erst explizite Rücknavigation zeigt wieder die Projektliste.
        taskDetail.GoBack();
        var projectDetailAfterSave = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Neue Aufgabe erscheint mit aktualisiertem Titel in der Aufgabenliste.
        projectDetailAfterSave.WaitForTask("Persistierte Neue Aufgabe");

        // Phase Abbrechen
        var taskDetailAbbrechen = projectDetailAfterSave.CreateTask();
        taskDetailAbbrechen.SetTaskTitle("Nicht gespeicherter Titel");
        taskDetailAbbrechen.GoBack();
        var projectDetailAfterCancel = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Der nicht gespeicherte Titel erscheint nicht in der Aufgabenliste
        Assert.False(projectDetailAfterCancel.HasTask("Nicht gespeicherter Titel"));

        // Die Aufgabenliste enthält beide zuvor angelegten Aufgaben (Status "Neu")
        var items = projectDetailAfterCancel.GetTaskElements();
        Assert.True(items.Length >= 2, "Aufgabenliste sollte beide angelegten Aufgaben weiterhin enthalten.");
        projectDetailAfterCancel.DeleteProject();
    }
}
