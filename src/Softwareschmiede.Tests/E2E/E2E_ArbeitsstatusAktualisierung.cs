using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die automatische Aktualisierung des KI-Ausführungsstatus in der Seitenleisten-Aufgabenliste
/// (Issue 108), ohne dass der Benutzer die Ansicht manuell neu laden muss.
///
/// Der Status ("▶ Läuft"/"⏸ Wartet"/"✓ Bereit") wird vom <c>KiAusfuehrungsStatusConverter</c> aus
/// <c>AktiveRunId</c> und <c>LastHeartbeatUtc</c> der Aufgabe berechnet. Diese Werte werden testweise
/// direkt in der SQLite-Testdatenbank des laufenden App-Prozesses gesetzt (analog zum bestehenden Muster
/// <see cref="WpfTestBase.OpenTestDbContext"/>), um den Heartbeat-Ablauf zu simulieren, ohne auf einen
/// echten KI-Prozess angewiesen zu sein. Dieser Weg durchläuft ausschließlich den periodischen
/// Timer-Fallback aus MainWindowViewModel (kein CliProcessStatusChanged-Event), da die Datenbankänderung
/// nicht über den laufenden CLI-Prozess ausgelöst wird.
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
public sealed class E2E_ArbeitsstatusAktualisierung : WpfTestBase
{
    private const string AufgabenTitel = "Neue Aufgabe";

    /// <summary>
    /// Szenario: Eine Aufgabe wird gestartet. Ihr Heartbeat wird direkt in der Datenbank auf "aktiv" gesetzt
    /// (simuliert einen laufenden KI-Prozess) — die Seitenleisten-Kachel muss automatisch, ohne manuelles
    /// Neuladen der Ansicht, auf "▶ Läuft" wechseln. Wird der Heartbeat anschließend wieder entfernt
    /// (simuliert CLI-Stopp), wechselt die Kachel automatisch zurück auf "✓ Bereit".
    /// </summary>
    [Fact]
    public void SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ArbeitsstatusAktualisierung-Repo", "ArbeitsstatusAktualisierung-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Baseline: kein aktiver Heartbeat gesetzt → Statuskachel zeigt "✓ Bereit"
        WaitForStatusHelpText(mainWindow, "✓ Bereit", Medium);

        // Heartbeat direkt in der Datenbank aktivieren (simuliert laufenden KI-Prozess), ohne die
        // Ansicht manuell neu zu laden.
        SetzeHeartbeatInDatenbank(aktiv: true);

        // Assert: Kachel wechselt automatisch auf "▶ Läuft" (Timer-Fallback, kein manuelles Neuladen)
        WaitForStatusHelpText(mainWindow, "▶ Läuft", Medium);

        // Heartbeat wieder entfernen (simuliert CLI-Stopp/Heartbeat-Ablauf)
        SetzeHeartbeatInDatenbank(aktiv: false);

        // Assert: Kachel wechselt automatisch zurück auf "✓ Bereit"
        WaitForStatusHelpText(mainWindow, "✓ Bereit", Medium);
    }

    private void SetzeHeartbeatInDatenbank(bool aktiv)
    {
        using var db = OpenTestDbContext();
        var aufgabe = db.Aufgaben.Single(a => a.Titel == AufgabenTitel);
        aufgabe.AktiveRunId = aktiv ? "e2e-simulierter-run" : null;
        aufgabe.LastHeartbeatUtc = aktiv ? DateTimeOffset.UtcNow : null;
        db.SaveChanges();
    }

    /// <summary>
    /// Wartet, bis die Status-Kachel der Aufgabe in der Seitenleiste den erwarteten Status-Text als
    /// <c>AutomationProperties.HelpText</c> anzeigt (siehe ActiveTasksListControl.xaml).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    /// <param name="erwarteterStatus">Der erwartete Status-Text (z. B. "▶ Läuft").</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    private static void WaitForStatusHelpText(AutomationElement mainWindow, string erwarteterStatus, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? letzterStatus = null;
        while (DateTime.UtcNow < deadline)
        {
            var statusElement = mainWindow.FindFirstDescendant(cf => cf.ByName($"AufgabeStatus:{AufgabenTitel}"));
            letzterStatus = statusElement?.HelpText;
            if (letzterStatus == erwarteterStatus)
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"Statuskachel zeigte innerhalb von {timeout.TotalSeconds}s nicht den erwarteten Status '{erwarteterStatus}' an. Zuletzt gesehen: '{letzterStatus}'.");
    }
}
