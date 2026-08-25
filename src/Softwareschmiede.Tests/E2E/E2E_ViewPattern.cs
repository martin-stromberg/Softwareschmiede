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
/// Alle Phasen laufen als aufeinanderfolgende Schritte in einem gemeinsamen App-Lifecycle
/// (aufgerufen aus <see cref="End2EndTest.RunGeneralTests"/>), um zusätzliche App-Starts zu vermeiden.
/// </summary>
public partial class End2EndTest
{
    private const string ViewPatternProjektName = "ViewPatternProjekt";
    private const string ViewPatternAufgabenTitel = "ViewPattern-Aufgabe";

    /// <summary>Happy Path: Dashboard → Projektliste → Projekt anlegen/öffnen → Aufgabe anlegen/speichern, jeweils über View-Erkennung geprüft.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ViewPatternHappyPath_NavigiertUndErstelltKorrekt_E2E(Window mainWindow)
    {
        // Die vorherige Phase (CommandLineParameters) endet auf der Einstellungsseite, nicht dem
        // Dashboard (siehe Kommentar in AutonomAufgabeInitialisierung_..._E2E) - ForceShow() stellt
        // den erwarteten Ausgangszustand her, statt ihn stillschweigend vorauszusetzen.
        var dashboardView = new DashboardView(mainWindow).ForceShow();
        Assert.True(dashboardView.IsVisible);
        Assert.IsType<DashboardView>(mainWindow.CurrentView());

        dashboardView.Menu.NavigateToProjects();
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListView.IsVisible);

        projectListView.CreateProject(ViewPatternProjektName);
        var projectDetailView = projectListView.OpenProject(ViewPatternProjektName);
        Assert.True(projectDetailView.IsVisible);
        Assert.Equal(ViewPatternProjektName, projectDetailView.GetProjectName());
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        var taskDetailView = projectDetailView.CreateTask();
        Assert.True(taskDetailView.IsVisible);
        Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        taskDetailView.SetTaskTitle(ViewPatternAufgabenTitel).SaveTask();
        Assert.Equal(ViewPatternAufgabenTitel, taskDetailView.GetTaskTitle());
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> TaskDetail, ProjectDetail, ProjectList, Settings und Dashboard korrekt erkennt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void AnsichtenErkennung_LiefertKorrekteViewTypen_E2E(Window mainWindow)
    {
        var taskDetailView = Assert.IsType<TaskDetailView>(mainWindow.CurrentView());

        taskDetailView.ForceClose(recurseToDashboard: false);
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        Assert.True(projectDetailView.IsVisible);

        projectDetailView.ForceClose(recurseToDashboard: false);
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        Assert.True(projectListView.IsVisible);
        Assert.Contains(projectListView.GetProjectElements(), e => e.Name == ViewPatternProjektName);

        var settingsView = new SettingsView(mainWindow).ForceShow();
        Assert.True(settingsView.IsVisible);
        Assert.IsType<SettingsView>(mainWindow.CurrentView());

        var initialTab = settingsView.GetActiveTab();
        settingsView.SwitchTab("Plugins");
        Assert.NotEqual(initialTab, settingsView.GetActiveTab());
        settingsView.SaveSettings();

        settingsView.ForceClose(recurseToDashboard: false);
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        Assert.True(dashboardView.IsVisible);
    }

    /// <summary>Prüft, dass <see cref="MenuView"/> zwischen Projektliste, Einstellungen und Dashboard navigiert.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void MenueNavigation_WechseltZwischenAnsichten_E2E(Window mainWindow)
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
    protected void ForceShow_NavigiertKorrektZuAnsicht_E2E(Window mainWindow)
    {
        var dashboardView = new DashboardView(mainWindow).ForceShow();
        Assert.True(dashboardView.IsVisible);

        var projectListView = new ProjectListView(mainWindow).ForceShow();
        Assert.True(projectListView.IsVisible);

        var projectDetailView = projectListView.OpenProject(ViewPatternProjektName);
        Assert.True(projectDetailView.IsVisible);

        // ForceShow() ist No-Op, wenn die Ansicht bereits sichtbar ist (siehe Designentscheidung in plan.md).
        var sameProjectDetailView = projectDetailView.ForceShow();
        Assert.True(sameProjectDetailView.IsVisible);
        Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
    }

