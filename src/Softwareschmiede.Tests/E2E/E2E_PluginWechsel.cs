using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für den Plugin-Wechsel bei laufender CLI über den "Plugin ändern"-Button (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung;
///   als KI-Plugins sind u.a. Softwareschmiede.KiSimulator und Softwareschmiede.ClaudeCli verfügbar.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_PluginWechsel : WpfTestBase
{
    /// <summary>
    /// Szenario: Aufgabe wird mit dem KI-Simulator-Plugin gestartet. Über "Plugin ändern" wird im
    /// Dialog ein anderes Plugin gewählt. Prüft: Der laufende CLI-Prozess wird gestoppt und mit dem
    /// neuen Plugin neu gestartet (CLI-Panel bleibt nach dem Wechsel sichtbar, Status bleibt "Gestartet").
    /// </summary>
    [Fact]
    public void PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("PluginWechsel-Repo", "PluginWechsel-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Kurze Stabilisierungspause nach Schließen des ersten Dialogs, damit die UIA-Elemente
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
