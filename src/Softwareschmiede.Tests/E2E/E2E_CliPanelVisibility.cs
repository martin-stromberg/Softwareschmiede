using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

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

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        // CLI läuft: Stoppen-Button sichtbar, CLI-Panel-Tab sichtbar (ShowCliPanel==true, AusfuehrungsStatus==Aktiv)
        taskDetail.WaitForCliRunning();
        Assert.True(taskDetail.HasCliPanel());

        // CLI manuell stoppen -> AusfuehrungsStatus wechselt auf "Beendet"
        taskDetail.StopCli();

        // Nach der Korrektur bleibt das CLI-Panel weiterhin sichtbar, obwohl AusfuehrungsStatus==Beendet ist
        Assert.True(taskDetail.HasCliPanel());

        // Letzte CLI-Ausgabe bleibt einsehbar
        Assert.True(taskDetail.HasTerminalOutput());

        // "CLI starten" (KannCliNeuStarten) ist verfügbar, damit die CLI manuell neu gestartet werden kann
        Assert.True(taskDetail.CanRestartCli());

        // Statusleiste zeigt weiterhin "Gestartet" (Aufgabenstatus unverändert)
        Assert.True(taskDetail.IsTaskStarted());

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }
}
