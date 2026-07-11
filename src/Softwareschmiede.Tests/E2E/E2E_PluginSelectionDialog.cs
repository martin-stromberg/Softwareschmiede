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
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_PluginSelectionDialog : WpfTestBase
{
    /// <summary>
    /// Szenario: Beim ersten Start einer Aufgabe ohne gespeichertes Plugin (kein Aufgaben-,
    /// Projekt- oder globaler Default) wird der Plugin-Auswahl-Dialog angezeigt.
    /// Prüft: Dialog erscheint mit Dropdown der verfügbaren KI-Plugins; nach Auswahl und OK
    /// wird der kombinierte Start-Ablauf fortgesetzt (CLI startet).
    /// </summary>
    [SkippableFact]
    public void StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E()
    {
        SkipWennConPtyNichtVerfuegbar();
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("PluginDialog-Repo", "PluginDialog-Projekt");

        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        // Plugin-Auswahl-Dialog erscheint als separates Fenster
        var dialog = WaitForWindow("KI-Plugin auswählen", Medium);
        Assert.NotNull(dialog);

        var pluginAuswahlBox = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Short);
        Assert.NotNull(pluginAuswahlBox);

        // Dropdown enthält mindestens den KI-Simulator (im Test-Modus immer verfügbar)
        SelectComboBoxItemByClick(pluginAuswahlBox, "Softwareschmiede.KiSimulator", Short);

        var okButton = WaitForElement(dialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();

        // Nach Bestätigung: kombinierter Start-Ablauf läuft weiter, CLI startet
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);
    }

    /// <summary>
    /// Szenario: Im Plugin-Auswahl-Dialog wird die Auswahl über "Abbrechen" verworfen.
    /// Prüft: Dialog schließt sich, der Start-Ablauf wird nicht fortgesetzt
    /// (Aufgabe bleibt im Status "Neu", Edit-Panel weiterhin sichtbar).
    /// </summary>
    [Fact]
    public void PluginAuswahlAbbrechen_StartetNichtUndBleibtImStatusNeu_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("PluginDialog-Abbrechen-Repo", "PluginDialog-Abbrechen-Projekt");

        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
        startenButton.AsButton().Click();

        var dialog = WaitForWindow("KI-Plugin auswählen", Medium);

        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        // Edit-Panel weiterhin sichtbar (Status nach wie vor "Neu")
        var editTitel = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
        Assert.NotNull(editTitel);

        var stoppenButton = mainWindow.FindFirstDescendant(cf =>
            cf.ByName("CliStoppen").And(cf.ByControlType(ControlType.Button)));
        Assert.Null(stoppenButton);
    }
}
