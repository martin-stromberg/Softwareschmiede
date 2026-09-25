using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für den ConPTY-basierten Prozess-Lifecycle und die Terminal-Ansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// Seit Issue #271 startet die Terminal-Session den Plugin-Prozess direkt aus der
/// <c>TerminalSessionStartSpec</c> — der KiSimulator startet eine interaktive <c>cmd.exe /k</c>-Shell
/// mit Banner, sodass getippte Befehle sofort ausgeführt werden (kein 300-ms-Delay, keine
/// Befehls-Injektion mehr). Die einzelnen Phasen decken ab: Start mit Banner-Nachweis im
/// CliOutput-Protokoll, Tastatur-Echo-Marker, ANSI-Burst über <c>type</c>, Clipboard-Paste,
/// Panel-Reattach ohne Doppelausgabe (Replay-Puffer), Resize und Prozessende über <c>exit</c>.
///
/// Konsolidierung (Issue #153): Alle Phasen laufen an derselben, einmal gestarteten Session
/// nacheinander in einem gemeinsamen App-Lifecycle — Prozessende bewusst zuletzt.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt die ConPTY-Szenarien nacheinander an derselben laufenden Session aus: Start (Stoppen-Button
    /// + /k-Banner im Protokoll), Tastatur-Echo-Marker, ANSI-Ausgabe-Burst, Clipboard-Paste,
    /// Session-Neuanbindung ohne Doppelausgabe, Fenster-Resize, Prozessende über den getippten
    /// <c>exit</c>-Befehl (IsCliRunning=false, Status bleibt "Gestartet").
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit dem geöffneten, für ConPTY vorbereiteten Task.</param>
    protected async Task ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "ConPty-Repo", "ConPty-Projekt");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        await ConPtyStart_ZeigtTerminalPanelMitStoppenButtonUndBanner_E2E(mainWindow, taskDetail);
        await ConPtyKeyboardInput_EchoMarkerErscheintImCliOutputProtokoll_E2E(mainWindow, taskDetail);
        await ConPtyAnsiBurst_TypeBefehlAusgabeImProtokoll_E2E(mainWindow, taskDetail);
        await ConPtyPaste_CtrlVFuegtEchoBefehlEin_E2E(mainWindow, taskDetail);
        await ConPtySessionReattach_WegUndZurueck_KeineDoppelausgabe_E2E(taskDetail);
        ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(mainWindow, taskDetail);
        ConPtyProcessEnd_NachExitBefehl_IsCliRunningFalse_E2E(mainWindow, taskDetail);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }

    /// <summary>
    /// Szenario: Aufgabe starten mit ConPTY. Nach erfolgreichem Start muss der Stoppen-Button
    /// erscheinen (IsCliRunning=true — belegt, dass TerminalSessionGestartet gefeuert und die
    /// Session gesetzt wurde) und das <c>cmd.exe /k</c>-Banner der Simulator-Shell im
    /// CliOutput-Protokoll eingehen (direkter Prozessstart aus der TerminalSessionStartSpec —
    /// kein Befehls-Echo über eine 300-ms-Verzögerung mehr).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der bereits gestarteten ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der gestarteten Aufgabe.</param>
    private async Task ConPtyStart_ZeigtTerminalPanelMitStoppenButtonUndBanner_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        taskDetail.WaitForCliRunning();

        // Kein Fehler-Banner sichtbar
        Assert.False(new ErrorView(mainWindow).IsVisible);

        // Das /k-Banner der interaktiven Simulator-Shell muss auf dem echten Ausgabepfad ankommen.
        await WarteAufCliOutputAsync("KI-Simulator läuft");
    }

    /// <summary>
    /// Szenario: Das TerminalControl erhält den Tastaturfokus; ein getippter <c>echo</c>-Befehl
    /// wird an der interaktiven Shell ausgeführt und der Marker landet im CliOutput-Protokoll.
    /// Zusätzlich werden Alt Gr-Sonderzeichen ("{", "}", "@", "~") und Strg+Links/Strg+Rechts
    /// fehlerfrei entgegengenommen.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private async Task ConPtyKeyboardInput_EchoMarkerErscheintImCliOutputProtokoll_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        var marker = $"E2E_MARKER_{Guid.NewGuid():N}";

        // Klick auf das Hauptfenster setzt den Fokus; anschließende Tastatureingabe landet im
        // fokussierten TerminalControl und wird via InputStream an die Shell weitergeleitet.
        mainWindow.ClickInForeground();
        Keyboard.Type($"echo {marker}");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

        // Alt Gr-Sonderzeichen (deutsches Layout) und wortweise Navigation: ohne Fehlerannahme.
        Keyboard.Type("{}@~");
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.LEFT);
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT);
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.BACK);
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

        await WarteAufCliOutputAsync(marker);

        Assert.False(new ErrorView(mainWindow).IsVisible);
        Assert.True(taskDetail.IsCliRunning());
    }

    /// <summary>
    /// Szenario: Eine ANSI-Testdatei mit SGR-Farbsequenzen wird per getipptem <c>type</c>-Befehl
    /// ausgegeben — der Bursts an Escape-Sequenzen muss fehlerfrei geparst werden und der
    /// Klartext-Bestandteil im CliOutput-Protokoll erscheinen.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private async Task ConPtyAnsiBurst_TypeBefehlAusgabeImProtokoll_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        var markerText = $"ANSIBURST_{Guid.NewGuid():N}";
        var ansiDatei = Path.Combine(Path.GetTempPath(), $"ssm-ansi-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(
            ansiDatei,
            $"\x1b[31mROT\x1b[0m {markerText} \x1b[1mFETT\x1b[0m \x1b[32mGRUEN\x1b[0m\r\n");
        try
        {
            mainWindow.ClickInForeground();
            Keyboard.Type($"type {ansiDatei}");
            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

            await WarteAufCliOutputAsync(markerText);

            Assert.False(new ErrorView(mainWindow).IsVisible);
            Assert.True(taskDetail.IsCliRunning());
        }
        finally
        {
            File.Delete(ansiDatei);
        }
    }

    /// <summary>
    /// Szenario: Ein per Ctrl+V eingefügter <c>echo</c>-Befehl aus der Zwischenablage wird an der
    /// interaktiven Shell ausgeführt — der Marker muss im CliOutput-Protokoll ankommen.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private async Task ConPtyPaste_CtrlVFuegtEchoBefehlEin_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        var marker = $"E2E_PASTE_{Guid.NewGuid():N}";
        SetClipboardText($"echo {marker}");

        mainWindow.ClickInForeground();
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_V);
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

        await WarteAufCliOutputAsync(marker);

        Assert.False(new ErrorView(mainWindow).IsVisible);
        Assert.True(taskDetail.IsCliRunning());
    }

    /// <summary>
    /// Szenario: Wechsel vom CLI-Panel ins Info-Panel und zurück. Die Session läuft unabhängig vom
    /// angezeigten Control weiter; bei der Neuanbindung wird der Buffer aus dem Replay-Puffer
    /// wiederhergestellt — die bereits geloggte Marker-Ausgabe darf im CliOutput-Protokoll nicht
    /// doppelt erscheinen.
    /// </summary>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private async Task ConPtySessionReattach_WegUndZurueck_KeineDoppelausgabe_E2E(TaskDetailView taskDetail)
    {
        var marker = $"E2E_REATTACH_{Guid.NewGuid():N}";
        taskDetail.Window.ClickInForeground();
        Keyboard.Type($"echo {marker}");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        await WarteAufCliOutputAsync(marker);

        // Weg vom CLI-Panel (Info) und zurück (CLI) — TerminalControl wird neu an die Session gebunden.
        taskDetail.SwitchPanel("InfoCliToggle");
        Assert.True(taskDetail.IsCliRunning(), "die Session muss unabhängig vom angezeigten Panel weiterlaufen");
        taskDetail.SwitchPanel("CliViewButton");
        Assert.True(taskDetail.HasTerminalOutput(), "das TerminalControl muss nach der Neuanbindung wieder sichtbar sein");

        // Der Marker darf im CliOutput-Protokoll trotz Replay-Rebuild nur genau einmal protokolliert sein.
        await using var db = OpenTestDbContext();
        var aufgabe = await db.Aufgaben.OrderByDescending(a => a.ErstellungsDatum).FirstAsync();
        var treffer = await db.Protokolleintraege
            .CountAsync(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.CliOutput && p.Inhalt.Contains(marker));
        Assert.True(treffer == 1, $"Der Marker '{marker}' darf nach der Session-Neuanbindung nicht doppelt protokolliert sein (gefunden: {treffer}).");
    }

    /// <summary>
    /// Szenario: Das Fenster wird verkleinert und vergrößert. Der Session-Resize wird dedupliziert
    /// und serialisiert an das ConPTY weitergereicht; nach Resize darf kein Fehler-Banner erscheinen.
    /// Der Stoppen-Button muss weiterhin sichtbar sein (CLI noch aktiv).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private void ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        var currentBounds = mainWindow.BoundingRectangle;
        FlaUiApp.GetMainWindow(Automation)?.Patterns.Transform.Pattern.Resize(
            currentBounds.Width - 100,
            currentBounds.Height - 50);

        Thread.Sleep(300);

        FlaUiApp.GetMainWindow(Automation)?.Patterns.Transform.Pattern.Resize(
            currentBounds.Width,
            currentBounds.Height);

        Thread.Sleep(300);

        Assert.True(taskDetail.IsCliRunning());
        Assert.False(new ErrorView(mainWindow).IsVisible);
    }

    /// <summary>
    /// Szenario: Der getippte <c>exit</c>-Befehl beendet die interaktive Shell — die Session meldet
    /// das Prozessende über ihr Exited-Ereignis, IsCliRunning wird false (Stoppen-Button
    /// verschwindet) und der Aufgaben-Status bleibt "Gestartet" (kein Rollback).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private void ConPtyProcessEnd_NachExitBefehl_IsCliRunningFalse_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        mainWindow.ClickInForeground();
        Keyboard.Type("exit");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);

        taskDetail.WaitForCliStopped();

        Assert.False(new ErrorView(mainWindow).IsVisible);
        Assert.True(taskDetail.IsTaskStarted());
    }

    /// <summary>Wartet, bis ein CliOutput-Protokolleintrag der jüngsten Aufgabe den erwarteten
    /// Text enthält (Nachweis, dass Ausgabe auf dem echten Ausgabepfad ankam).</summary>
    private async Task WarteAufCliOutputAsync(string erwarteterInhalt)
    {
        await WartenBisAsync(async () =>
        {
            await using var db = OpenTestDbContext();
            var aufgabe = await db.Aufgaben.OrderByDescending(a => a.ErstellungsDatum).FirstAsync();
            return await db.Protokolleintraege
                .AnyAsync(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.CliOutput && p.Inhalt.Contains(erwarteterInhalt));
        });
    }

    /// <summary>Setzt Text in die Zwischenablage über einen STA-Thread (WPF-Clipboard ist STA-gebunden).</summary>
    private static void SetClipboardText(string text)
    {
        Exception? fehler = null;
        var thread = new Thread(() =>
        {
            try { System.Windows.Clipboard.SetText(text); }
            catch (Exception ex) { fehler = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (fehler is not null)
            throw fehler;
    }
}
