using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für den automatischen CLI-Neustart beim Laden einer Aufgabe im Status "Gestartet"
/// ohne laufenden CLI-Prozess (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Aufgabe wird gestartet (Status wechselt zu "Gestartet", CLI läuft). Über "Stoppen"
    /// wird der CLI-Prozess manuell beendet, ohne den Status zu ändern. Anschließend wird über
    /// "Zurück" und erneutes Öffnen der Aufgabe die Ansicht neu geladen.
    /// Prüft: Beim Laden der Aufgabe (Status "Gestartet", kein laufender Prozess) wird die CLI
    /// automatisch neu gestartet und eingebettet (Stoppen-Button erscheint wieder ohne manuellen Klick
    /// auf "Starten" oder "Plugin ändern").
    /// </summary>
    protected void AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "AutoStartCli-Repo", "AutoStartCli-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // CLI manuell stoppen, Status bleibt "Gestartet"
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Short);
        stoppenButton.AsButton().Click();

        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);

        // Zurück navigieren und Aufgabe erneut öffnen (löst TaskDetailViewModel.LadenAsync neu aus)
        AufgabeDetailZurueck(mainWindow);

        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 1, "Aufgabenliste sollte die gestartete Aufgabe enthalten.");
        ErsteOffeneAufgabeOeffnen(items);

        // Automatischer CLI-Neustart beim Laden: Stoppen-Button erscheint ohne manuellen Start-Klick
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
    }
}
