using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

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
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        // Nach erfolgreichem Start ist das Repository geklont (LokalerKlonPfad gesetzt).
        taskDetail.WaitForCliRunning();

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();

        // Phase 1: "Arbeitsverzeichnis öffnen" zeichnet den OS-Dateiexplorer-Start mit dem LokalerKlonPfad auf.
        taskDetail.OpenWorkingDirectory();
        await WaitForProzessStartEintragAsync(lokalerKlonPfad);

        // Phase 2: Ohne "*.sln" ist "IDE öffnen" weiterhin aktiv (CanExecute hängt nur noch vom vorhandenen
        // Arbeitsverzeichnis ab, nicht mehr von gefundenen Solutions) - ein Klick löst über
        // PluginSelectionService.ResolveIdePluginAsync automatisch den Visual-Studio-Code-Fallback aus, da
        // Visual Studio Code standardmäßig als aktives IDE-Plugin gilt.
        Assert.True(taskDetail.IsIdeButtonEnabled(), "das Arbeitsverzeichnis existiert bereits, auch ohne .sln-Datei");

        var protokollVorFallback = await ReadProzessStartLogAsync();
        taskDetail.OpenIde();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(lokalerKlonPfad), sinceContent: protokollVorFallback);

        Assert.False(new SolutionSelectionDialogView(mainWindow).IsVisible, "ohne gefundene Solution öffnet der Visual-Studio-Code-Fallback direkt, ohne Auswahl-Dialog");

        // Genau eine "*.sln" anlegen und die Aufgabe neu laden (Ribbon-Button-CanExecute wird beim Laden gecacht).
        var ersteSolution = Path.Combine(lokalerKlonPfad, "Erste.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        taskDetail = taskDetail.Reload();

        // Phase 3: Bei genau einer "*.sln" öffnet der Haupt-Button von "IDE öffnen" diese weiterhin direkt,
        // ohne Auswahl-Dialog (Visual Studio ist als Explicit-Plugin kompatibel und gewinnt beim Haupt-Button
        // gegenüber dem Fallback). Der Dropdown-Button ist aber bereits jetzt sichtbar: Zusätzlich zum einen
        // Visual-Studio-Einstiegspunkt liefert das weiterhin aktive Visual-Studio-Code-Fallback-Plugin einen
        // weiteren Einstiegspunkt - aggregiert über alle kompatiblen Plugins sind das 2 Einstiegspunkte.
        Assert.True(taskDetail.IsIdeButtonEnabled(), "es existiert jetzt genau eine .sln-Datei");
        taskDetail.OpenIde();
        await WaitForProzessStartEintragAsync(ersteSolution);

        Assert.False(new SolutionSelectionDialogView(mainWindow).IsVisible, "der Haupt-Button des Split-Buttons öffnet weiterhin direkt, ohne Auswahl-Dialog");
        Assert.True(taskDetail.HasIdeDropdown(), "Visual Studio liefert die eine .sln als Explicit-Einstiegspunkt, zusätzlich liefert das weiterhin aktive Visual-Studio-Code-Fallback-Plugin einen weiteren Einstiegspunkt - aggregiert sind das 2 Einstiegspunkte, der Dropdown-Button muss also sichtbar sein");

        // Eine zweite "*.slnx" anlegen und die Aufgabe erneut neu laden.
        var zweiteSolution = Path.Combine(lokalerKlonPfad, "Zweite.slnx");
        File.WriteAllText(zweiteSolution, string.Empty);
        taskDetail = taskDetail.Reload();

        // Phase 4: Bei mehreren "*.sln"-Dateien öffnet der Haupt-Button des Split-Buttons weiterhin direkt
        // den ersten (alphabetisch sortierten) Einstiegspunkt, ohne Auswahl-Dialog; der weiterhin sichtbare
        // Dropdown-Button zeigt bei Klick den Auswahl-Dialog mit allen (jetzt 3, plugin-qualifiziert
        // formatierten) Einstiegspunkten an.
        Assert.True(taskDetail.HasIdeDropdown());

        var protokollVorHauptklick = await ReadProzessStartLogAsync();
        taskDetail.OpenIde();
        await WaitForProzessStartEintragAsync(ersteSolution, sinceContent: protokollVorHauptklick);

        Assert.False(new SolutionSelectionDialogView(mainWindow).IsVisible, "der Haupt-Button des Split-Buttons öffnet weiterhin direkt, ohne Auswahl-Dialog");

        var solutionDialog = taskDetail.OpenIdeDropdown();
        // Die Liste zeigt plugin-qualifizierte Anzeigewerte ("{PluginName}: {Dateiname}"), keine Rohpfade.
        solutionDialog.SelectSolution($"Visual Studio: {Path.GetFileName(zweiteSolution)}");
        solutionDialog.Confirm();

        await WaitForProzessStartEintragAsync(zweiteSolution);

        mainWindow.AsWindow().Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
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
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();

        var lokalerKlonPfad = await GetLokalerKlonPfadAsync();
        var unterverzeichnis = Path.Combine(lokalerKlonPfad, "backend");
        Directory.CreateDirectory(unterverzeichnis);

        await SeedRepositoryWorkingDirectoryAsync("backend");

        // Aufgabe neu laden, damit TaskDetailViewModel die soeben hinterlegte RepositoryStartKonfiguration liest.
        taskDetail = taskDetail.Reload();

        // Phase 1: "Arbeitsverzeichnis öffnen" muss das aufgelöste Unterverzeichnis öffnen, nicht den Repository-Root.
        taskDetail.OpenWorkingDirectory();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(unterverzeichnis));

        // Phase 2: Ohne "*.sln" im Unterverzeichnis ist "IDE öffnen" weiterhin aktiv (CanExecute hängt nur
        // vom vorhandenen Arbeitsverzeichnis ab) und fällt über ResolveIdePluginAsync automatisch auf das
        // standardmäßig aktive Visual-Studio-Code-Plugin zurück, das direkt mit dem aufgelösten
        // Unterverzeichnis öffnet - ohne Solution-Auswahl-Dialog. Da Phase 1 bereits einen Prozessstart mit
        // demselben aufgelösten Pfad aufgezeichnet hat, wird nur der seit diesem Zeitpunkt neu hinzugekommene
        // Teil der Log-Datei geprüft.
        var protokollVorFallback = await ReadProzessStartLogAsync();

        Assert.True(taskDetail.IsIdeButtonEnabled(), "das konfigurierte Arbeitsverzeichnis existiert, auch ohne .sln-Datei");
        taskDetail.OpenIde();
        await WaitForProzessStartEintragAsync(Path.GetFullPath(unterverzeichnis), sinceContent: protokollVorFallback);

        Assert.False(new SolutionSelectionDialogView(mainWindow).IsVisible, "ohne gefundene Solution öffnet der Fallback Visual Studio Code direkt, ohne Auswahl-Dialog");

        // Eine "*.sln" NUR im Unterverzeichnis anlegen (nicht im Repository-Root) und neu laden.
        var solutionImUnterverzeichnis = Path.Combine(unterverzeichnis, "Backend.sln");
        File.WriteAllText(solutionImUnterverzeichnis, string.Empty);
        taskDetail = taskDetail.Reload();

        // Phase 3: "IDE öffnen" muss die Solution aus dem Unterverzeichnis finden und öffnen - der Repository-Root
        // enthält keine Solution, das kann also nur gelingen, wenn tatsächlich im aufgelösten Verzeichnis gesucht wurde.
        Assert.True(taskDetail.IsIdeButtonEnabled(), "es liegt jetzt eine .sln-Datei im konfigurierten Arbeitsverzeichnis");
        taskDetail.OpenIde();
        await WaitForProzessStartEintragAsync(solutionImUnterverzeichnis);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
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