    /// <summary>Prüft, dass <c>TaskDetailView.ForceClose(recurseToDashboard: false)</c> nur bis ProjectDetailView schließt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ForceClose_OhneRekursion_SchliesstNurEineEbene_E2E(Window mainWindow)
    {
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetailView = projectDetailView.CreateTask();
        taskDetailView.SetTaskTitle(ViewPatternAufgabenTitel + "-OhneRekursion").SaveTask();

        taskDetailView.ForceClose(recurseToDashboard: false);
        var projectDetailViewAfterClose = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        Assert.True(projectDetailViewAfterClose.IsVisible);
        Assert.True(projectDetailViewAfterClose.GetTaskElements().Length >= 2);
    }

    /// <summary>Prüft, dass <c>TaskDetailView.ForceClose(recurseToDashboard: true)</c> bis zum Dashboard durchreicht.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void ForceClose_MitRekursion_SchliesstBisDashboard_E2E(Window mainWindow)
    {
        var projectDetailView = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskDetailView = projectDetailView.CreateTask();
        taskDetailView.SetTaskTitle(ViewPatternAufgabenTitel + "-MitRekursion").SaveTask();

        taskDetailView.ForceClose(recurseToDashboard: true);
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        Assert.True(dashboardView.IsVisible);
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> den Repository-Zuweisungs- und den KI-Plugin-Auswahl-Dialog erkennt.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected void DialogErkennung_LiefertKorrekteDialogViewTypen_E2E(Window mainWindow)
    {
        var dashboardView = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        dashboardView.Menu.NavigateToProjects();
        var projectListView = Assert.IsType<ProjectListView>(mainWindow.CurrentView());
        var projectDetailView = projectListView.OpenProject(ViewPatternProjektName);

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
        WaitForElement(mainWindow, cf => cf.ByName("Löschen"), Short).AsButton().Click();
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
    protected void UnbekannteAnsicht_WirftAussagekraeftigeException_E2E(Window mainWindow)
    {
        const string unbekannterTitel = "ViewPattern-Test-UnbekannteAnsicht";
        var messageBoxThread = new Thread(() => System.Windows.MessageBox.Show("Fenster ohne View-Marker.", unbekannterTitel))
        {
            IsBackground = true
        };
        messageBoxThread.SetApartmentState(ApartmentState.STA);
        messageBoxThread.Start();

        try
        {
            var msgBoxWindow = WaitForWindow(unbekannterTitel, Short).AsWindow();

            var exception = Assert.Throws<InvalidOperationException>(() => msgBoxWindow.CurrentView());
            Assert.Contains("konnte keine bekannte Ansicht erkennen", exception.Message);

            msgBoxWindow.Close();
        }
        finally
        {
            messageBoxThread.Join(TimeSpan.FromSeconds(5));
        }

        Assert.IsType<DashboardView>(mainWindow.CurrentView());
    }

    /// <summary>Prüft, dass <see cref="WindowExtensions.CurrentView"/> das Fehlerbanner als <see cref="ErrorView"/> erkennt, ausgelöst durch ein fehlendes Arbeitsverzeichnis.</summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster.</param>
    protected async Task FehlerAnsichtErkennung_ZeigtFehlermeldung_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, "ViewPattern-Fehler-Repo", "ViewPattern-Fehler-Projekt");

        await SeedRepositoryWorkingDirectoryAsync("does-not-exist");

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var errorView = Assert.IsType<ErrorView>(mainWindow.CurrentView());
        Assert.True(errorView.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(errorView.GetErrorMessage()));

        DeleteCurrentTask(mainWindow);
        errorView.DismissError();
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }
}
