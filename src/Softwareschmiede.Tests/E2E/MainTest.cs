using FlaUI.Core.AutomationElements;
using Softwareschmiede.App.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Führt die gebündelten FlaUI-E2E-Szenarien der Softwareschmiede aus.
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public partial class End2EndTest : WpfTestBase
{
    /// <summary>Prüft Start und Settings in einem isolierten App-Lifecycle.</summary>
    [Fact]
    public Task General_BaseAndSettings_E2E()
        => WithFreshAppAsync(mainWindow =>
        {
            StartApp_ShowsVersionsTextInFooter_E2E(mainWindow);
            Settings_SaveCodexAsDefaultAiPluginAndExecutablePath_PersistedBoth_E2E(mainWindow);
            Settings_GeneralContainsOnlyExistingSection_E2E(mainWindow);
            Settings_RemoteStatusSection_TogglesAndPersists_E2E(mainWindow);
            Settings_ExecutionLimitGlobalAndPerPlugin_E2E(mainWindow);
            Settings_ConfigureIssuePreparation_E2E(mainWindow);
            IdePluginSettings_ActivationValidationAndOrder_E2E(mainWindow);
            return Task.CompletedTask;
        });

    /// <summary>Prüft Repository-, Aufgaben- und Terminal-Fallback-Szenarien isoliert.</summary>
    [Fact]
    public Task General_RepositoryAndWorkItems_E2E()
        => WithFreshAppAsync(async mainWindow =>
        {
            await RepositoryAssignment(mainWindow);
            Todo_CreateCheckoffDeleteAndCompletionValidation_E2E(mainWindow);
            TaskDetail_ShowsData_Back_AndOpenWindowspanning_E2E(mainWindow);
            await PauseWorkItem_DialogCountdownDimmingAndLift_E2E(mainWindow);
            mainWindow = await WorkItemsList_CompactTileSymbolAndModeswitch_E2E(mainWindow);
            await SessionLimit_MarkerPauseLogAndUpdateSafe_E2E(mainWindow);
            await TerminalFallbackDiagnostics_PipeFallbackAndRequiresPtyError_E2E(mainWindow);
            CommandLineParameter_TextBoxSavesValueAndHelpDialogWorks_E2E(mainWindow);
        });

    /// <summary>Prüft die Wiedergabeaufzeichnung isoliert, damit Hänger direkt zuordenbar bleiben.</summary>
    [Fact]
    public Task General_Konsolenwiedergabe_E2E()
        => WithFreshAppAsync(ConsoleTestwindow_OpensLoadsRecordingAndPlaysFrom_E2E);

    /// <summary>Prüft den Auto-Shutdown-Fluss (Issue 6) in einem eigenen App-Lifecycle plus Neustart.</summary>
    [Fact]
    public Task General_AutoShutdown_E2E()
        => WithFreshAppAsync(AutoShutdown_CountdownCancelRecordingAndPersistence_E2E);

    /// <summary>Prüft View- und Dialognavigation in einem eigenen App-Lifecycle.</summary>
    [Fact]
    public Task General_ViewNavigation_E2E()
        => WithFreshAppAsync(async mainWindow =>
        {
            ViewPatternHappyPath_NavigatesAndCreatesCorrect_E2E(mainWindow);
            ViewDetection_ReturnsCorrectViewTypes_E2E(mainWindow);
            MenuNavigation_SwitchesBetweenView_E2E(mainWindow);
            ForceShow_NavigatesCorrectToView_E2E(mainWindow);
            ForceClose_WithoutRecursion_ClosesOnlyALevel_E2E(mainWindow);
            ForceClose_WithRecursion_ClosesUntilDashboard_E2E(mainWindow);
            DialogDetection_ReturnsCorrectDialogViewTypes_E2E(mainWindow);
            UnknownView_ThrowsMeaningfulException_E2E(mainWindow);
            await ErrorViewDetection_ShowsErrormessage_E2E(mainWindow);
        });

    /// <summary>Prüft die isolierten Update-Fixture-Smokes separat.</summary>
    [Fact]
    public async Task General_UpdateFixtureSmoke_E2E()
    {
        await Fixture_UsesIsolatedRealUpdatePipeline();
        await Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization();
    }

    /// <summary>Prüft die sieben isolierten Update-Startvarianten separat.</summary>
    [Fact]
    public async Task General_UpdateStartvarianten_E2E()
    {
        await Settings_AllModesAndUpdatePathPersist();
        await Startup_ModesDriveUpdatePipeline();
        await UpdatePath_SelectsExclusiveChannelAsset();
        await SavedChangesInvalidatePreviousOffer();
        await Startup_NoUpdateOrUncheckableRemainsUsable();
        await Startup_SafetyCancelAndErrorsRemainUsable();
        await Startup_IsOnceAndCommandsStayBlocked();
    }

    private async Task WithFreshAppAsync(Func<Window, Task> scenario)
    {
        var app = LaunchApp(true);
        try
        {
            await scenario(WaitOnRealMainwindow(app));
        }
        finally
        {
            app.Close();
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int maxAttempts = 150)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Bedingung wurde innerhalb des Zeitlimits nicht erfüllt.");
    }

    /// <summary>Gezielt ausführbarer Pflichtnachweis E-02 für den Mauspfad. Er startet nur die
    /// dafür nötige Test-App statt des gesamten General-E2E-Bündels.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "TextCopyReplayMouse")]
    public async Task ReplayText_MarkAndCopyViaMouse_E2E()
    {
        var app = LaunchApp(true);
        try
        {
            await ReplayText_MarkAndCopyViaMouse(WaitOnRealMainwindow(app));
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>Gezielt ausführbarer E-02-Nachweis für Fokus und Kopier-Shortcut auf einer
    /// bestehenden Auswahl. Die Umschalt-Pfeil-Auswahl ist als WPF-Integrationstest getrennt,
    /// weil die Windows-Injektion im E2E-Testdesktop Modifier nicht zuverlässig transportiert.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "TextCopyReplayKeyboard")]
    public async Task ReplayText_CopyShortcutWithFocusedSelection_E2E()
    {
        var app = LaunchApp(true);
        try
        {
            await ReplayText_CopyShortcutWithFocusedSelection(WaitOnRealMainwindow(app));
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>Gezielt ausführbarer Pflichtnachweis E-01 gegen eine echte ConPTY-Session.
    /// Er isoliert Auswahl, sichtbares Overlay und Kopieren vom großen Lifecycle-Sammeltest,
    /// damit ein früher Fehler in einer anderen Session-Phase den Nachweis nicht verdeckt.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "TextCopyLiveTerminal")]
    public async Task TerminalText_LiveMarkCopyAndKeepSelection_E2E()
    {
        SkipWhenConPtyNotAvailable();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WaitOnRealMainwindow(app);
            ConfirmLocalDirectoryGitInitInSourceDirectory();
            SetupProjectWithNewWorkItem(mainWindow, "Live-TextCopy-Repo", "Live-TextCopy-Projekt");
            var taskDetail = new Views.TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);

            await ConPtyStart_ShowsTerminalPanelWithStopButtonAndBanner_E2E(mainWindow, taskDetail);
            await TerminalText_LiveMarkCopyAndKeepSelection(mainWindow, taskDetail);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>Prüft das Ab- und Wiederanbinden einer laufenden ConPTY-Session isoliert.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "ConPtySessionReattach")]
    public async Task ConPty_SessionReattach_NoDoubleoutput_E2E()
    {
        SkipWhenConPtyNotAvailable();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WaitOnRealMainwindow(app);
            ConfirmLocalDirectoryGitInitInSourceDirectory();
            SetupProjectWithNewWorkItem(mainWindow, "ConPty-Reattach-Repo", "ConPty-Reattach-Projekt");
            var taskDetail = new Views.TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);
            await ConPtyStart_ShowsTerminalPanelWithStopButtonAndBanner_E2E(mainWindow, taskDetail);
            await ConPtySessionReattach_PathAndBack_NoDoubleoutput_E2E(taskDetail);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>
    /// Führt ConPTY-abhängige UI-Szenarien in einem gemeinsamen App-Lifecycle aus.
    /// </summary>
    [Fact]
    public async Task ConPty_LifecycleAndReplay_E2E()
    {
        SkipWhenConPtyNotAvailable();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WaitOnRealMainwindow(app);
            await ConPtyLifecycle_StartResizeKeyboardinputAndProcessend_E2E(mainWindow);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>
    /// Führt die verbleibenden ConPTY-abhängigen UI-Szenarien in einem gemeinsamen App-Lifecycle aus.
    /// Der vollständige Terminal-Lifecycle läuft bewusst separat in
    /// <see cref="ConPty_LifecycleAndReplay_E2E"/>, damit sein lang laufender UI- und Prozesszustand
    /// nicht von den unabhängigen Navigationsszenarien beeinflusst wird.
    /// </summary>
    [Fact]
    public async Task ConPty_FurtherLifecycleScenarios_E2E()
    {
        SkipWhenConPtyNotAvailable();

        var app = LaunchApp(true);
        var mainWindow = WaitOnRealMainwindow(app);

        await ScheduledPrompt_AfterPlan_ShowsQueueStatus_E2E(mainWindow);
        await StartWorkItem_WithConfiguredWorkingDirectory_CliStartsSuccessful_E2E(mainWindow);
        await DirectoryAction_OpenWorkingDirectoryAndIde_E2E(mainWindow);
        await DirectoryAction_ConfiguredWorkingDirectoryWillResolved_E2E(mainWindow);
        await IdeSelection_NoEntrypointsAndDropdownCancel_E2E(mainWindow);
        WorkItemSwitchOverSidebar_ShowsNewWorkItemWithOwnCli_E2E(mainWindow);
        StartWorkItem_WithCodexCommandLineParameterInStore_AiSimulatorStartsCorrect_E2E(mainWindow);
        PluginProjectDefault_SaveAndAutomaticApplicationInFollowuptask_E2E(mainWindow);
        SwitchPluginSelectionCancelOkAnd_E2E(mainWindow);
        PluginActivation_ValidationPersistenceAndSinglePluginBehavior_E2E(mainWindow);
        FileExplorer_ShowsTreeAndModeButtons_AndSwitchesToInfoAndBack_E2E(mainWindow);
        WorkItemCreate_SavePersisted_AndCancelDiscardsTitle_E2E(mainWindow);
        OpenWorkItem_StopAfter_StartsCliNotAutomaticFirstExplicit_E2E(mainWindow);
        StartWorkItem_ClonesRepositoryAndStartsCli_E2E(mainWindow);
        CliRawExport_CreatesRawFileWithCliOutput_HappyPath_E2E(mainWindow);
        CliRawExport_CancelCreatesNoFileAndNoErrorbanner_E2E(mainWindow);
        CliPanel_RemainsVisibleAfterTermination_E2E(mainWindow);
        SidebarTile_UpdatesStatusAutomatic_WithoutManualReload_E2E(mainWindow);
        await FileExplorer_WorksDirectoryToAndAgainOn_LoadsChildrenAfter_E2E(mainWindow);
        await FileExplorer_WorksDirectoryOnAndLoadsChildrenAfter_E2E(mainWindow);

        app.Close();
    }
}
