using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Tests.E2E.Views;

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

        ConfirmLocalDirectoryGitInitInSourceDirectory();
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());

        // Phase 1: Visual-Studio-Code-Plugin deaktivieren (Einstellungen), damit ohne .sln-Datei kein
        // Fallback-Plugin mehr existiert und FindEntryPointsAsync 0 Einstiegspunkte liefert.
        var settings = projectDetail.Menu.NavigateToSettings();
        settings.SwitchTab("Plugins");
        settings.SetIdePluginEnabled("Softwareschmiede.VisualStudioCode", false);
        settings.SaveSettings();

        var projectList = settings.Menu.NavigateToProjects();
        var projectDetailReopened = projectList.OpenProject("IdeAuswahl-Projekt");
        var taskDetailReopened = projectDetailReopened.OpenFirstTask();

        // Das Ribbon kann bei sichtbarer CLI-Gruppe breiter als das Standard-Fenster werden; einfache
        // WPF-Buttons implementieren kein UIA-ScrollItemPattern und können daher nicht programmatisch in
        // den sichtbaren Bereich gescrollt werden - das Fenster wird deshalb maximiert, damit die
        // Ribbon-Buttons ("IdeOeffnen" etc.) tatsächlich im klickbaren Bereich liegen. Bewusst erst HIER
        // (statt vor der Projektlisten-Navigation) maximiert: Klicks auf Projekt-Kacheln in der Projektliste
        // waren bei bereits maximiertem Fenster nicht zuverlässig (vermutlich ein Koordinaten-/Hit-Test-
        // Problem nach dem Zustandswechsel).
        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        // Phase 2: Ohne .sln-Datei und ohne aktives Fallback-Plugin liefert der Haupt-Button eine
        // Fehlermeldung; der Dropdown-Button bleibt unsichtbar (KannIdeAuswaehlen erfordert >= 2 Einstiegspunkte).
        var protokollVorFehler = await ReadProzessStartLogAsync();
        taskDetailReopened.OpenIde();

        Assert.False(string.IsNullOrWhiteSpace(new ErrorView(mainWindow).GetErrorMessage()));

        var protokollNachFehler = await ReadProzessStartLogAsync();
        Assert.Equal(protokollVorFehler, protokollNachFehler);

        Assert.False(taskDetailReopened.HasIdeDropdown());

        // Phase 3: Zwei .sln-Dateien anlegen und die Aufgabe neu laden - der Dropdown-Button wird sichtbar.
        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        var ersteSolution = Path.Combine(lokalerKlonPfad, "Erste.sln");
        var zweiteSolution = Path.Combine(lokalerKlonPfad, "Zweite.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        File.WriteAllText(zweiteSolution, string.Empty);
        taskDetailReopened = taskDetailReopened.Reload();

        Assert.True(taskDetailReopened.HasIdeDropdown());
        var protokollVorAbbruch = await ReadProzessStartLogAsync();
        var solutionDialog = taskDetailReopened.OpenIdeDropdown();
        solutionDialog.Cancel();

        var protokollNachAbbruch = await ReadProzessStartLogAsync();
        Assert.Equal(protokollVorAbbruch, protokollNachAbbruch);

        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        taskDetailReopened.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();

        // Das in Phase 1 deaktivierte Visual-Studio-Code-Plugin wieder aktivieren und speichern, damit
        // nachfolgende E2E-Methoden in RunConPtyTests denselben App-Lifecycle mit beiden IDE-Plugins aktiv
        // vorfinden (analog zu E2E_IdePluginSettings.cs).
        var settingsEnde = projectDetailFinal.Menu.NavigateToSettings();
        settingsEnde.SwitchTab("Plugins");
        settingsEnde.SetIdePluginEnabled("Softwareschmiede.VisualStudioCode", true);
        settingsEnde.SaveSettings();

        settingsEnde.Menu.NavigateToDashboard();
    }
}
