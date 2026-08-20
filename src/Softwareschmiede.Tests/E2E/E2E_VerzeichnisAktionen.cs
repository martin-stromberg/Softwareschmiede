using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FluentAssertions;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die ins Ribbon-Menü der Aufgabendetailansicht überführten Verzeichnis-Aktionen
/// „Arbeitsverzeichnis öffnen" und „IDE öffnen".
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Testmodus (SOFTWARESCHMIEDE_TEST_DB_PATH gesetzt) zeichnet IProzessStarter
///   (AufzeichnenderProzessStarter) jeden Prozessstart in einer Logdatei auf, statt einen echten
///   Prozess zu starten - siehe WpfTestBase.WaitForProzessStartEintragAsync.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Repository klonen (Aufgabe starten), dann nacheinander „Arbeitsverzeichnis öffnen"
    /// und „IDE öffnen" über das Ribbon prüfen - zunächst ohne, dann mit einer und mit mehreren
    /// „*.sln"-Dateien im Arbeitsverzeichnis.
    /// </summary>
    protected async Task VerzeichnisAktionen_ArbeitsverzeichnisUndIdeOeffnen_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabe(mainWindow, "VerzeichnisAktionen-Repo", "VerzeichnisAktionen-Projekt");

        // Das Ribbon kann bei sichtbarer CLI-Gruppe (Status Gestartet/Wartend) breiter als das Standard-
        // Fenster werden (Dateien- und Werkzeuge-Gruppe kommen neu hinzu). Ein Button einfacher WPF-Buttons
        // implementiert kein UIA-ScrollItemPattern, kann also nicht programmatisch in den sichtbaren Bereich
        // gescrollt werden - das Fenster wird deshalb maximiert, damit die Ribbon-Buttons tatsächlich im
        // klickbaren Bereich liegen.
        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        // git init im Quellverzeichnis vorab bestätigen, damit "Starten" im ersten Versuch gelingt.
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt).
        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();

        // Phase 1: "Arbeitsverzeichnis öffnen" zeichnet den OS-Dateiexplorer-Start mit dem LokalerKlonPfad auf.
        var arbeitsverzeichnisButton = WaitForElement(mainWindow, cf => cf.ByName("ArbeitsverzeichnisOeffnen"), Short);
        arbeitsverzeichnisButton.AsButton().Click();
        await WaitForProzessStartEintragAsync(lokalerKlonPfad);

        // Phase 2: Ohne "*.sln" ist "IDE öffnen" weiterhin aktiv (CanExecute hängt nur noch vom vorhandenen
        // Arbeitsverzeichnis ab, nicht mehr von gefundenen Solutions) - ein Klick löst über
        // PluginSelectionService.ResolveIdePluginAsync automatisch den Visual-Studio-Code-Fallback aus, da
        // Visual Studio Code standardmäßig als aktives IDE-Plugin gilt.
        var ideButtonOhneSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonOhneSln.Properties.IsEnabled.Value.Should().BeTrue("das Arbeitsverzeichnis existiert bereits, auch ohne .sln-Datei");

        var protokollVorFallback = await ReadProzessStartLogAsync();
        ideButtonOhneSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(lokalerKlonPfad), sinceContent: protokollVorFallback);

        var dialogBeiFallback = mainWindow.FindFirstDescendant(cf => cf.ByName("Solution auswählen"));
        dialogBeiFallback.Should().BeNull("ohne gefundene Solution öffnet der Visual-Studio-Code-Fallback direkt, ohne Auswahl-Dialog");

        // Genau eine "*.sln" anlegen und die Aufgabe neu laden (Ribbon-Button-CanExecute wird beim Laden gecacht).
        var ersteSolution = Path.Combine(lokalerKlonPfad, "Erste.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        ReloadTaskDetail(mainWindow);

        // Phase 3: Bei genau einer "*.sln" öffnet "IDE öffnen" diese direkt, ohne Auswahl-Dialog.
        var ideButtonMitEinerSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonMitEinerSln.Properties.IsEnabled.Value.Should().BeTrue("es existiert jetzt genau eine .sln-Datei");
        ideButtonMitEinerSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(ersteSolution);

        var dialogNachEinerSln = mainWindow.FindFirstDescendant(cf => cf.ByName("Solution auswählen"));
        dialogNachEinerSln.Should().BeNull("bei genau einer Solution darf kein Auswahl-Dialog erscheinen");

        var dropdownButtonMitEinerSln = mainWindow.FindFirstDescendant(cf => cf.ByName("IdeOeffnenDropdown"));
        dropdownButtonMitEinerSln.Should().BeNull("bei genau einem Einstiegspunkt muss der Dropdown-Button des Split-Buttons unsichtbar sein");

        // Eine zweite "*.slnx" anlegen und die Aufgabe erneut neu laden.
        var zweiteSolution = Path.Combine(lokalerKlonPfad, "Zweite.slnx");
        File.WriteAllText(zweiteSolution, string.Empty);
        ReloadTaskDetail(mainWindow);

        // Phase 4: Bei mehreren "*.sln"-Dateien öffnet der Haupt-Button des Split-Buttons weiterhin direkt
        // den ersten (alphabetisch sortierten) Einstiegspunkt, ohne Auswahl-Dialog; der jetzt sichtbare
        // Dropdown-Button zeigt bei Klick den Auswahl-Dialog mit allen Einstiegspunkten an.
        var ideHauptButtonMitZweiSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        var ideDropdownButtonMitZweiSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnenDropdown"), Short);

        var protokollVorHauptklick = await ReadProzessStartLogAsync();
        ideHauptButtonMitZweiSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(ersteSolution, sinceContent: protokollVorHauptklick);

        var dialogNachHauptklick = mainWindow.FindFirstDescendant(cf => cf.ByName("Solution auswählen"));
        dialogNachHauptklick.Should().BeNull("der Haupt-Button des Split-Buttons öffnet weiterhin direkt, ohne Auswahl-Dialog");

        ideDropdownButtonMitZweiSln.AsButton().Click();

        var dialog = WaitForWindow("Solution auswählen", Short);
        var solutionListe = WaitForElement(dialog, cf => cf.ByName("SolutionAuswahl"), Short);
        var zweiterEintrag = WaitForElement(solutionListe, cf => cf.ByName(zweiteSolution), Short);
        zweiterEintrag.Click();

        var okButton = WaitForElement(dialog, cf => cf.ByName("OK"), Short);
        okButton.AsButton().Click();

        await WaitForProzessStartEintragAsync(zweiteSolution);

        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        NavigateBackFromProjectCardToProjectsList(mainWindow);
        DeleteCurrentProject(mainWindow);
    }

    /// <summary>
    /// Szenario: Für das Repository ist ein Arbeitsunterverzeichnis (<c>RepositoryStartKonfiguration.WorkingDirectoryRelativePath</c>)
    /// konfiguriert. „Arbeitsverzeichnis öffnen" muss dieses aufgelöste Unterverzeichnis öffnen (nicht den
    /// Repository-Root), „IDE öffnen" muss - solange keine Solution im Unterverzeichnis liegt - über
    /// PluginSelectionService.ResolveIdePluginAsync automatisch auf das (standardmäßig aktive)
    /// Visual-Studio-Code-Plugin direkt mit dem aufgelösten Unterverzeichnis zurückfallen, und sobald eine
    /// Solution im Unterverzeichnis liegt, muss diese gefunden und geöffnet werden (nicht im Root) - der
    /// Beweis, dass die Solution-Suche im aufgelösten Arbeitsverzeichnis stattfindet.
    /// </summary>
    protected async Task VerzeichnisAktionen_KonfiguriertesArbeitsverzeichnisWirdAufgeloest_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabe(mainWindow, "WorkDir-Ribbon-Repo", "WorkDir-Ribbon-Projekt");

        ConfirmLocalDirectoryGitInitInSourceDirectory();
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        var unterverzeichnis = Path.Combine(lokalerKlonPfad, "backend");
        Directory.CreateDirectory(unterverzeichnis);

        await SeedRepositoryWorkingDirectoryAsync("backend");

        // Aufgabe neu laden, damit TaskDetailViewModel die soeben hinterlegte RepositoryStartKonfiguration liest.
        ReloadTaskDetail(mainWindow);

        // Phase 1: "Arbeitsverzeichnis öffnen" muss das aufgelöste Unterverzeichnis öffnen, nicht den Repository-Root.
        var arbeitsverzeichnisButton = WaitForElement(mainWindow, cf => cf.ByName("ArbeitsverzeichnisOeffnen"), Short);
        arbeitsverzeichnisButton.AsButton().Click();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(unterverzeichnis));

        // Phase 2: Ohne "*.sln" im Unterverzeichnis ist "IDE öffnen" weiterhin aktiv (CanExecute hängt nur
        // vom vorhandenen Arbeitsverzeichnis ab) und fällt über ResolveIdePluginAsync automatisch auf das
        // standardmäßig aktive Visual-Studio-Code-Plugin zurück, das direkt mit dem aufgelösten
        // Unterverzeichnis öffnet - ohne Solution-Auswahl-Dialog. Da Phase 1 bereits einen Prozessstart mit
        // demselben aufgelösten Pfad aufgezeichnet hat, wird nur der seit diesem Zeitpunkt neu hinzugekommene
        // Teil der Log-Datei geprüft.
        var protokollVorFallback = await ReadProzessStartLogAsync();

        var ideButtonOhneSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonOhneSln.Properties.IsEnabled.Value.Should().BeTrue("das konfigurierte Arbeitsverzeichnis existiert, auch ohne .sln-Datei");
        ideButtonOhneSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(unterverzeichnis), sinceContent: protokollVorFallback);

        var solutionAuswahlDialogBeiFallback = mainWindow.FindFirstDescendant(cf => cf.ByName("Solution auswählen"));
        solutionAuswahlDialogBeiFallback.Should().BeNull("ohne gefundene Solution öffnet der Fallback Visual Studio Code direkt, ohne Auswahl-Dialog");

        // Eine "*.sln" NUR im Unterverzeichnis anlegen (nicht im Repository-Root) und neu laden.
        var solutionImUnterverzeichnis = Path.Combine(unterverzeichnis, "Backend.sln");
        File.WriteAllText(solutionImUnterverzeichnis, string.Empty);
        ReloadTaskDetail(mainWindow);

        // Phase 3: "IDE öffnen" muss die Solution aus dem Unterverzeichnis finden und öffnen - der Repository-Root
        // enthält keine Solution, das kann also nur gelingen, wenn tatsächlich im aufgelösten Verzeichnis gesucht wurde.
        var ideButtonMitSln = WaitForElement(mainWindow, cf => cf.ByName("IdeOeffnen"), Short);
        ideButtonMitSln.Properties.IsEnabled.Value.Should().BeTrue("es liegt jetzt eine .sln-Datei im konfigurierten Arbeitsverzeichnis");
        ideButtonMitSln.AsButton().Click();
        await WaitForProzessStartEintragAsync(solutionImUnterverzeichnis);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
    }

    /// <summary>
    /// Hinterlegt für das (einzige) zugewiesene Repository eine RepositoryStartKonfiguration mit dem
    /// übergebenen relativen Arbeitsverzeichnis-Pfad direkt in der Test-Datenbank.
    /// </summary>
    /// <param name="workingDirectoryRelativePath">Relativer Arbeitsverzeichnis-Pfad, der hinterlegt wird.</param>
    private async Task SeedRepositoryWorkingDirectoryAsync(string workingDirectoryRelativePath)
    {
        await using var db = OpenTestDbContext();
        var repository = db.GitRepositories.Single();

        db.Add(new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            WorkingDirectoryRelativePath = workingDirectoryRelativePath,
            Aktiv = true
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Liest den aktuellen Inhalt der Prozessstart-Logdatei (leer, falls sie noch nicht existiert).</summary>
    /// <returns>Der vollständige aktuelle Inhalt der Logdatei, oder <see cref="string.Empty"/>.</returns>
    private async Task<string> ReadProzessStartLogAsync()
    {
        var pfad = ResolveProzessStartLogPfad();
        return File.Exists(pfad) ? await File.ReadAllTextAsync(pfad) : string.Empty;
    }
}
