using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die Sichtbarkeit des CLI-Panels nach Beendigung der Ausführung (Korrektur des
/// Arbeitsablaufs). Vor der Korrektur wurde das CLI-Panel ausgeblendet, sobald AusfuehrungsStatus
/// auf "Beendet" wechselte, obwohl die Aufgabe selbst noch im Status "Gestartet" oder "Wartend" war -
/// der Benutzer konnte die letzte CLI-Ausgabe nicht mehr einsehen und die CLI nicht manuell neu starten.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Aufgabe starten, CLI läuft, CLI manuell stoppen (AusfuehrungsStatus wechselt auf
    /// "Beendet", Aufgabenstatus bleibt "Gestartet"). Prüft: Das CLI-Panel (CliViewButton, gebunden an
    /// ShowCliPanel) bleibt nach dem Stoppen sichtbar, die letzte CLI-Ausgabe (TerminalConsole) bleibt
    /// einsehbar, und der Button "CLI starten" (KannCliNeuStarten) erscheint, sodass die CLI manuell
    /// neu gestartet werden kann.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected void CliPanel_BleibtSichtbarNachBeendigung_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "CliPanelVisibility-Repo", "CliPanelVisibility-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // CLI läuft: Stoppen-Button sichtbar, CLI-Panel-Tab sichtbar (ShowCliPanel==true, AusfuehrungsStatus==Aktiv)
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        var cliViewButtonWaehrendLauf = WaitForElement(mainWindow, cf => cf.ByName("CliViewButton"), Short);
        Assert.NotNull(cliViewButtonWaehrendLauf);

        // CLI manuell stoppen -> AusfuehrungsStatus wechselt auf "Beendet"
        var stoppenButton = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        stoppenButton!.AsButton().Click();

        // Stoppen-Button verschwindet (CLI nicht mehr aktiv)
        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Nach der Korrektur bleibt das CLI-Panel weiterhin sichtbar, obwohl AusfuehrungsStatus==Beendet ist
        var cliViewButtonNachStopp = WaitForElement(mainWindow, cf => cf.ByName("CliViewButton"), Short);
        Assert.NotNull(cliViewButtonNachStopp);

        // Letzte CLI-Ausgabe bleibt einsehbar
        var terminalConsole = mainWindow.FindFirstDescendant(cf => cf.ByName("TerminalConsole"));
        Assert.NotNull(terminalConsole);

        // "CLI starten" (KannCliNeuStarten) ist verfügbar, damit die CLI manuell neu gestartet werden kann
        var cliNeustartenButton = WaitForElement(mainWindow, cf => cf.ByName("CliNeustarten"), Short);
        Assert.NotNull(cliNeustartenButton);

        // Statusleiste zeigt weiterhin "Gestartet" (Aufgabenstatus unverändert)
        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
    }
}
