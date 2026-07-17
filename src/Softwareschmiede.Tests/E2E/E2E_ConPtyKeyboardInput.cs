using FlaUI.Core.Input;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Tastatureingabe über das TerminalControl wird an den CLI-Prozess weitergeleitet.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_ConPtyKeyboardInput : WpfTestBase
{
    /// <summary>
    /// Szenario: Nach ConPTY-Start kann das TerminalControl den Tastaturfokus erhalten.
    /// Tastatureingaben werden fehlerfrei entgegengenommen (kein Fehler-Banner).
    /// Die Eingabe landet in PseudoConsoleSession.InputStream — verifizierbar durch
    /// das Ausbleiben von Fehlern nach der Eingabe.
    /// </summary>
    [SkippableFact]
    public void ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ConPtyKbd-Repo", "ConPtyKbd-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Klick auf das Hauptfenster setzt den Fokus; anschließende Tastatureingabe
        // landet im fokussierten TerminalControl und wird via InputStream weitergeleitet.
        mainWindow.Click();
        Keyboard.Type("hello");

        // Kurz warten — bei ConPTY-Fehler würde ein FehlerMeldung-Banner erscheinen
        Thread.Sleep(500);
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }
}
