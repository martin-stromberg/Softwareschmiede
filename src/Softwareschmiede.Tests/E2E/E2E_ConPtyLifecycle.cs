using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
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
/// Konsolidierung (Issue #153): Start, Resize, Tastatureingabe und Prozessende testen alle
/// dieselbe, einmal gestartete ConPTY-Session nacheinander (kein Aufräumen zwischen den Phasen nötig,
/// da alle Phasen bis auf die letzte denselben laufenden Prozess voraussetzen) - daher als vier Phasen
/// in einem gemeinsamen App-Lifecycle statt vier eigenständiger App-Starts. Prozessende steht bewusst
/// als letzte Phase, da sie den Prozess beendet.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt vier ConPTY-Szenarien nacheinander an derselben laufenden Session aus: Start (Stoppen-Button
    /// erscheint), Fenster-Resize (Session bleibt aktiv), Tastatureingabe (wird ohne Fehler entgegengenommen),
    /// Prozessende über den Stoppen-Button (Session endet, Status bleibt "Gestartet").
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit dem geöffneten, für ConPTY vorbereiteten Task.</param>
    protected void ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "ConPty-Repo", "ConPty-Projekt");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E(mainWindow, taskDetail);
        ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(mainWindow, taskDetail);
        ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E(mainWindow, taskDetail);
        ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E(mainWindow, taskDetail);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }

    /// <summary>
    /// Szenario: Aufgabe starten mit ConPTY. Nach erfolgreichem Start muss der
    /// Stoppen-Button erscheinen (IsCliRunning=true), was bestätigt, dass
    /// PseudoConsoleSessionGestartet gefeuert wurde und die Session nicht null ist.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der bereits gestarteten ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der gestarteten Aufgabe.</param>
    private void ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        // Stoppen-Button erscheint wenn IsCliRunning=true — dies belegt, dass
        // PseudoConsoleSessionGestartet gefeuert und die Session gesetzt wurde.
        taskDetail.WaitForCliRunning();

        // Kein Fehler-Banner sichtbar
        Assert.False(new ErrorView(mainWindow).IsVisible);
    }

    /// <summary>
    /// Szenario: Das Fenster wird verkleinert und vergrößert. ResizePseudoConsole wird intern
    /// aufgerufen; nach Resize darf kein Fehler-Banner erscheinen. Der Stoppen-Button muss weiterhin
    /// sichtbar sein (CLI noch aktiv).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private void ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        // Fenstergröße ändern, um ResizePseudoConsole auszulösen
        var currentBounds = mainWindow.BoundingRectangle;
        FlaUiApp.GetMainWindow(Automation)?.Patterns.Transform.Pattern.Resize(
            currentBounds.Width - 100,
            currentBounds.Height - 50);

        Thread.Sleep(300);

        FlaUiApp.GetMainWindow(Automation)?.Patterns.Transform.Pattern.Resize(
            currentBounds.Width,
            currentBounds.Height);

        Thread.Sleep(300);

        // CLI muss noch laufen und kein Fehler erschienen sein
        Assert.True(taskDetail.IsCliRunning());
        Assert.False(new ErrorView(mainWindow).IsVisible);
    }

    /// <summary>
    /// Szenario: Das TerminalControl erhält den Tastaturfokus. Reguläre Tastatureingaben, über Alt Gr
    /// erreichbare Sonderzeichen (z. B. "{", "}", "@", "~" auf deutschem Layout) und Strg+Links/Strg+Rechts
    /// (wortweise Navigation) werden fehlerfrei entgegengenommen (kein Fehler-Banner) und die CLI-Session
    /// bleibt aktiv. Die Sonderzeichen werden über FlaUI direkt als Unicode-Zeichen eingegeben (wie es auch
    /// WPF <c>OnTextInput</c> bei Alt Gr-Komposition erhält), unabhängig vom tatsächlich aktiven
    /// System-Tastaturlayout der Testmaschine (siehe Offener Punkt #1 im Umsetzungsplan). Die Eingaben landen
    /// in <c>PseudoConsoleSession.InputStream</c> — verifizierbar durch das Ausbleiben von Fehlern und die
    /// weiterhin laufende Session nach der Eingabe. Eine Prüfung des exakten Zeichen- bzw. Cursor-Ergebnisses
    /// im Terminal-Puffer ist über FlaUI/UI-Automation nicht möglich, da <c>TerminalControl</c> den Inhalt
    /// selbst zeichnet und keine Text-Automation-Pattern für den Bufferinhalt bereitstellt.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private void ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        // Klick auf das Hauptfenster setzt den Fokus; anschließende Tastatureingabe
        // landet im fokussierten TerminalControl und wird via InputStream weitergeleitet.
        mainWindow.Click();
        Keyboard.Type("hello");

        // Alt Gr-Sonderzeichen (deutsches Layout): "{", "}", "@", "~".
        Keyboard.Type("{}@~");

        // Strg+Links/Strg+Rechts: wortweise Cursor-Navigation als VT100-Sequenz.
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.LEFT);
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT);

        // Kurz warten — bei ConPTY-Fehler würde ein FehlerMeldung-Banner erscheinen
        Thread.Sleep(500);
        Assert.False(new ErrorView(mainWindow).IsVisible);

        // Die CLI-Session muss nach den neuen Tasteneingaben weiterhin aktiv sein (kein Absturz/Abbruch).
        Assert.True(taskDetail.IsCliRunning());
    }

    /// <summary>
    /// Szenario: Der Prozess wird über den Stoppen-Button beendet. Das Prozessende beendet den
    /// ReadLoop und setzt IsCliRunning=false: Der Stoppen-Button verschwindet und der Status bleibt
    /// "Gestartet" (kein Rollback).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    /// <param name="taskDetail">Die Aufgabendetailansicht der laufenden Aufgabe.</param>
    private void ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E(Window mainWindow, TaskDetailView taskDetail)
    {
        // Nach Prozessende: Stoppen-Button verschwindet (IsCliRunning=false)
        taskDetail.StopCli();

        // Kein Fehler-Banner
        Assert.False(new ErrorView(mainWindow).IsVisible);

        // Status-Anzeige zeigt weiterhin "Gestartet" (kein Rollback durch manuelles Stoppen)
        Assert.True(taskDetail.IsTaskStarted());
    }
}
