using FlaUI.Core.AutomationElements;
using Softwareschmiede.Infrastructure.Services;

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
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_AufgabeStarten : WpfTestBase
{
    /// <summary>
    /// Szenario: Aufgabe im Status "Neu" mit "Starten" auf "Gestartet" wechseln.
    /// Erster Versuch schlägt erwartungsgemäß fehl, da ConfirmGitInitInSourceDirectory nicht gesetzt ist
    /// (InSourceDirectory-Modus erfordert explizite Bestätigung für git init).
    /// Nach Korrektur der Einstellung gelingt der zweite Versuch: Repository wird geklont,
    /// Status wechselt auf "Gestartet" und die CLI wird gestartet (CLI-Panel mit Stoppen-Button sichtbar).
    /// </summary>
    [SkippableFact]
    public void AufgabeStarten_KlontRepositoryUndStartetCli_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe(
            "AufgabeStarten-Repo",
            "AufgabeStarten-Projekt",
            initializeSourceGitRepository: false);

        // Erster Versuch: ConfirmGitInitInSourceDirectory ist nicht gesetzt → Fehlermeldung erwartet
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Fehlermeldung wird angezeigt, da git init im Quellverzeichnis nicht bestätigt ist
        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Medium);
        Assert.NotNull(fehlerBanner);

        // Einstellung korrigieren: ConfirmGitInitInSourceDirectory auf true setzen
        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");

        // Zweiter Versuch: Plugin-Dialog erneut bedienen, diesmal ist die Bestätigung gesetzt
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start: CLI-Panel sichtbar (Stoppen-Button erscheint, da IsCliRunning=true)
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        // Statusleiste zeigt "Gestartet"
        var statusGestartet = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartet);

        // Kein Fehler mehr angezeigt (Border ist Collapsed → TextBlock nicht im UIA-Baum)
        var fehlerMeldungNachStart = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerMeldungNachStart);
    }
}
