using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die automatische Aktualisierung des KI-Ausführungsstatus in der Seitenleisten-Aufgabenliste
/// (Issue 108), ohne dass der Benutzer die Ansicht manuell neu laden muss.
///
/// Der Status ("▶ Läuft"/"⏸ Wartet"/"✓ Bereit") wird vom <c>KiAusfuehrungsStatusConverter</c> aus
/// <c>AktiveRunId</c> und <c>LastHeartbeatUtc</c> der Aufgabe berechnet. Dieser Test startet und stoppt
/// einen echten CLI-Prozess (KiSimulator-Plugin) und prüft, dass die Seitenleisten-Kachel dem tatsächlichen
/// Produktivpfad folgt: <c>CliProcessManager</c> setzt/entfernt <c>AktiveRunId</c> beim
/// <c>CliProcessStatusChanged</c>-Event (Start/Stopp), <c>MainWindowViewModel</c> lädt daraufhin
/// (event-getrieben oder über den periodischen Timer-Fallback) die aktiven Aufgaben neu.
///
/// Hinweis zu einer früheren Fassung dieses Tests: Sie simulierte den Heartbeat direkt in der Datenbank
/// (<c>AktiveRunId</c>/<c>LastHeartbeatUtc</c> per SQL gesetzt), statt einen echten CLI-Prozess zu nutzen.
/// Dadurch deckte der Test ausschließlich den Timer-Fallback ab, nicht aber, ob ein echter CLI-Start
/// tatsächlich <c>AktiveRunId</c> setzt. Produktivcode hat <c>AktiveRunId</c> nirgends gesetzt (Issue-108-
/// Kundenrückmeldung: Fußzeile zeigte "Ausführung läuft", Seitenleisten-Kachel blieb bei "✓ Bereit") — der
/// Test war also grün, obwohl das reale Verhalten fehlerhaft war. Seit der Behebung (siehe
/// <c>CliProcessManager.OnCliProcessStatusChanged</c>) prüft dieser Test den echten Start-/Stopp-Pfad.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    private const string AufgabenTitel = "Neue Aufgabe";

    /// <summary>
    /// Szenario: Eine Aufgabe wird über den echten Produktivpfad gestartet (KiSimulator-CLI-Prozess). Erst
    /// mit dem Start wechselt die Aufgabe von Status "Neu" (in der Seitenleiste nicht gelistet) auf
    /// "Gestartet" — die Seitenleisten-Kachel muss dabei automatisch, ohne manuelles Neuladen der Ansicht,
    /// "▶ Läuft" anzeigen. Wird der CLI-Prozess über den Stoppen-Button beendet, wechselt die Kachel
    /// automatisch auf "✓ Bereit".
    /// </summary>
    protected void SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "ArbeitsstatusAktualisierung-Repo", "ArbeitsstatusAktualisierung-Projekt");

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();

        // Assert: Kachel erscheint automatisch mit "▶ Läuft", sobald der echte CLI-Prozess läuft — ohne
        // manuelles Neuladen der Ansicht (CliProcessManager.OnCliProcessStatusChanged setzt AktiveRunId
        // beim Gestartet-Event; vor dem Fix für Issue 108 wurde AktiveRunId nirgends im Produktivcode
        // gesetzt, sodass die Kachel fälschlich dauerhaft "✓ Bereit" gezeigt hätte).
        taskDetail.Menu.WaitForTaskStatus(AufgabenTitel, "▶ Läuft", Medium);

        taskDetail.StopCli();

        // Assert: Kachel wechselt automatisch zurück auf "✓ Bereit", nachdem der CLI-Prozess beendet wurde
        // (CliProcessManager entfernt AktiveRunId beim Gestoppt-Event).
        taskDetail.Menu.WaitForTaskStatus(AufgabenTitel, "✓ Bereit", Medium);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }
}
