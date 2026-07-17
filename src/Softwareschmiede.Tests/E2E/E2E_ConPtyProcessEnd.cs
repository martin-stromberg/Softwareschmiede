using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Prozessende über Stoppen-Button setzt IsCliRunning auf false.
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
public sealed class E2E_ConPtyProcessEnd : WpfTestBase
{
    /// <summary>
    /// Szenario: Nach ConPTY-Start wird der Prozess über den Stoppen-Button beendet.
    /// Das Prozessende beendet den ReadLoop und setzt IsCliRunning=false:
    /// Der Stoppen-Button verschwindet und der Starten-Button erscheint wieder nicht
    /// (Status bleibt Gestartet, kein Rollback).
    /// </summary>
    [SkippableFact]
    public void ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("ConPtyEnd-Repo", "ConPtyEnd-Projekt");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        stoppenButton.AsButton().Click();

        // Nach Prozessende: Stoppen-Button verschwindet (IsCliRunning=false)
        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Kein Fehler-Banner
        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);

        // Status-Anzeige zeigt weiterhin "Gestartet" (kein Rollback durch manuelles Stoppen)
        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);
    }
}
