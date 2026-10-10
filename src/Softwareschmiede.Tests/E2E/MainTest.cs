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
    /// <summary>Prüft Start, autonome Aufgaben und Einstellungen in einem isolierten App-Lifecycle.</summary>
    [Fact]
    public Task General_BasisUndAutonomeAufgaben_E2E()
        => MitFrischerAppAsync(async mainWindow =>
        {
            AppStarten_ZeigtVersionsTextInFusszeile_E2E(mainWindow);
            await AutonomAufgabeInitialisierung_DialogErstelltArbeitsverzeichnisUndZeigtDetailAnsicht_E2E(mainWindow);
            await AutonomAufgabeAgentExecution_StartUnteragentUndSessionPause_E2E(mainWindow);
            Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E(mainWindow);
            Einstellungen_AutonomAufgabenFeatureFlagToggle_PersistiertWert_E2E(mainWindow);
            IdePluginSettings_AktivierungValidierungUndReihenfolge_E2E(mainWindow);
        });

    /// <summary>Prüft Repository-, Aufgaben- und Terminal-Fallback-Szenarien isoliert.</summary>
    [Fact]
    public Task General_RepositoryUndAufgaben_E2E()
        => MitFrischerAppAsync(async mainWindow =>
        {
            await RepositoryZuweisung(mainWindow);
            Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E(mainWindow);
            TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E(mainWindow);
            await AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E(mainWindow);
            await SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E(mainWindow);
            await TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E(mainWindow);
            CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E(mainWindow);
        });

    /// <summary>Prüft die Wiedergabeaufzeichnung isoliert, damit Hänger direkt zuordenbar bleiben.</summary>
    [Fact]
    public Task General_Konsolenwiedergabe_E2E()
        => MitFrischerAppAsync(KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E);

    /// <summary>Prüft View- und Dialognavigation in einem eigenen App-Lifecycle.</summary>
    [Fact]
    public Task General_ViewNavigation_E2E()
        => MitFrischerAppAsync(async mainWindow =>
        {
            ViewPatternHappyPath_NavigiertUndErstelltKorrekt_E2E(mainWindow);
            AnsichtenErkennung_LiefertKorrekteViewTypen_E2E(mainWindow);
            MenueNavigation_WechseltZwischenAnsichten_E2E(mainWindow);
            ForceShow_NavigiertKorrektZuAnsicht_E2E(mainWindow);
            ForceClose_OhneRekursion_SchliesstNurEineEbene_E2E(mainWindow);
            ForceClose_MitRekursion_SchliesstBisDashboard_E2E(mainWindow);
            DialogErkennung_LiefertKorrekteDialogViewTypen_E2E(mainWindow);
            UnbekannteAnsicht_WirftAussagekraeftigeException_E2E(mainWindow);
            await FehlerAnsichtErkennung_ZeigtFehlermeldung_E2E(mainWindow);
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
        await Settings_AllModesAndPrereleasesPersist();
        await Startup_ModesDriveUpdatePipeline();
        await PrereleaseCheckbox_SelectsMatchingAsset();
        await SavedChangesInvalidatePreviousOffer();
        await Startup_NoUpdateOrUncheckableRemainsUsable();
        await Startup_SafetyCancelAndErrorsRemainUsable();
        await Startup_IsOnceAndCommandsStayBlocked();
    }

    private async Task MitFrischerAppAsync(Func<Window, Task> szenario)
    {
        var app = LaunchApp(true);
        try
        {
            await szenario(WarteAufEchtesHauptfenster(app));
        }
        finally
        {
            app.Close();
        }
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
            await ReplayText_MarkAndCopyViaMouse(WarteAufEchtesHauptfenster(app));
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
            await ReplayText_CopyShortcutWithFocusedSelection(WarteAufEchtesHauptfenster(app));
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>Gezielt ausführbarer Pflichtnachweis E-01 gegen eine echte ConPTY-Session.
    /// Er isoliert Auswahl, sichtbares Overlay und Kopieren vom großen Lifecycle-Sammeltest,
    /// damit ein früher Fehler in einer anderen Session-Phase den Nachweis nicht verdeckt.</summary>
    [SkippableFact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "TextCopyLiveTerminal")]
    public async Task TerminalText_LiveMarkCopyAndKeepSelection_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WarteAufEchtesHauptfenster(app);
            ConfirmLocalDirectoryGitInitInSourceDirectory();
            SetupProjectMitNeuerAufgabe(mainWindow, "Live-TextCopy-Repo", "Live-TextCopy-Projekt");
            var taskDetail = new Views.TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

            await ConPtyStart_ZeigtTerminalPanelMitStoppenButtonUndBanner_E2E(mainWindow, taskDetail);
            await TerminalText_LiveMarkCopyAndKeepSelection(mainWindow, taskDetail);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>Prüft das Ab- und Wiederanbinden einer laufenden ConPTY-Session isoliert.</summary>
    [SkippableFact]
    [Trait("Category", "E2E")]
    [Trait("Scenario", "ConPtySessionReattach")]
    public async Task ConPty_SessionReattach_KeineDoppelausgabe_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WarteAufEchtesHauptfenster(app);
            ConfirmLocalDirectoryGitInitInSourceDirectory();
            SetupProjectMitNeuerAufgabe(mainWindow, "ConPty-Reattach-Repo", "ConPty-Reattach-Projekt");
            var taskDetail = new Views.TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
            await ConPtyStart_ZeigtTerminalPanelMitStoppenButtonUndBanner_E2E(mainWindow, taskDetail);
            await ConPtySessionReattach_WegUndZurueck_KeineDoppelausgabe_E2E(taskDetail);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>
    /// Führt ConPTY-abhängige UI-Szenarien in einem gemeinsamen App-Lifecycle aus.
    /// </summary>
    [SkippableFact]
    public async Task ConPty_LifecycleUndReplay_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();

        var app = LaunchApp(true);
        try
        {
            var mainWindow = WarteAufEchtesHauptfenster(app);
            await ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E(mainWindow);
        }
        finally
        {
            app.Close();
        }
    }

    /// <summary>
    /// Führt die verbleibenden ConPTY-abhängigen UI-Szenarien in einem gemeinsamen App-Lifecycle aus.
    /// Der vollständige Terminal-Lifecycle läuft bewusst separat in
    /// <see cref="ConPty_LifecycleUndReplay_E2E"/>, damit sein lang laufender UI- und Prozesszustand
    /// nicht von den unabhängigen Navigationsszenarien beeinflusst wird.
    /// </summary>
    [SkippableFact]
    public async Task ConPty_WeitereLebenszyklusSzenarien_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();

        var app = LaunchApp(true);
        var mainWindow = WarteAufEchtesHauptfenster(app);

        ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E(mainWindow);
        await AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E(mainWindow);
        await VerzeichnisAktionen_ArbeitsverzeichnisUndIdeOeffnen_E2E(mainWindow);
        await VerzeichnisAktionen_KonfiguriertesArbeitsverzeichnisWirdAufgeloest_E2E(mainWindow);
        await IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E(mainWindow);
        AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E(mainWindow);
        AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E(mainWindow);
        PluginProjectDefault_SpeichernUndAutomatischeUebernahmeInFolgeaufgabe_E2E(mainWindow);
        PluginAuswahlAbbrechenOkUndWechsel_E2E(mainWindow);
        PluginAktivierung_ValidierungPersistenzUndSinglePluginVerhalten_E2E(mainWindow);
        DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E(mainWindow);
        AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E(mainWindow);
        AufgabeOeffnen_NachStoppen_StartetCliNichtAutomatischErstExplizit_E2E(mainWindow);
        AufgabeStarten_KlontRepositoryUndStartetCli_E2E(mainWindow);
        CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E(mainWindow);
        CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E(mainWindow);
        CliPanel_BleibtSichtbarNachBeendigung_E2E(mainWindow);
        SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E(mainWindow);
        await DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E(mainWindow);
        await DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E(mainWindow);

        app.Close();
    }
}
