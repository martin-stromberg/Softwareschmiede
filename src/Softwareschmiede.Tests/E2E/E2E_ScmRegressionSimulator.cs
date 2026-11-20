namespace Softwareschmiede.Tests.E2E;

public partial class End2EndTest
{
    /// <summary>Übt SCM-nahe UI-Abläufe über das explizite Test-Datenbank-Pipe-Backend; dies ist keine ConPTY-Abdeckung.</summary>
    [Fact]
    [Trait("Scenario", "ScmRegressionSimulator")]
    public async Task Simulator_PluginswitchProjectdefaultWorkitemstartAndNavigation_E2E()
    {
        var app = LaunchApp(true);
        try
        {
            var mainWindow = WaitOnRealMainwindow(app);
            Assert.False(string.IsNullOrWhiteSpace(TestDbPath));
            await StartWorkItem_WithConfiguredWorkingDirectory_CliStartsSuccessful_E2E(mainWindow);
            WorkItemSwitchOverSidebar_ShowsNewWorkItemWithOwnCli_E2E(mainWindow);
            PluginProjectDefault_SaveAndAutomaticApplicationInFollowuptask_E2E(mainWindow);
            SwitchPluginSelectionCancelOkAnd_E2E(mainWindow);
            StartWorkItem_ClonesRepositoryAndStartsCli_E2E(mainWindow);
        }
        finally
        {
            app.Close();
        }
    }
}
