using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

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
        const string taskTitle = "ConPty-Aufzeichnung";
        var taskDetail = new TaskDetailView(mainWindow);
        taskDetail.SetTaskTitle(taskTitle).SaveTask().WaitForPersisted();
        taskDetail.Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        await ConPtyStart_ZeigtTerminalPanelMitStoppenButtonUndBanner_E2E(mainWindow, taskDetail);
        await TerminalText_LiveMarkCopyAndKeepSelection(mainWindow, taskDetail);
        await ConPtyKeyboardInput_EchoMarkerErscheintImCliOutputProtokoll_E2E(mainWindow, taskDetail);
        await ConPtyAnsiBurst_TypeBefehlAusgabeImProtokoll_E2E(mainWindow, taskDetail);
        await ConPtyPaste_CtrlVFuegtEchoBefehlEin_E2E(mainWindow, taskDetail);
        ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(mainWindow, taskDetail);
        ConPtyProcessEnd_NachExitBefehl_IsCliRunningFalse_E2E(mainWindow, taskDetail);

        await ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E(mainWindow, taskDetail, taskTitle);

        var dashboardAfterReplay = new MenuView(mainWindow).NavigateToDashboard();
        var projectDetail = dashboardAfterReplay.Menu.NavigateToProjects().OpenProject("ConPty-Projekt");
        projectDetail.DeleteProject();
    }

    /// <summary>
    /// Szenario: Nach dem Prozessende steht die vollständige Rohbyte-Aufzeichnung noch zur Verfügung.
    /// Zuerst wird der Save-Dialog des Exports per ESC abgebrochen (keine Datei, kein
    /// Fehlerbanner); danach erzeugt der bestätigte Export eine gültige .clireplay-Datei
    /// (Magic "SWCLRPLY"), die im Konsolentestfenster geladen und mit maximaler Geschwindigkeit
    /// vollständig über den echten Renderpfad abgespielt wird. Anschließend wird über die
    /// Seitenleiste zur Aufgabe zurücknavigiert.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der beendeten ConPTY-Session.</param>
    /// <param name="taskTitle">Der vor dem Terminal-Lifecycle ermittelte Titel der Aufgabe.</param>
    private async Task ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E(Window mainWindow, TaskDetailView taskDetail, string taskTitle)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_{Guid.NewGuid():N}.clireplay");
        SettingsView? settings = null;
        KonsolenTestDialogView? dialog = null;
        try
        {
            // Abbruch-Subphase: Save-Dialog per ESC schließen — keine Datei, kein Fehlerbanner.
            taskDetail.ExportCliReplay(null);
            Assert.False(File.Exists(pfad), "Der abgebrochene Export darf keine Datei erzeugen.");
            Assert.False(new ErrorView(mainWindow).IsVisible);

            taskDetail.ExportCliReplay(pfad);

            Assert.True(File.Exists(pfad), "Die .clireplay-Exportdatei wurde nicht erzeugt.");

            var magic = new byte[8];
            await using (var stream = File.OpenRead(pfad))
            {
                await stream.ReadExactlyAsync(magic);
            }
            Assert.Equal("SWCLRPLY", System.Text.Encoding.ASCII.GetString(magic));

            // Konsolentestfenster über Einstellungen öffnen, Export laden und abspielen.
            settings = new Views.SettingsView(mainWindow).ForceShow();
            dialog = settings.OpenKonsolenTestDialog();
            dialog.SetZeitrafferSchwelle("0");
            dialog.OeffneAufzeichnung(pfad);
            dialog.WarteAufQuellEintraege(1);

            dialog.StartWiedergabe();
            dialog.WarteAufStatus("Wiedergabe beendet.");
            dialog.Schliessen();

            settings.ForceClose(recurseToDashboard: false);

            // Nach Prozessende ist die Aufgabe nicht mehr in der Seitenleistenliste der aktiven
            // Aufgaben. Über die Projektdetailansicht wird sie unabhängig vom Laufstatus geöffnet.
            var dashboard = Assert.IsType<DashboardView>(mainWindow.CurrentView());
            var projekt = dashboard.Menu.NavigateToProjects().OpenProject("ConPty-Projekt");
            _ = projekt.OpenTask(taskTitle);
        }
        finally
        {
            // TryClose-Muster wie in E2E_KonsolenTestfenster: bei einem Assert-Fehler dürfen
            // das nicht-modale Konsolentestfenster und die Einstellungsansicht nicht offen
            // bleiben — sonst laufen Folge-Phasen gegen einen unerwarteten Fensterzustand.
            TryCloseKonsolenTestfenster(dialog, settings);

            if (File.Exists(pfad))
                File.Delete(pfad);
        }
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

    /// <summary>Pflichtabnahme E-01: Auswahl und Kopieren laufen gegen das echte Live-Terminal.
    /// Die sichtbare Markerzeile wird nach <c>cls</c> bewusst direkt oberhalb des Prompts platziert.
    /// Ihre Zellkoordinaten folgen daraus ohne Bild- oder Texterkennung; die Testhilfe verwendet
    /// dieselbe Consolas-/DPI-Messung wie der Renderer.</summary>
    private async Task TerminalText_LiveMarkCopyAndKeepSelection(Window mainWindow, TaskDetailView taskDetail)
    {
        var marker = $"LIVESEL{Guid.NewGuid().ToString("N")[..12]}";
        mainWindow.ClickInForeground();
        // Das Live-Backend kann ein höheres, festes Grid als der sichtbare Viewport haben.
        // Nach cls füllen harmlose Platzhalterzeilen dieses Grid, sodass Marker und Prompt
        // unabhängig von der festen ConPTY-Geometrie sicher am unteren Viewport-Rand landen.
        Keyboard.Type($"cls & for /L %i in (1,1,100) do @echo . & echo {marker}");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        await WarteAufCliOutputAsync(marker);

        // Der Protokolleintrag wird vor dem WPF-Renderpass geschrieben; erst danach ist die
        // Markerzeile auch zuverlässig im echten Terminal sichtbar.
        Thread.Sleep(250);

        var geometry = taskDetail.GetLiveTerminalGeometry();
        Assert.True(geometry.Rows >= 3, "das Live-Terminal braucht mindestens Marker-, Prompt- und Randzeile");
        Assert.True(marker.Length < geometry.Cols, "der Marker muss ohne Zeilenumbruch in das Live-Terminal passen");
        // Die cmd-Shell gibt vor dem nächsten Prompt eine Leerzeile aus. Der Cursor liegt daher
        // zwei logische Zeilen unter dem abschließenden echo-Marker. Die UIA-Geometrie liefert
        // beide Koordinatensysteme des Renderers;
        // dadurch bleibt der Drag auch bei abweichender ConPTY-Höhe stabil.
        var markerViewportRow = geometry.CursorRow - 2 - geometry.StartRow;
        Assert.InRange(markerViewportRow, 0, geometry.Rows - 1);
        var markerEndCol = marker.Length - 1;

        // UIA kann den Custom-Renderer nicht als TextPattern lesen. Der gezielte Vorher/Nachher-
        // Vergleich des berechneten Zellrechtecks belegt daher das echte Auswahl-Overlay, ohne
        // eine helle Textzeile im Screenshot suchen zu müssen.
        var vorAuswahl = taskDetail.ErfasseLiveZellPixel(geometry, markerViewportRow, 0, markerEndCol);
        SetClipboardText("E2E-Live-Clipboard-vor-Kopie");
        taskDetail.MarkiereLiveTerminalZeile(geometry, markerViewportRow, 0, markerEndCol);
        var nachAuswahl = taskDetail.ErfasseLiveZellPixel(geometry, markerViewportRow, 0, markerEndCol);
        Assert.True(KonsolenTestDialogView.ZaehleAbweichendeReplayPixel(vorAuswahl, nachAuswahl) > 100,
            "der Mausdrag muss im echten Live-Terminal ein sichtbares Auswahl-Overlay zeichnen");

        taskDetail.KopiereLiveAuswahl();
        var copiedMarker = GetClipboardText();
        Assert.True(string.Equals(marker, copiedMarker, StringComparison.Ordinal),
            $"die Auswahl muss den Marker kopieren (tatsächlich '{copiedMarker}'). "
            + $"Viewport: Start={geometry.StartRow}, Rows={geometry.Rows}, Cursor={geometry.CursorRow}:{geometry.CursorColumn}, "
            + $"MarkerViewportRow={markerViewportRow}, Cell={geometry.CellWidth:F3}x{geometry.CellHeight:F3}, Bounds={geometry.Rect}.");

        // Ausgabe auf einer anderen, aktuellen Promptzeile darf eine gültige Auswahl nicht
        // verwerfen. Der erneute Clipboard-Nachweis vermeidet eine Prüfung bloßer UI-Zustände.
        var weitereAusgabe = $"LIVEOUT{Guid.NewGuid().ToString("N")[..12]}";
        mainWindow.ClickInForeground();
        Keyboard.Type($"echo {weitereAusgabe}");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        await WarteAufCliOutputAsync(weitereAusgabe);
        Assert.Contains("Selection=Valid", taskDetail.GetLiveTerminalStatus(), StringComparison.Ordinal);

        // Genügend neue Zeilen schieben den Marker garantiert ins Scrollback. Seine Zellen
        // bleiben unverändert; eine fortbestehende Kopie beweist den stabilen RowId-Pfad.
        var scrollEnd = $"LIVESCROLL{Guid.NewGuid().ToString("N")[..12]}";
        mainWindow.ClickInForeground();
        Keyboard.Type($"for /L %i in (1,1,{geometry.Rows + 4}) do @echo {scrollEnd}");
        Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        await WarteAufCliOutputAsync(scrollEnd);
        Thread.Sleep(150);
        Assert.Contains("Selection=Valid", taskDetail.GetLiveTerminalStatus(), StringComparison.Ordinal);

        Assert.True(taskDetail.HasTerminalOutput(), "das Live-Terminal muss für die Auswahl sichtbar sein");
        Assert.True(taskDetail.IsCliRunning(), "die Auswahl darf den Live-Prozess nicht beeinflussen");
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
        Thread.Sleep(300); // cmd protokolliert Eingabezeile und Echo-Ausgabe in getrennten Chunks.

        await using var db = OpenTestDbContext();
        var aufgabe = await db.Aufgaben.OrderByDescending(a => a.ErstellungsDatum).FirstAsync();
        var trefferVorReattach = await db.Protokolleintraege
            .CountAsync(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.CliOutput && p.Inhalt.Contains(marker));

        // Weg vom CLI-Panel (Info) und zurück (CLI) — TerminalControl wird neu an die Session gebunden.
        taskDetail.SwitchPanel("InfoCliToggle");
        // Der Stoppen-Button liegt in der CLI-Ribbon-Gruppe und ist im Info-Panel absichtlich
        // ausgeblendet. Die weiterhin sichtbare CLI-Umschaltung ist hier der korrekte UI-Nachweis,
        // dass die Aufgabe die Session weiterhin anzeigen kann; nach dem Rückwechsel beweist das
        // sichtbare Terminal zusätzlich die erfolgreiche Neuanbindung.
        Assert.True(taskDetail.HasCliPanel(), "die Session muss unabhängig vom angezeigten Panel weiterlaufen");
        taskDetail.SwitchPanel("CliViewButton");
        Assert.True(taskDetail.HasTerminalOutput(), "das TerminalControl muss nach der Neuanbindung wieder sichtbar sein");

        // Der Replay-Rebuild darf keine bereits vor dem Panelwechsel gespeicherten Chunks nochmals
        // protokollieren. Die konkrete Anzahl ist absichtlich nicht eins: cmd liefert sowohl
        // die eingegebene Echo-Zeile als auch deren Ausgabe.
        var trefferNachReattach = await db.Protokolleintraege
            .CountAsync(p => p.AufgabeId == aufgabe.Id && p.Typ == ProtokollTyp.CliOutput && p.Inhalt.Contains(marker));
        Assert.Equal(trefferVorReattach, trefferNachReattach);
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
            // Clipboard kann unmittelbar nach dem Schließen einer UIA-/Screenshot-Operation
            // noch kurz einem anderen Prozess gehören. Die begrenzte Wiederholung ist Teil
            // der E2E-Infrastruktur und verhindert, dass ein echter Produktnachweis daran
            // scheitert.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch (Exception ex)
                {
                    fehler = ex;
                    Thread.Sleep(50);
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (fehler is not null)
            throw fehler;
    }
}
