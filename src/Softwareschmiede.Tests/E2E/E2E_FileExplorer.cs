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
/// Konsolidierung (Issue #153): Beide ursprünglichen Szenarien teilen exakt dasselbe Setup (Repository
/// klonen, Aufgabe starten, Dateiexplorer öffnen) und laufen deshalb als Phasen in einem App-Lifecycle.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_FileExplorer : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository klonen (Aufgabe starten), dann auf das "Dateien"-Register wechseln.
    /// Prüft: Split-View mit Baum und Standard/Vergleich/Aktualisieren-Umschaltung ist erreichbar.
    /// Anschließend: Wechsel zum Info-Register, dann zum CLI-Register.
    /// Prüft: Der Dateiexplorer wird ausgeblendet und das Info- sowie das CLI-Register bleiben
    /// erreichbar. Regressionstest für ein defektes Visibility-Binding (RelativeSource
    /// AncestorType=UserControl auf eine Eigenschaft, die nur im DataContext existiert), das das
    /// FileExplorerView zuvor dauerhaft sichtbar hielt und damit die anderen Register überdeckte.
    /// </summary>
    [SkippableFact]
    public void DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("FileExplorer-Repo", "FileExplorer-Projekt");

        // git init im Quellverzeichnis vorab bestätigen, damit "Starten" im ersten Versuch gelingt.
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt) und
        // das CLI-Panel sichtbar - das bestätigt, dass der kombinierte Klon-/Start-Ablauf durchlief.
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var dateiViewButton = WaitForElement(mainWindow, cf => cf.ByName("DateiViewButton"), Short);
        dateiViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerStandardButton"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerVergleichButton"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerAktualisierenButton"), Short);
        WaitForElement(mainWindow, cf => cf.ByName("FileExplorerDateiOeffnenButton"), Short);

        var infoButton = WaitForElement(mainWindow, cf => cf.ByName("InfoCliToggle"), Short);
        infoButton.AsButton().Click();

        // Dateiexplorer-Baum muss verschwinden - vorher blieb er wegen des defekten Bindings dauerhaft
        // sichtbar und überdeckte das Info-Register.
        WaitUntilGone(mainWindow, cf => cf.ByName("FileExplorerBaum"), Short);

        var cliViewButton = WaitForElement(mainWindow, cf => cf.ByName("CliViewButton"), Short);
        cliViewButton.AsButton().Click();

        WaitForElement(mainWindow, cf => cf.ByName("TerminalConsole"), Short);
    }
}
