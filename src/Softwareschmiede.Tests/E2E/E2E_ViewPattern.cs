using System.Threading;
using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Szenarien für das View-Pattern (Issue #231): erkennt Ansichten über
/// <see cref="WindowExtensions.CurrentView"/> und navigiert über die <c>*View</c>-Klassen aus
/// <c>Softwareschmiede.Tests.E2E.Views</c>, statt direkt mit rohen FlaUI-Aufrufen zu arbeiten.
/// Alle Phasen laufen als aufeinanderfolgende Schritte im isolierten
/// <c>General_ViewNavigation_E2E</c>-Szenario, damit Fehler dieser Navigation von anderen E2E-Flows getrennt bleiben.
/// </summary>
public partial class End2EndTest
{
    private const string ViewPatternProjectName = "ViewPatternProjekt";
    private const string ViewPatternWorkItemsTitle = "ViewPattern-Aufgabe";

    /// <summary>Happy Path: Dashboard → Projektliste → Projekt anlegen/öffnen → Aufgabe anlegen/speichern, jeweils über View-Erkennung geprüft.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ViewPatternHappyPath_NavigatesAndCreatesCorrect_E2E(Window mainWindow)
    {
        // Die vorherige Phase (CommandLineParameters) endet auf der Einstellungsseite, nicht dem
        // Dashboard - ForceShow() stellt den erwarteten Ausgangszustand her, statt ihn stillschweigend
        // vorauszusetzen.
        var dashboardView = new DashboardView(mainWindow).ForceShow();
        Assert.True(dashboardView.IsVisible);
        Assert.IsType<DashboardView>(mainWindow.CurrentView());

        dashboardView.Menu.NavigateToProjects();
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListView.IsVisible);

        projectListView.CreateProject(ViewPatternProjectName);
        var projectDetailView = projectListView.OpenProject(ViewPatternProjectName);
        Assert.True(projectDetailView.IsVisible);
        Assert.Equal(ViewPatternProjectName, projectDetailView.GetProjectName());
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        var taskDetailView = projectDetailView.CreateTask();
        Assert.True(taskDetailView.IsVisible);
        Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        taskDetailView.SetTaskTitle(ViewPatternWorkItemsTitle).SaveTask();
        Assert.Equal(ViewPatternWorkItemsTitle, taskDetailView.GetTaskTitle());
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> TaskDetail, ProjectDetail, ProjectList, Settings und Dashboard korrekt erkennt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ViewDetection_ReturnsCorrectViewTypes_E2E(Window mainWindow)
    {
        var taskDetailView = Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        taskDetailView.ForceClose(recurseToDashboard: false);
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        Assert.True(projectDetailView.IsVisible);

        projectDetailView.ForceClose(recurseToDashboard: false);
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListView.IsVisible);
        Assert.Contains(projectListView.GetProjectElements(), e => e.Name == ViewPatternProjectName);

        var settingsView = new SettingsView(mainWindow).ForceShow();
        Assert.True(settingsView.IsVisible);
        Assert.IsType<SettingsView>(mainWindow.CurrentView());

        settingsView.SwitchTab("Plugins");
        Assert.True(settingsView.IsOnPluginsTab());
        settingsView.SaveSettings();

        settingsView.ForceClose(recurseToDashboard: false);
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        Assert.True(dashboardView.IsVisible);
    }

    /// <summary>Prüft, dass <see cref="MenuView"/> zwischen Projektliste, Settings und Dashboard navigiert.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void MenuNavigation_SwitchesBetweenView_E2E(Window mainWindow)
    {
        var menu = new MenuView(mainWindow);
        Assert.True(menu.IsVisible);

        menu.NavigateToProjects();
        Assert.IsType<ProjectListView>(mainWindow.CurrentView());

        menu.NavigateToSettings();
        Assert.IsType<SettingsView>(mainWindow.CurrentView());

        menu.NavigateToDashboard();
        Assert.IsType<DashboardView>(mainWindow.CurrentView());
    }

    /// <summary>Prüft <see cref="BaseWindowView.ForceShow"/> für Dashboard, ProjectList und ProjectDetail, inklusive No-Op bei bereits sichtbarer Ansicht.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ForceShow_NavigatesCorrectToView_E2E(Window mainWindow)
    {
        var dashboardView = new DashboardView(mainWindow).ForceShow();
        Assert.True(dashboardView.IsVisible);

        var projectListView = new ProjectListView(mainWindow).ForceShow();
        Assert.True(projectListView.IsVisible);

        var projectDetailView = projectListView.OpenProject(ViewPatternProjectName);
        Assert.True(projectDetailView.IsVisible);

        // ForceShow() ist No-Op, wenn die Ansicht bereits sichtbar ist (siehe Designentscheidung in plan.md).
        var sameProjectDetailView = projectDetailView.ForceShow();
        Assert.True(sameProjectDetailView.IsVisible);
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
    }

    /// <summary>Prüft, dass <c>TaskDetailView.ForceClose(recurseToDashboard: false)</c> nur bis ProjectDetailView schließt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ForceClose_WithoutRecursion_ClosesOnlyALevel_E2E(Window mainWindow)
    {
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetailView = projectDetailView.CreateTask();
        taskDetailView.SetTaskTitle(ViewPatternWorkItemsTitle + "-OhneRekursion").SaveTask();

        taskDetailView.ForceClose(recurseToDashboard: false);
        var projectDetailViewAfterClose = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        Assert.True(projectDetailViewAfterClose.IsVisible);
        Assert.True(projectDetailViewAfterClose.GetTaskElements().Length >= 2);
    }

    /// <summary>Prüft, dass <c>TaskDetailView.ForceClose(recurseToDashboard: true)</c> bis zum Dashboard durchreicht.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ForceClose_WithRecursion_ClosesUntilDashboard_E2E(Window mainWindow)
    {
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetailView = projectDetailView.CreateTask();
        taskDetailView.SetTaskTitle(ViewPatternWorkItemsTitle + "-MitRekursion").SaveTask();

        taskDetailView.ForceClose(recurseToDashboard: true);
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        Assert.True(dashboardView.IsVisible);
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> den Repository-Zuweisungs- und den KI-Plugin-Auswahl-Dialog erkennt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void DialogDetection_ReturnsCorrectDialogViewTypes_E2E(Window mainWindow)
    {
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        dashboardView.Menu.NavigateToProjects();
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        var projectDetailView = projectListView.OpenProject(ViewPatternProjectName);

        var repositoryDialog = new RepositoryAssignDialogView(mainWindow).ForceShow();
        Assert.True(repositoryDialog.IsVisible);
        Assert.IsType<RepositoryAssignDialogView>(mainWindow.CurrentView());
        repositoryDialog.ForceClose(recurseToDashboard: false);
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        var taskDetailView = projectDetailView.CreateTask();

        var pluginDialog = new PluginSelectionDialogView(mainWindow).ForceShow();
        Assert.True(pluginDialog.IsVisible);
        Assert.IsType<PluginSelectionDialogView>(mainWindow.CurrentView());
        pluginDialog.ForceClose(recurseToDashboard: false);
        Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        // Prüft den Abbrechen-Pfad des nativen Löschdialogs: Aufgabe bleibt erhalten, wenn der
        // Bestätigungsdialog über "Nein" (DeleteConfirmationDialogView.Cancel()) verlassen wird.
        WaitForElement(mainWindow, cf => cf.ByName("Delete"), Short).AsButton().ClickInForeground();
        new DeleteConfirmationDialogView(mainWindow).Cancel();
        Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        taskDetailView.DeleteTask();
        projectDetailView.DeleteProject();
        Assert.IsType<ProjectListView>(mainWindow.CurrentView());

        new ProjectListView(mainWindow).ForceClose(recurseToDashboard: false);
        Assert.IsType<DashboardView>(mainWindow.CurrentView());
    }

    /// <summary>
    /// Prüft, dass <see cref="WindowExtensions.CurrentView"/> auf einem Fenster ohne jeden View-Marker eine
    /// aussagekräftige <see cref="InvalidOperationException"/> wirft. Nutzt eine eigens für diesen Test
    /// erzeugte, native MessageBox mit eindeutigem Titel als garantiert unbekanntes Fenster - der native
    /// "Löschen bestätigen"-Dialog der Anwendung selbst eignet sich dafür nicht, da er über
    /// <see cref="DeleteConfirmationDialogView"/> bereits ein bekannter, korrekt erkannter Marker ist.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void UnknownView_ThrowsMeaningfulException_E2E(Window mainWindow)
    {
        const string unknownTitle = "ViewPattern-Test-UnbekannteAnsicht";
        var messageBoxThread = new Thread(() => System.Windows.MessageBox.Show("Fenster ohne View-Marker.", unknownTitle))
        {
            IsBackground = true
        };
        messageBoxThread.SetApartmentState(ApartmentState.STA);
        messageBoxThread.Start();

        try
        {
            var msgBoxWindow = WaitForWindow(unknownTitle, Short).AsWindow();

            var exception = Assert.Throws<InvalidOperationException>(() => msgBoxWindow.CurrentView());
            Assert.Contains("konnte keine bekannte Ansicht erkennen", exception.Message);

            // Native MessageBox unterstützt WindowPattern.Close() nicht (FlaUI.Core.Exceptions.
            // MethodNotSupportedException) - stattdessen über den Standard-"OK"-Button schließen.
            WaitForElement(msgBoxWindow, cf => cf.ByName("OK"), Short).AsButton().ClickInForeground();
        }
        finally
        {
            messageBoxThread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.IsType<DashboardView>(mainWindow.CurrentView());
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> das Fehlerbanner als <see cref="ErrorView"/> erkennt, ausgelöst durch ein fehlendes Arbeitsverzeichnis.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected async Task ErrorViewDetection_ShowsErrormessage_E2E(Window mainWindow)
    {
        SetupProjectWithNewWorkItemForStartedApp(mainWindow, "ViewPattern-Fehler-Repo", "ViewPattern-Fehler-Projekt");

        await SeedRepositoryWorkingDirectoryAsync("does-not-exist");

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartAndPluginSelect(mainWindow, "Softwareschmiede.KiSimulator");

        var errorView = Assert.IsType<ErrorView>(mainWindow.CurrentView());
        Assert.True(errorView.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(errorView.GetErrorMessage()));

        DeleteCurrentTask(mainWindow);
        errorView.DismissError();
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }
}
