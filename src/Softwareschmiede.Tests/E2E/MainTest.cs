using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// Führt die gebündelten FlaUI-E2E-Szenarien der Softwareschmiede aus.
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public partial class End2EndTest : WpfTestBase
{
    /// <summary>
    /// Führt UI-Szenarien ohne ConPTY-Abhängigkeit in einem gemeinsamen App-Lifecycle aus.
    /// </summary>
    [Fact]
    public async Task RunGeneralTests()
    {
        var app = LaunchApp(true);
        var mainWindow = app.GetMainWindow(Automation, Long)!;

        AppStarten_ZeigtVersionsTextInFusszeile_E2E(mainWindow);
        Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E(mainWindow);
        IdePluginSettings_AktivierungValidierungUndReihenfolge_E2E(mainWindow);
        await RepositoryZuweisung(mainWindow);
        Todo_ErstellenAbhakenLoeschenUndAbschlussValidierung_E2E(mainWindow);
        TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E(mainWindow);
        CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E(mainWindow);

        app.Close();
    }

    /// <summary>
    /// Führt ConPTY-abhängige UI-Szenarien in einem gemeinsamen App-Lifecycle aus.
    /// </summary>
    [SkippableFact]
    public async Task RunConPtyTests()
    {
        SkipWennConPtyNichtVerfuegbar();

        var app = LaunchApp(true);
        var mainWindow = app.GetMainWindow(Automation, Long)!;

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
        ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E(mainWindow);
        AufgabeOeffnen_NachStoppen_StartetCliNichtAutomatischErstExplizit_E2E(mainWindow);
        AufgabeStarten_KlontRepositoryUndStartetCli_E2E(mainWindow);
        CliPanel_BleibtSichtbarNachBeendigung_E2E(mainWindow);
        SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E(mainWindow);
        await DateiExplorer_KlapptVerzeichnisZuUndErneutAuf_LaedtKinderNach_E2E(mainWindow);
        await DateiExplorer_KlapptVerzeichnisAufUndLaedtKinderNach_E2E(mainWindow);

        app.Close();
    }
}
