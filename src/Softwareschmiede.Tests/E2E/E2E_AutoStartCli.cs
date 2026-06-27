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
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_AutoStartCli : WpfTestBase
{
    /// <summary>
    /// Szenario: Aufgabe wird gestartet (Status wechselt zu "Gestartet", CLI läuft). Über "Stoppen"
    /// wird der CLI-Prozess manuell beendet, ohne den Status zu ändern. Anschließend wird über
    /// "Zurück" und erneutes Öffnen der Aufgabe die Ansicht neu geladen.
    /// Prüft: Beim Laden der Aufgabe (Status "Gestartet", kein laufender Prozess) wird die CLI
    /// automatisch neu gestartet und eingebettet (Stoppen-Button erscheint wieder ohne manuellen Klick
    /// auf "Starten" oder "Plugin ändern").
    /// </summary>
    [Fact]
    public void AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E()
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        var mainWindow = SetupProjectMitNeuerAufgabe("AutoStartCli-Repo", "AutoStartCli-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // CLI manuell stoppen, Status bleibt "Gestartet"
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Short);
        stoppenButton.AsButton().Click();

        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var statusGestartetNachStop = WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);
        Assert.NotNull(statusGestartetNachStop);

        // Zurück navigieren und Aufgabe erneut öffnen (löst TaskDetailViewModel.LadenAsync neu aus)
        var zurueckButton = WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);
        zurueckButton.AsButton().Click();

        var listBox = WaitForElement(mainWindow, cf => cf.ByName("AufgabenListe"), Medium);
        var items = listBox.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        Assert.True(items.Length >= 1, "Aufgabenliste sollte die gestartete Aufgabe enthalten.");
        items[0].DoubleClick();

        // Automatischer CLI-Neustart beim Laden: Stoppen-Button erscheint ohne manuellen Start-Klick
        var stoppenButtonNachAutoStart = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButtonNachAutoStart);
    }
}
