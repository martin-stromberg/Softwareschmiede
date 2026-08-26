using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für den kombinierten Start-Ablauf (Klonen + CLI-Start) der Aufgabendetailansicht (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus (SOFTWARESCHMIEDE_TEST_DB_PATH gesetzt) steht ausschließlich das LocalDirectoryPlugin
///   als SCM-Plugin zur Verfügung (kein GitHub-Plugin), siehe PluginManager.IsAllowedInTestMode.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Aufgabe im Status "Neu" mit "Starten" auf "Gestartet" wechseln.
    /// Erster Versuch schlägt erwartungsgemäß fehl, da ConfirmGitInitInSourceDirectory nicht gesetzt ist
    /// (InSourceDirectory-Modus erfordert explizite Bestätigung für git init).
    /// Nach Korrektur der Einstellung gelingt der zweite Versuch: Repository wird geklont,
    /// Status wechselt auf "Gestartet" und die CLI wird gestartet (CLI-Panel mit Stoppen-Button sichtbar).
    /// </summary>
    protected void AufgabeStarten_KlontRepositoryUndStartetCli_E2E(Window mainWindow)
    {
        const string repositoryFolderName = "AufgabeStarten-Repo";
        const string projektName = "AufgabeStarten-Projekt";
        const string pluginName = "Softwareschmiede.KiSimulator";

        new WindowsCredentialStore().DeleteCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory");

        // InSourceDirectory-Modus ohne vorinitialisiertes Git-Repository: Der erste Startversuch muss
        // an der fehlenden ConfirmGitInitInSourceDirectory-Bestätigung scheitern (siehe unten).
        var sourceDirectory = CreateLocalSourceDirectory(repositoryFolderName, initializeGitRepository: false);

        // ForceClose(recurseToDashboard: true) gibt laut Vertrag immer die ursprüngliche Instanz zurück
        // (siehe BaseWindowView.ForceClose), nicht die tatsächlich erreichte Dashboard-Ansicht - deshalb
        // wird der Rückgabewert verworfen und CurrentView() danach erneut abgefragt (siehe
        // ForceClose_MitRekursion_SchliesstBisDashboard_E2E für dasselbe Muster).
        mainWindow.CurrentView().ForceReset();
        var dashboard = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        var settings = dashboard.Menu.NavigateToSettings();
        dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory);

        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject(projektName);
        var projectDetail = projectList.OpenProject(projektName);

        projectDetail = new RepositoryAssignDialogView(mainWindow)
            .ForceShow()
            .SelectFirstRepository()
            .Confirm();

        var taskDetail = projectDetail.CreateTask();

        // Erster Versuch: ConfirmGitInitInSourceDirectory ist nicht gesetzt → Fehlermeldung erwartet
        taskDetail.Start(pluginName, fuerProjektVerwenden: false);
        var errorView = Assert.IsType<ErrorView>(mainWindow.CurrentView());
        Assert.False(string.IsNullOrWhiteSpace(errorView.GetErrorMessage()));

        // Einstellung korrigieren: ConfirmGitInitInSourceDirectory auf true setzen
        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");

        // Zweiter Versuch: Plugin-Dialog erneut bedienen, diesmal ist die Bestätigung gesetzt
        taskDetail.Start(pluginName, fuerProjektVerwenden: false);

        // Nach erfolgreichem Start: CLI-Panel sichtbar (Stoppen-Button + Status "Gestartet"), kein Fehler mehr
        taskDetail.WaitForCliRunning();
        Assert.False(new ErrorView(mainWindow).IsVisible);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetailAfterTask = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailAfterTask.DeleteProject();
    }
}
