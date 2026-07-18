using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Anzeige des Plugin-Auswahl-Dialogs beim Starten einer Aufgabe ohne
/// gespeichertes KI-Plugin (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_PluginSelectionDialog : WpfTestBase
{
    /// <summary>
    /// Szenario: Beim ersten Start einer Aufgabe ohne gespeichertes Plugin (kein Aufgaben-,
    /// Projekt- oder globaler Default) wird der Plugin-Auswahl-Dialog angezeigt. Zunächst wird die
    /// Auswahl über "Abbrechen" verworfen (Phase Abbrechen); anschließend wird derselbe Start erneut
    /// versucht, ein Plugin ausgewählt und mit "OK" bestätigt (Phase OK).
    /// Prüft: Im Abbrechen-Pfad wird der Start-Ablauf nicht fortgesetzt (Aufgabe bleibt im Status
    /// "Neu", Edit-Panel weiterhin sichtbar). Im OK-Pfad wird nach Auswahl und Bestätigung der
    /// kombinierte Start-Ablauf fortgesetzt (CLI startet).
    /// </summary>
    [SkippableFact]
    public void PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("PluginDialog-Repo", "PluginDialog-Projekt");

        // Phase Abbrechen
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var abbrechenDialog = WaitForWindow("KI-Plugin auswählen", Medium);

        var abbrechenButton = WaitForElement(abbrechenDialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Edit-Panel weiterhin sichtbar (Status nach wie vor "Neu")
        var editTitel = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        Assert.NotNull(editTitel);

        var stoppenButtonNachAbbrechen = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("CliStoppen").And(cf.ByControlType(ControlType.Button)));
        Assert.Null(stoppenButtonNachAbbrechen);

        // Phase OK
        var startenButtonErneut = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButtonErneut.AsButton().Click();

        var okDialog = WaitForWindow("KI-Plugin auswählen", Medium);
        var pluginAuswahlBox = WaitForElement(okDialog, cf => cf.ByName("PluginAuswahl"), Short);
        SelectComboBoxItemByClick(pluginAuswahlBox, "Softwareschmiede.KiSimulator", Short);

        var okButton = WaitForElement(okDialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();

        // Nach Bestätigung: kombinierter Start-Ablauf läuft weiter, CLI startet
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);
    }
}
