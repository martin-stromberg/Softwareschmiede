using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für "Shutdown bei Abschluss" (Issue 6): Der Auto-Shutdown-Block in der
/// Seitenleisten-Fußzeile zeigt die fachlich laufenden Automatisierungen, armiert nach dem
/// letzten Ende einen Toleranz-Countdown, bricht bei Wiederanlauf ab und zeichnet im
/// Testmodus die Shutdown-Aufforderung in <c>system-shutdowns.log</c> auf statt das
/// Betriebssystem herunterzufahren. Force und Toleranz persistieren über einen Neustart,
/// die Aktivierung bleibt sitzungsgebunden.
///
/// Start und Stopp der KiSimulator-CLI erfolgen über den Prozesslebenszyklus (UI-Stop-Pfad
/// bzw. "CliNeustarten") - ohne ConPTY-Befehlseingabe, damit der Test auch in Umgebungen
/// ohne funktionierende Konsolen-Isolation läuft.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface" (dieser Test läuft in der
/// OsInterface-Lane, Category "E2E").
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Konsolidierter Auto-Shutdown-Fluss (ein App-Lifecycle plus ein Neustart):
    /// Aufgabe mit KiSimulator-CLI starten → Fußzeile zeigt "1 laufende" → Aktivierungs-Checkbox
    /// erscheint → aktivieren → Toleranzfeld + Force-Checkbox erscheinen → Toleranz "5" + Force
    /// setzen → CLI stoppen → Countdown-Text sichtbar → CLI erneut starten → Countdown verschwindet
    /// (Abbruch) → erneut stoppen → Countdown erneut → Checkbox deaktivieren → Countdown weg und
    /// Checkbox bei Anzahl 0 ausgeblendet → CLI erneut starten → Checkbox wieder sichtbar →
    /// reaktivieren → Toleranz "0" → CLI stoppen → Toleranz 0 löst unmittelbar aus →
    /// <c>system-shutdowns.log</c> enthält <c>erzwingen=True</c> → Neustart → Aktivierung aus,
    /// Force/Toleranz wiederhergestellt.
    /// </summary>
    protected async Task AutoShutdown_CountdownAbbruchAufzeichnungUndPersistenz_E2E(Window mainWindow)
    {
        const string aufgabenTitel = "Neue Aufgabe";
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        SetupProjectMitNeuerAufgabe(mainWindow, "AutoShutdown-Repo", "AutoShutdown-Projekt");

        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();
        var menu = taskDetail.Menu;

        // Die Fußzeile zeigt die laufende Automatisierung; die Aktivierungs-Checkbox erscheint
        // erst bei Anzahl > 0 (Sichtbarkeitsregel).
        menu.WaitForAutoShutdownLaufendeAnzahlText("1 laufende", Long);
        Assert.True(
            menu.IsAutoShutdownAktivierenVisible(),
            "Die Aktivierungs-Checkbox sollte bei einer laufenden Automatisierung sichtbar sein.");

        menu.SetzeAutoShutdownAktiviert(true);
        // Das Options-Panel aktualisiert den UIA-Baum erst im Render-Pass nach der
        // Aktivierung — Reads ohne Synchronisation können die Elemente noch nicht sehen.
        menu.WaitForAutoShutdownToleranzSichtbar(Medium);
        Assert.True(menu.IsAutoShutdownToleranzVisible(), "Das Toleranz-Feld sollte nach der Aktivierung sichtbar sein.");
        menu.SetzeAutoShutdownToleranz("5");
        menu.SetzeAutoShutdownErzwingen(true);
        Assert.True(menu.GetAutoShutdownErzwingenChecked() == true, "Die Erzwingen-Checkbox sollte gesetzt sein.");

        // Stopp der letzten laufenden Automatisierung → Toleranz-Countdown wird armiert.
        taskDetail.StopCli();
        menu.WaitForAutoShutdownCountdownText("Herunterfahren in", Long);

        // Wiederanlauf bricht den Countdown ab (Statuswechsel 0 → >0).
        WaitForElement(mainWindow, cf => cf.ByName("CliNeustarten"), Medium).AsButton().ClickInForeground();
        taskDetail.WaitForCliRunning();
        menu.WaitForAutoShutdownLaufendeAnzahlText("1 laufende", Long);
        menu.WaitForAutoShutdownCountdownVerschwunden(Long);

        // Erneuter Stopp → neuer voller Countdown (positive → null-Transition armiert neu).
        taskDetail.StopCli();
        menu.WaitForAutoShutdownCountdownText("Herunterfahren in", Long);

        // Deaktivierung während des Countdowns bricht ab; bei Anzahl 0 blendet sich der
        // gesamte Block anschließend aus (Sichtbarkeitsregel).
        menu.SetzeAutoShutdownAktiviert(false);
        menu.WaitForAutoShutdownCountdownVerschwunden(Long);
        // Bei Anzahl 0 und deaktiviertem Schalter blendet sich der gesamte Block aus.
        menu.WaitForAutoShutdownAktivierenVerschwunden(Medium);

        // Wiederanlauf → Checkbox wird wieder sichtbar; Toleranz 0 für den sofortigen Auslöser.
        WaitForElement(mainWindow, cf => cf.ByName("CliNeustarten"), Medium).AsButton().ClickInForeground();
        taskDetail.WaitForCliRunning();
        menu.WaitForAutoShutdownLaufendeAnzahlText("1 laufende", Long);
        menu.SetzeAutoShutdownAktiviert(true);
        menu.SetzeAutoShutdownToleranz("0");

        taskDetail.StopCli();

        // Toleranz 0 → die Shutdown-Aufforderung wird unmittelbar aufgezeichnet (Testmodus-Fake,
        // kein echter OS-Shutdown). Force wurde zuvor gesetzt → "erzwingen=True".
        await WaitForSystemShutdownEintragAsync("erzwingen=True", Medium);

        // Persistenz-Nachweis über Neustart: Force + Toleranz wiederhergestellt,
        // Aktivierung sitzungsgebunden zurückgesetzt (Checkbox aus).
        mainWindow = RestartAppPreservingDatabase();
        var restartedMenu = new MenuView(mainWindow);
        var restartedDetail = restartedMenu.NavigateToProjects()
            .OpenProject("AutoShutdown-Projekt")
            .OpenTask(aufgabenTitel);
        WaitForElement(mainWindow, cf => cf.ByName("CliNeustarten"), Medium).AsButton().ClickInForeground();
        restartedDetail.WaitForCliRunning();

        restartedMenu.WaitForAutoShutdownLaufendeAnzahlText("1 laufende", Long);
        Assert.Equal(
            false,
            restartedMenu.GetAutoShutdownAktiviertChecked());
        restartedMenu.SetzeAutoShutdownAktiviert(true);
        // Wie oben: erst auf das eingeblendete Options-Panel synchronisieren, dann lesen.
        restartedMenu.WaitForAutoShutdownToleranzSichtbar(Medium);
        Assert.Equal("0", restartedMenu.GetAutoShutdownToleranzText());
        Assert.Equal(true, restartedMenu.GetAutoShutdownErzwingenChecked());

        // Aufräumen: Schalter wieder lösen, damit kein weiterer Log-Eintrag entsteht, und
        // die CLI sauber beenden.
        restartedMenu.SetzeAutoShutdownAktiviert(false);
        restartedDetail.StopCli();
    }
}
