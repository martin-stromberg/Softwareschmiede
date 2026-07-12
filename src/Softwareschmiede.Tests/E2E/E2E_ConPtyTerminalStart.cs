namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für den ConPTY-basierten Prozessstart und die Terminal-Ansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_ConPtyTerminalStart : WpfTestBase
{
    /// <summary>
    /// Szenario: Aufgabe starten mit ConPTY. Nach erfolgreichem Start muss der
    /// Stoppen-Button erscheinen (IsCliRunning=true), was bestätigt, dass
    /// PseudoConsoleSessionGestartet gefeuert wurde und die Session nicht null ist.
    /// </summary>
    [SkippableFact]
    public void ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ConPty-Repo", "ConPty-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Stoppen-Button erscheint wenn IsCliRunning=true — dies belegt, dass
        // PseudoConsoleSessionGestartet gefeuert und die Session gesetzt wurde.
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        // Kein Fehler-Banner sichtbar
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);
    }
}
