namespace Softwareschmiede.Tests.E2E;

public partial class End2EndTest
{
    /// <summary>Exercises SCM-adjacent UI workflows through the explicit test-database pipe backend; this is not ConPTY coverage.</summary>
    [Fact]
    [Trait("Scenario", "ScmRegressionSimulator")]
    public async Task Simulator_PluginwechselProjektstandardAufgabenstartUndNavigation_E2E()
    {
        var app = LaunchApp(true);
        try
        {
            var mainWindow = WarteAufEchtesHauptfenster(app);
            Assert.False(string.IsNullOrWhiteSpace(TestDbPath));
            await AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E(mainWindow);
            AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E(mainWindow);
            PluginProjectDefault_SpeichernUndAutomatischeUebernahmeInFolgeaufgabe_E2E(mainWindow);
            PluginAuswahlAbbrechenOkUndWechsel_E2E(mainWindow);
            AufgabeStarten_KlontRepositoryUndStartetCli_E2E(mainWindow);
        }
        finally
        {
            app.Close();
        }
    }
}
