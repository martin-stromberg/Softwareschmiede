using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Regressionstest: Gespeicherte CommandLineParameters eines Plugins dürfen den Start einer
/// Aufgabe mit einem anderen Plugin (KI Simulator) nicht beeinträchtigen.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Speichert CommandLineParameters für das Codex-Plugin im Credential Store, startet dann eine
    /// Aufgabe mit dem KI Simulator und prüft, dass die Aufgabe trotzdem korrekt startet.
    /// </summary>
    protected void AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E(Window mainWindow)
    {
        var credentialStore = new WindowsCredentialStore();
        var backup = credentialStore.GetCredential("Softwareschmiede.Codex.CommandLineParameters");
        try
        {
            credentialStore.SetCredential(
                "Softwareschmiede.Codex.CommandLineParameters", "--test-regression-flag");
            ConfirmLocalDirectoryGitInitInSourceDirectory();

            SetupProjectMitNeuerAufgabe(
                mainWindow,
                "CmdParamsRegressionRepo",
                "CmdParams-Regressions-Projekt");

            var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
            taskDetail.WaitForCliRunning();

            taskDetail.ForceClose(recurseToDashboard: false);
            var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
            projectDetail.DeleteProject();
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(backup))
            {
                credentialStore.DeleteCredential("Softwareschmiede.Codex.CommandLineParameters");
            }
            else
            {
                credentialStore.SetCredential(
                    "Softwareschmiede.Codex.CommandLineParameters", backup);
            }
        }
    }
}
