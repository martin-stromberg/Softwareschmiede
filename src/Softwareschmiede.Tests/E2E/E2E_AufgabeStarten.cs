using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für den kombinierten Start-Ablauf (Klonen + CLI-Start) der Aufgabendetailansicht (Feature 72).
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
public sealed class E2E_AufgabeStarten : WpfTestBase
{
    /// <summary>
    /// Szenario: Aufgabe im Status "Neu" mit "Starten" auf "Gestartet" wechseln.
    /// Prüft: Repository wird geklont (lokales Quellverzeichnis via LocalDirectoryPlugin),
    /// Status wechselt auf "Gestartet" und die CLI wird gestartet (CLI-Panel mit Stoppen-Button sichtbar).
    /// </summary>
    [Fact]
    public void AufgabeStarten_KlontRepositoryUndStartetCli_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("AufgabeStarten-Repo", "AufgabeStarten-Projekt");

        // Da kein KI-Plugin vorkonfiguriert ist, zeigt der Start-Ablauf zunächst den Plugin-Auswahl-Dialog
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start: CLI-Panel sichtbar (Stoppen-Button erscheint, da IsCliRunning=true)
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        // Statusleiste zeigt "Gestartet"
        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);

        // Kein Fehler angezeigt
        var fehlerMeldung = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerMeldung);
    }
}
