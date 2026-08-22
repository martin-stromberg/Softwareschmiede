using FlaUI.Core.AutomationElements;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test dafür, dass nach einem gestoppten CLI-Lauf kein impliziter Neustart beim Laden erfolgt.
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
    /// wird der CLI-Prozess manuell beendet, ohne den Gesamtstatus zu ändern. Anschließend wird über
    /// "Zurück" und erneutes Öffnen der Aufgabe die Ansicht neu geladen.
    /// Prüft: Beim Laden der Aufgabe (Gesamtstatus "Gestartet", beendete Ausführung) wird die CLI
    /// nicht automatisch neu gestartet. Erst ein expliziter Klick auf "Starten" darf wieder einen
    /// CLI-Prozess einbetten. Prüft außerdem (Issue 193), dass Protokolleinträge im Hintergrund
    /// nachgeladen werden.
    /// </summary>
    protected void AufgabeOeffnen_NachStoppen_StartetCliNichtAutomatischErstExplizit_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "AutoStartCli-Repo", "AutoStartCli-Projekt");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        WechsleAufgabenansicht(mainWindow, "InfoCliToggle");
        // Protokoll-Nachladen (Issue 193): Der GitAktion-Eintrag aus der Repository-Vorbereitung
        // wird asynchron im Hintergrund geladen und muss ohne expliziten Reload sichtbar werden.
        // AutomationProperties.Name des Protokolltyp-TextBlocks ist explizit an Typ gebunden
        // (TaskDetailView.xaml, "ProtokollTyp-{Typ}"), statt zufällig am impliziten Textinhalt.
        WaitForElement(mainWindow, cf => cf.ByName("ProtokollTyp-GitAktion"), Medium);

        // CLI manuell stoppen, Gesamtstatus bleibt "Gestartet"
        WechsleAufgabenansicht(mainWindow, "CliViewButton");
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Short);
        stoppenButton.AsButton().Click();

        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        WaitForElement(mainWindow, cf => cf.ByName("Gestartet"), Short);

        // Zurück navigieren und Aufgabe erneut öffnen (löst TaskDetailViewModel.LadenAsync neu aus)
        AufgabeDetailZurueck(mainWindow);

        var items = OffeneAufgabenItems(mainWindow);
        Assert.True(items.Length >= 1, "Aufgabenliste sollte die gestartete Aufgabe enthalten.");
        ErsteOffeneAufgabeOeffnen(items);

        // Kein impliziter CLI-Neustart beim Laden: Stoppen bleibt weg, Starten bleibt explizit verfügbar.
        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Short);
        var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);

        startenButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Protokoll wird nach dem erneuten Öffnen erneut asynchron nachgeladen und angezeigt.
        WechsleAufgabenansicht(mainWindow, "InfoCliToggle");
        WaitForElement(mainWindow, cf => cf.ByName("ProtokollTyp-GitAktion"), Medium);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
    }
}
