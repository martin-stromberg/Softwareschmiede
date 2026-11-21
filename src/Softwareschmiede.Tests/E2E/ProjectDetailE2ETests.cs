using FlaUI.Core.AutomationElements;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Projektdetailansicht mit Ribbon-Menü und Kacheln.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// Konsolidierung (Issue #153): <see cref="ProjectDetailScenarios"/> führt alle sechs Szenarien
/// (Navigation, Bearbeiten, Aufgaben/Filtern, Repository-Dialog, Offene/beendete Aufgaben-Trennung,
/// Löschen) als aufeinanderfolgende Phasen in einem gemeinsamen App-Lifecycle aus. Jede Phase räumt ihr
/// Projekt über <see cref="ProjectDetailView.DeleteProject"/> auf und kehrt über
/// <see cref="MenuView.NavigateToDashboard"/> zum Dashboard zurück, bevor die nächste Phase mit
/// <see cref="MenuView.NavigateToProjects"/> neu beginnt - ein erneuter Klick auf " Projekte" direkt
/// aus einer bereits geöffneten Projektdetailansicht heraus navigiert nicht zuverlässig zur Übersicht,
/// sondern bleibt in der zuletzt geöffneten Projektansicht (daher immer zuerst zurück zum Dashboard).
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class ProjectDetailE2ETests : WpfTestBase
{
    /// <summary>
    /// Führt sechs Projektdetail-Szenarien nacheinander in einem gemeinsamen App-Lifecycle aus:
    /// Navigation (Neuanlage abbrechen, öffnen/schließen), Bearbeiten (umbenennen), Aufgaben anlegen
    /// und filtern, Repository-Dialog prüfen, Trennung offener/beendeter Aufgaben, sowie Löschen.
    /// Jede Phase räumt ihr Projekt auf, bevor die nächste beginnt.
    /// </summary>
    [Fact]
    public async Task ProjectDetailScenarios()
    {
        var mainWindow = LaunchAppAndGetMainWindow();

        ProjectNavigation_CloseNewcreationCancelAndOpenAnd_E2E(mainWindow);
        ProjectEdit_NamesChangeSaveBackAndAgainEdit_E2E(mainWindow);
        WorkItemsInProjectdetail_FilterNewCreateAnd_E2E(mainWindow);
        RepositoryDialog_OpenButtonAssignPluginAndWorkingDirectory_E2E(mainWindow);
        await Projectdetailview_DisconnectsOpenAndFinishedWorkItems_E2E(mainWindow);
        DeleteProject_ConfirmationRequiredAndOverlayClosed_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Projekt anlegen; Neuanlage starten und über "Zurück" abbrechen; erstes Projekt
    /// öffnen und wieder verlassen; erneut öffnen; zuletzt zur Übersicht zurücknavigieren.
    /// Prüft: Nach Cancel der Neuanlage ist das erste Projekt noch in der Liste aufrufbar; das
    /// wiederholte Öffnen/Verlassen der Detailansicht funktioniert; die Übersicht zeigt die
    /// Projektkachel nach dem finalen "Zurück".
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void ProjectNavigation_CloseNewcreationCancelAndOpenAnd_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();

        // Erstes Projekt anlegen
        projectList.CreateProject("Bestehendes-Projekt");

        // Neuanlage starten und über Zurück abbrechen (kein Speichern, direkter ForceClose reicht als "Cancel")
        var newButton = WaitForElement(mainWindow, cf => cf.ByName("New"), Short);
        newButton.AsButton().ClickInForeground();
        WaitForElement(mainWindow, cf => cf.ByName("Save"), Short);

        // ForceClose(false) gibt laut Vertrag stets die ursprüngliche Instanz zurück (siehe
        // BaseWindowView.ForceClose), nicht die tatsächlich erreichte Ansicht - deshalb wird der
        // Rückgabewert verworfen und CurrentView() danach erneut abgefragt.
        new ProjectDetailView(mainWindow).ForceClose(recurseToDashboard: false);
        var projectListAfterCancel = Assert.IsType<ProjectListView>(mainWindow.CurrentView());

        // Erstes Projekt ist noch in der Liste und aufrufbar
        var projectDetail = projectListAfterCancel.OpenProject("Bestehendes-Projekt");
        Assert.True(projectDetail.IsVisible);

        // Zurück zur Übersicht, Projekt erneut öffnen
        projectDetail.ForceClose(recurseToDashboard: false);
        var projectListAgain = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        var projectDetailAgain = projectListAgain.OpenProject("Bestehendes-Projekt");
        Assert.True(projectDetailAgain.IsVisible);

        // Zurück zur Übersicht
        projectDetailAgain.ForceClose(recurseToDashboard: false);
        var projectListFinal = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListFinal.ProjectExists("Bestehendes-Projekt"));

        var projectDetailToDelete = projectListFinal.OpenProject("Bestehendes-Projekt");
        var projectListAfterDelete = projectDetailToDelete.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Projekt anlegen und öffnen, Namen ändern und speichern, zur Übersicht
    /// zurücknavigieren, Projektkachel erneut öffnen, erneut umbenennen und speichern.
    /// Prüft: Die Projektkachel zeigt nach dem ersten Speichern den aktualisierten Namen; die
    /// erneute Bearbeitung (UpdateAsync-Pfad) hält den aktualisierten Namen im Textfeld.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void ProjectEdit_NamesChangeSaveBackAndAgainEdit_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("Umbenennen-Test");
        var projectDetail = projectList.OpenProject("Umbenennen-Test");

        projectDetail.SetProjectName("Umbenennen-Test-Aktualisiert");
        projectDetail.SaveChanges();

        // Zurück zur Übersicht navigieren; Projektkachel zeigt jetzt den neuen Namen
        projectDetail.ForceClose(recurseToDashboard: false);
        var projectListAfterRename = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListAfterRename.ProjectExists("Umbenennen-Test-Aktualisiert"));

        // Kachel erneut öffnen → Detailansicht öffnet sich
        var projectDetailReopened = projectListAfterRename.OpenProject("Umbenennen-Test-Aktualisiert");

        // Erneut bearbeiten und speichern (UpdateAsync-Pfad); Name bleibt aktualisiert
        projectDetailReopened.SetProjectName("Umbenennen-Test-Aktualisiert-Erneut");
        projectDetailReopened.SaveChanges();
        Assert.Equal("Umbenennen-Test-Aktualisiert-Erneut", projectDetailReopened.GetProjectName());

        var projectListFinal = projectDetailReopened.DeleteProject();
        projectListFinal.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Projekt löschen.
    /// Prüft: Bestätigungsdialog erscheint, Löschen schließt das Overlay.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void DeleteProject_ConfirmationRequiredAndOverlayClosed_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("Loeschen-Test");
        var projectDetail = projectList.OpenProject("Loeschen-Test");

        projectDetail.DeleteProject();
    }

    /// <summary>
    /// Szenario: Projektdetailansicht trennt offene und beendete Aufgaben.
    /// Prueft: Offene Aufgaben sind direkt sichtbar, beendete Aufgaben erst nach Aufklappen des Expanders.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task Projectdetailview_DisconnectsOpenAndFinishedWorkItems_E2E(Window mainWindow)
    {
        var projectName = "Archivierte-Aufgaben-E2E";
        var openWorkItemTitle = "Offene Aufgabe E2E";
        var finishedWorkItemTitle = "Beendete Aufgabe E2E";

        await using (var db = OpenTestDbContext())
        {
            var projectService = new ProjectService(db, NullLogger<ProjectService>.Instance);
            var workItemService = new WorkItemService(db, NullLogger<WorkItemService>.Instance, new TodoService(db, NullLogger<TodoService>.Instance));

            var project = await projectService.CreateAsync(projectName, null);
            await workItemService.CreateAsync(project.Id, openWorkItemTitle, null);
            var finishedWorkItem = await workItemService.CreateAsync(project.Id, finishedWorkItemTitle, null);
            await workItemService.SetStatusUncheckedAsync(finishedWorkItem.Id, WorkItemStatus.Completed);
        }

        var projectList = new ProjectListView(mainWindow).ForceShow();
        var projectDetail = projectList.OpenProject(projectName);

        var openItems = projectDetail.GetTaskElements();
        Assert.Contains(openItems, item => item.Name == openWorkItemTitle);
        Assert.DoesNotContain(openItems, item => item.Name == finishedWorkItemTitle);

        Assert.True(projectDetail.IsFinishedTasksExpanderCollapsed());

        var finishedItems = projectDetail.ExpandAndGetFinishedTasks();
        Assert.Contains(finishedItems, item => item.Name == finishedWorkItemTitle);

        var projectListAfterDelete = projectDetail.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Aufgabe neu anlegen und in die Aufgabenliste zurückkehren; anschließend das
    /// Filter-Overlay öffnen, den RadioButton "Aktiv" wählen und das Overlay wieder schließen.
    /// Prüft: "AufgabeNeu" erstellt eine Aufgabe und navigiert zur separaten TaskDetailView; nach
    /// Zurück-Navigation erscheint die neue Aufgabe in der Aufgabenliste; das Filter-Overlay öffnet
    /// und schließt sich korrekt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void WorkItemsInProjectdetail_FilterNewCreateAnd_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("Aufgabe-Test");
        var projectDetail = projectList.OpenProject("Aufgabe-Test");

        // Neue Aufgabe erstellen; Navigation zur separaten TaskDetailView (Edit-Panel, da Status == Neu)
        var taskDetail = projectDetail.CreateTask();

        // Zurück zur Projektdetailansicht navigieren
        taskDetail.GoBack();
        var projectDetailAfterCreate = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Aufgabenliste enthält jetzt mindestens eine Aufgabe
        var items = projectDetailAfterCreate.GetTaskElements();
        Assert.True(items.Length >= 1, "Aufgabenliste sollte nach Anlage mindestens eine Aufgabe enthalten.");

        // Filter-Overlay öffnen, RadioButton "Aktiv" wählen, Overlay wieder schließen
        projectDetailAfterCreate.OpenFilter();
        projectDetailAfterCreate.SelectFilterOption("Aktiv");
        projectDetailAfterCreate.CloseFilter();

        var projectListAfterDelete = projectDetailAfterCreate.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: "Öffnen"-Button in der Detailansicht prüfen; anschließend den
    /// Repository-Zuweisungs-Dialog öffnen und die Arbeitsverzeichnis-ComboBox prüfen;
    /// Dialog über "Cancel" schließen.
    /// Prüft: Der "Öffnen"-Button existiert; der Dialog enthält das Label und die ComboBox für die
    /// Arbeitsverzeichnis-Auswahl (die Plugin-Auswahl-ComboBox ist bei nur einem aktiven SCM-Plugin
    /// ausgeblendet); nach "Cancel" bleibt das Hauptfenster-Overlay ("Speichern") weiterhin sichtbar.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private void RepositoryDialog_OpenButtonAssignPluginAndWorkingDirectory_E2E(Window mainWindow)
    {
        var projectList = new ProjectListView(mainWindow).ForceShow();
        projectList.CreateProject("Repository-Dialog-Test");
        var projectDetail = projectList.OpenProject("Repository-Dialog-Test");

        Assert.True(projectDetail.HasOpenButton());

        var dialog = new RepositoryAssignDialogView(mainWindow).ForceShow();

        // Label "Arbeitsverzeichnis im Repository" ist vorhanden. Die ComboBox für die
        // Arbeitsverzeichnis-Auswahl ist vorhanden. Die Plugin-Auswahl-ComboBox wird nur angezeigt,
        // wenn mehrere SCM-Plugins aktiv sind (hier nur LocalDirectoryPlugin).
        Assert.True(dialog.HasWorkingDirectoryLabel());
        Assert.True(dialog.HasWorkingDirectoryComboBox());

        dialog.Cancel();

        // Hauptfenster-Overlay noch offen (Speichern-Button sichtbar)
        Assert.True(projectDetail.IsVisible);

        var projectListAfterDelete = projectDetail.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }
}
