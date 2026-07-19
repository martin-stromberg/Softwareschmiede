using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Anzeige des Plugin-Auswahl-Dialogs beim Starten einer Aufgabe ohne
/// gespeichertes KI-Plugin sowie für den Plugin-Wechsel bei laufender CLI (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung;
///   als KI-Plugins sind u.a. Softwareschmiede.KiSimulator und Softwareschmiede.ClaudeCli verfügbar.
///
/// Konsolidierung (Issue #153): Die "OK"-Phase des Auswahl-Dialogs endet bereits mit laufender CLI
/// (Softwareschmiede.KiSimulator) - genau die Vorbedingung, die der Plugin-Wechsel-Test benötigt.
/// Beide laufen daher als Phasen an derselben Aufgabe in einem gemeinsamen App-Lifecycle statt zwei
/// eigenständiger App-Starts.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_PluginAuswahlUndWechsel : WpfTestBase
{
    /// <summary>
    /// Führt die Plugin-Auswahl (Abbrechen, dann OK) und den anschließenden Plugin-Wechsel bei
    /// laufender CLI als zwei Phasen an derselben Aufgabe aus.
    /// </summary>
    [SkippableFact]
    public void PluginAuswahlAbbrechenOkUndWechsel_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("PluginDialog-Repo", "PluginDialog-Projekt");

        PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E(mainWindow);
        PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Beim ersten Start einer Aufgabe ohne gespeichertes Plugin (kein Aufgaben-,
    /// Projekt- oder globaler Default) wird der Plugin-Auswahl-Dialog angezeigt. Zunächst wird die
    /// Auswahl über "Abbrechen" verworfen (Phase Abbrechen); anschließend wird derselbe Start erneut
    /// versucht, ein Plugin ausgewählt und mit "OK" bestätigt (Phase OK).
    /// Prüft: Im Abbrechen-Pfad wird der Start-Ablauf nicht fortgesetzt (Aufgabe bleibt im Status
    /// "Neu", Edit-Panel weiterhin sichtbar). Im OK-Pfad wird nach Auswahl und Bestätigung der
    /// kombinierte Start-Ablauf fortgesetzt (CLI startet).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der neu angelegten Aufgabe im Edit-Panel.</param>
    private void PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E(AutomationElement mainWindow)
    {
        // Phase Abbrechen
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var abbrechenDialog = WaitForWindow("KI-Plugin auswählen", Medium);

        var abbrechenButton = WaitForElement(abbrechenDialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Edit-Panel weiterhin sichtbar (Status nach wie vor "Neu")
        WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);

        var stoppenButtonNachAbbrechen = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("CliStoppen").And(cf.ByControlType(ControlType.Button)));
        Assert.Null(stoppenButtonNachAbbrechen);

        // Phase OK
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach Bestätigung: kombinierter Start-Ablauf läuft weiter, CLI startet
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
    }

    /// <summary>
    /// Szenario: Über "Plugin ändern" wird im Dialog ein anderes Plugin gewählt, während die CLI
    /// bereits läuft. Prüft: Der laufende CLI-Prozess wird gestoppt und mit dem neuen Plugin neu
    /// gestartet (CLI-Panel bleibt nach dem Wechsel sichtbar, Status bleibt "Gestartet").
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit bereits laufender CLI (Softwareschmiede.KiSimulator).</param>
    private void PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E(AutomationElement mainWindow)
    {
        // Kurze Stabilisierungspause nach Schließen des vorherigen Dialogs, damit die UIA-Elemente
        // wieder einen gültigen Klickpunkt liefern (sonst NoClickablePointException möglich).
        Thread.Sleep(500);

        // Plugin ändern: Dialog mit aktuellem Plugin vorselektiert anzeigen
        var pluginAendernButton = WaitForElement(mainWindow, cf => cf.ByName("PluginAendern"), Short);
        pluginAendernButton.AsButton().Click();

        var wechselDialog = WaitForWindow("KI-Plugin auswählen", Medium);
        var wechselPluginAuswahlBox = WaitForElement(wechselDialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(wechselPluginAuswahlBox, "Softwareschmiede.ClaudeCli", Short);

        var wechselOkButton = WaitForElement(wechselDialog, cf => cf.ByName("OK"), Short);
        wechselOkButton.AsButton().Click();

        // Nach dem Wechsel: alter Prozess gestoppt, neuer CLI-Prozess läuft (Stoppen-Button weiterhin sichtbar)
        var stoppenButtonNachWechsel = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButtonNachWechsel);

        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);

        var fehlerMeldung = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerMeldung);
    }
}
