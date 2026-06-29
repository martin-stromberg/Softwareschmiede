using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Regressionstest: Gespeicherte CommandLineParameters eines Plugins dürfen den Start einer
/// Aufgabe mit einem anderen Plugin (KI Simulator) nicht beeinträchtigen.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_TaskExecutionCommandLineParameters : WpfTestBase
{
    /// <summary>
    /// Speichert CommandLineParameters für das Codex-Plugin im Credential Store, startet dann eine
    /// Aufgabe mit dem KI Simulator und prüft, dass die Aufgabe trotzdem korrekt startet.
    /// </summary>
    [Fact]
    public void AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E()
    {
        new WindowsCredentialStore().SetCredential(
            "Softwareschmiede.Codex.CommandLineParameters", "--test-regression-flag");
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe(
            "CmdParamsRegressionRepo",
            "CmdParams-Regressions-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        new WindowsCredentialStore().DeleteCredential("Softwareschmiede.Codex.CommandLineParameters");
    }
}
