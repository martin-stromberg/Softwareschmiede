using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

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
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_ConPtyLifecycle : WpfTestBase
{
    /// <summary>
    /// Führt vier ConPTY-Szenarien nacheinander an derselben laufenden Session aus: Start (Stoppen-Button
    /// erscheint), Fenster-Resize (Session bleibt aktiv), Tastatureingabe (wird ohne Fehler entgegengenommen),
    /// Prozessende über den Stoppen-Button (Session endet, Status bleibt "Gestartet").
    /// </summary>
    [SkippableFact]
    public void ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ConPty-Repo", "ConPty-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E(mainWindow);
        ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(mainWindow);
        ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E(mainWindow);
        ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Aufgabe starten mit ConPTY. Nach erfolgreichem Start muss der
    /// Stoppen-Button erscheinen (IsCliRunning=true), was bestätigt, dass
    /// PseudoConsoleSessionGestartet gefeuert wurde und die Session nicht null ist.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der bereits gestarteten ConPTY-Session.</param>
    private void ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E(AutomationElement mainWindow)
    {
        // Stoppen-Button erscheint wenn IsCliRunning=true — dies belegt, dass
        // PseudoConsoleSessionGestartet gefeuert und die Session gesetzt wurde.
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Kein Fehler-Banner sichtbar
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }

    /// <summary>
    /// Szenario: Das Fenster wird verkleinert und vergrößert. ResizePseudoConsole wird intern
    /// aufgerufen; nach Resize darf kein Fehler-Banner erscheinen. Der Stoppen-Button muss weiterhin
    /// sichtbar sein (CLI noch aktiv).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    private void ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E(AutomationElement mainWindow)
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
        var stoppenButtonNachResize = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        Assert.NotNull(stoppenButtonNachResize);

        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }

    /// <summary>
    /// Szenario: Das TerminalControl erhält den Tastaturfokus. Tastatureingaben werden fehlerfrei
    /// entgegengenommen (kein Fehler-Banner). Die Eingabe landet in
    /// <c>PseudoConsoleSession.InputStream</c> — verifizierbar durch das Ausbleiben von Fehlern nach
    /// der Eingabe.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    private void ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E(AutomationElement mainWindow)
    {
        // Klick auf das Hauptfenster setzt den Fokus; anschließende Tastatureingabe
        // landet im fokussierten TerminalControl und wird via InputStream weitergeleitet.
        mainWindow.Click();
        Keyboard.Type("hello");

        // Kurz warten — bei ConPTY-Fehler würde ein FehlerMeldung-Banner erscheinen
        Thread.Sleep(500);
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }

    /// <summary>
    /// Szenario: Der Prozess wird über den Stoppen-Button beendet. Das Prozessende beendet den
    /// ReadLoop und setzt IsCliRunning=false: Der Stoppen-Button verschwindet und der Status bleibt
    /// "Gestartet" (kein Rollback).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der laufenden ConPTY-Session.</param>
    private void ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E(AutomationElement mainWindow)
    {
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        stoppenButton.AsButton().Click();

        // Nach Prozessende: Stoppen-Button verschwindet (IsCliRunning=false)
        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Kein Fehler-Banner
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);

        // Status-Anzeige zeigt weiterhin "Gestartet" (kein Rollback durch manuelles Stoppen)
        WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
    }
}
