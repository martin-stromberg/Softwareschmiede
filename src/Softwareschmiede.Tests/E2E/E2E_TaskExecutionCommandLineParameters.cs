using Softwareschmiede.Infrastructure.Services;

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
    protected void AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E(FlaUI.Core.AutomationElements.Window mainWindow)
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

            StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

            var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
            Assert.NotNull(stoppenButton);
            NavigateBackFromTaskToProject(mainWindow);
            DeleteCurrentProject(mainWindow);
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
