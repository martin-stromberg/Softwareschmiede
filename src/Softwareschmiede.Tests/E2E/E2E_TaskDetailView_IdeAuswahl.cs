using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FluentAssertions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die im RibbonSplitButton "IDE öffnen" nicht bereits von
/// <c>E2E_VerzeichnisAktionen.VerzeichnisAktionen_ArbeitsverzeichnisUndIdeOeffnen_E2E</c> abgedeckten Szenarien:
/// Abbruch des Auswahl-Dialogs über den Dropdown-Button sowie das Fehlerverhalten bei null gefundenen
/// Einstiegspunkten (deaktiviertes Visual-Studio-Code-Fallback-Plugin, keine .sln-Datei).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Deaktiviertes Visual-Studio-Code-Plugin und keine .sln-Datei führen zu 0 gefundenen
    /// Einstiegspunkten - der Haupt-Button des Split-Buttons zeigt eine Fehlermeldung, der Dropdown-Button
    /// bleibt unsichtbar. Anschließend zeigen zwei angelegte .sln-Dateien den Dropdown-Button; ein Abbruch
    /// des darüber geöffneten Auswahl-Dialogs öffnet nichts. Am Ende wird das in Phase 1 deaktivierte
    /// Visual-Studio-Code-Plugin wieder aktiviert und gespeichert, damit nachfolgende E2E-Methoden im
    /// selben App-Lifecycle (<c>RunConPtyTests</c>) beide IDE-Plugins aktiv vorfinden.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabe(mainWindow, "IdeAuswahl-Repo", "IdeAuswahl-Projekt");

        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        // Phase 1: Visual-Studio-Code-Plugin deaktivieren (Einstellungen), damit ohne .sln-Datei kein
        // Fallback-Plugin mehr existiert und FindEntryPointsAsync 0 Einstiegspunkte liefert.
        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);
        DeaktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudioCode");

        var speichernButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButton.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);

        NavigateBackToDashboard(mainWindow);
        var offeneAufgaben = OffeneAufgabenItems(mainWindow);
        ErsteOffeneAufgabeOeffnen(offeneAufgaben);
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);

        // Phase 2: Ohne .sln-Datei und ohne aktives Fallback-Plugin liefert der Haupt-Button eine
        // Fehlermeldung; der Dropdown-Button bleibt unsichtbar (KannIdeAuswaehlen erfordert >= 2 Einstiegspunkte).
        var ideHauptButtonOhneEinstiegspunkte = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        var protokollVorFehler = await ReadProzessStartLogAsync();
        ideHauptButtonOhneEinstiegspunkte.AsButton().Click();

        var fehlerMeldung = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
        fehlerMeldung.Should().NotBeNull();

        var protokollNachFehler = await ReadProzessStartLogAsync();
        protokollNachFehler.Should().Be(protokollVorFehler, "ohne gefundene Einstiegspunkte darf kein Prozess gestartet werden");

        var dropdownButtonOhneEinstiegspunkte = mainWindow.FindFirstDescendant(cf => cf.ByName("IdeOeffnenDropdown"));
        dropdownButtonOhneEinstiegspunkte.Should().BeNull("ohne mindestens zwei Einstiegspunkte muss der Dropdown-Button unsichtbar sein");

        // Phase 3: Zwei .sln-Dateien anlegen und die Aufgabe neu laden - der Dropdown-Button wird sichtbar.
        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        var ersteSolution = Path.Combine(lokalerKlonPfad, "Erste.sln");
        var zweiteSolution = Path.Combine(lokalerKlonPfad, "Zweite.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        File.WriteAllText(zweiteSolution, string.Empty);
        ReloadTaskDetail(mainWindow);

        var dropdownButtonMitZweiSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnenDropdown"), Short);
        var protokollVorAbbruch = await ReadProzessStartLogAsync();
        dropdownButtonMitZweiSln.AsButton().Click();

        var dialog = WaitForWindow("Solution auswählen", Short);
        var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
        abbrechenButton.AsButton().Click();

        WaitUntilGone(Automation.GetDesktop(), cf => cf.ByName("Solution auswählen"), Short);

        var protokollNachAbbruch = await ReadProzessStartLogAsync();
        protokollNachAbbruch.Should().Be(protokollVorAbbruch, "ein abgebrochener Auswahl-Dialog darf keinen Einstiegspunkt öffnen");

        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        NavigateBackFromProjectCardToProjectsList(mainWindow);
        DeleteCurrentProject(mainWindow);

        // Das in Phase 1 deaktivierte Visual-Studio-Code-Plugin wieder aktivieren und speichern, damit
        // nachfolgende E2E-Methoden in RunConPtyTests denselben App-Lifecycle mit beiden IDE-Plugins aktiv
        // vorfinden (analog zu E2E_IdePluginSettings.cs).
        NavigateToSettings(mainWindow);
        OpenPluginsTab(mainWindow);
        AktiviereIdePlugin(mainWindow, "Softwareschmiede.VisualStudioCode");

        var speichernButtonEnde = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
        speichernButtonEnde.AsButton().Click();
        WaitForElement(mainWindow, cf => cf.ByName("Einstellungen gespeichert."), Short);

        NavigateBackToDashboard(mainWindow);
    }
}
