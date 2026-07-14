using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für das neue Dateiexplorer-Register in der Aufgabendetailansicht.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus (SOFTWARESCHMIEDE_TEST_DB_PATH gesetzt) steht ausschließlich das LocalDirectoryPlugin
///   als SCM-Plugin zur Verfügung (kein GitHub-Plugin), siehe PluginManager.IsAllowedInTestMode.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_FileExplorer : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository klonen (Aufgabe starten), dann auf das "Dateien"-Register wechseln.
    /// Prüft: Split-View mit Baum und Standard/Vergleich/Aktualisieren-Umschaltung ist erreichbar.
    /// </summary>
    [SkippableFact]
    public void DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("FileExplorer-Repo", "FileExplorer-Projekt");

        // git init im Quellverzeichnis vorab bestätigen, damit "Starten" im ersten Versuch gelingt.
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt) und
        // das CLI-Panel sichtbar - das bestätigt, dass der kombinierte Klon-/Start-Ablauf durchlief.
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var dateiViewButton = WaitForElement(mainWindow, cf => cf.ByName("DateiViewButton"), Short);
        dateiViewButton.AsButton().Click();

        var baum = WaitForElement(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);
        Assert.NotNull(baum);

        var standardButton = WaitForElement(mainWindow, cf => cf.ByName("FileExplorerStandardButton"), Short);
        Assert.NotNull(standardButton);

        var vergleichButton = WaitForElement(mainWindow, cf => cf.ByName("FileExplorerVergleichButton"), Short);
        Assert.NotNull(vergleichButton);

        var aktualisierenButton = WaitForElement(mainWindow, cf => cf.ByName("FileExplorerAktualisierenButton"), Short);
        Assert.NotNull(aktualisierenButton);
    }
}
