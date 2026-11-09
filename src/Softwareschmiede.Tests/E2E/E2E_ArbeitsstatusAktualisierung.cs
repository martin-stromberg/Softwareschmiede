using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
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

        // Issue 342: Sitzungs-Wiederverwendung — die Detailansicht verlassen und dieselbe Aufgabe
        // erneut öffnen. Die Fußzeile muss den Live-CLI-Status der wiederangebundenen Session zeigen
        // und die Kachel weiterhin synchron "▶ Läuft" melden.
        taskDetail = taskDetail.Reload();
        taskDetail.WaitForCliRunning();
        WarteAufCliStatusText(taskDetail, "Ausführung läuft");
        taskDetail.Menu.WaitForTaskStatus(AufgabenTitel, "▶ Läuft", Medium);

        // Issue 342 (Divergenz-F1/H4-Bild): Fehlende oder veraltete persistierte Laufdaten bei
        // weiterlaufendem Prozess — Laufdaten direkt in der DB löschen. Die Kachel muss über den
        // Live-Zweig weiter "▶ Läuft" zeigen, und die Selbstheilung (Heartbeat-Tick/Abgleich,
        // Intervall 30 s) muss die Laufdaten wieder herstellen.
        Guid aufgabeId;
        using (var db = OpenTestDbContext())
        {
            aufgabeId = db.Aufgaben.AsNoTracking().Single(a => a.Titel == AufgabenTitel).Id;
        }
        using (var db = OpenTestDbContext())
        {
            var tracked = db.Aufgaben.Single(a => a.Id == aufgabeId);
            tracked.AktiveRunId = null;
            tracked.LastHeartbeatUtc = null;
            tracked.LaufStatus = null;
            tracked.AusfuehrungsStatus = Softwareschmiede.Domain.Enums.AufgabeAusfuehrungsStatus.Beendet;
            db.SaveChanges();
        }

        taskDetail.Menu.WaitForTaskStatus(AufgabenTitel, "▶ Läuft", Medium);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        var laufdatenGeheilt = false;
        while (DateTime.UtcNow < deadline && !laufdatenGeheilt)
        {
            using var db = OpenTestDbContext();
            laufdatenGeheilt = db.Aufgaben.AsNoTracking().Single(a => a.Id == aufgabeId).AktiveRunId is not null;
            if (!laufdatenGeheilt)
                Thread.Sleep(500);
        }
        Assert.True(laufdatenGeheilt, "Die Selbstheilung hat die gelöschten Laufdaten nicht innerhalb von 45 s wieder hergestellt.");

        taskDetail.StopCli();

        // Assert: Kachel wechselt automatisch zurück auf "✓ Bereit", nachdem der CLI-Prozess beendet wurde
        // (CliProcessManager entfernt AktiveRunId beim Gestoppt-Event).
        taskDetail.Menu.WaitForTaskStatus(AufgabenTitel, "✓ Bereit", Medium);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }

    /// <summary>Wartet, bis die Fußzeile der Aufgabendetailansicht den erwarteten CLI-Status enthält.</summary>
    private static void WarteAufCliStatusText(TaskDetailView taskDetail, string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            var text = taskDetail.GetCliStatusText();
            if (text.Contains(expected, StringComparison.Ordinal))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Die Fußzeile zeigte innerhalb von {Medium.TotalSeconds}s nicht '{expected}' (zuletzt: '{taskDetail.GetCliStatusText()}').");
    }
}
