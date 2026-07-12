namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Größenänderung des Anwendungsfensters aktualisiert die ConPTY-Dimensionen.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_ConPtyResize : WpfTestBase
{
    /// <summary>
    /// Szenario: Nach ConPTY-Start wird das Fenster verkleinert und vergrößert.
    /// ResizePseudoConsole wird intern aufgerufen; nach Resize darf kein Fehler-Banner erscheinen.
    /// Der Stoppen-Button muss weiterhin sichtbar sein (CLI noch aktiv).
    /// </summary>
    [SkippableFact]
    public void ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ConPtyResize-Repo", "ConPtyResize-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

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
}
