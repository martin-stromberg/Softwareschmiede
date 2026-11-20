using FlaUI.Core.AutomationElements;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die automatische Ausführung des Repository-Initialisierungsskripts nach dem Klonen
/// (Issue #228): Erfolgreiche Ausführung sowie Fehlertoleranz (die Aufgabe wird trotz fehlschlagendem
/// Initialisierungsskript nicht blockiert).
///
/// Konfiguriert das LocalDirectoryPlugin bewusst im <c>SeparateWorkingDirectory</c>-Modus: Nur dort
/// kopiert der Klon-Schritt den tatsächlichen Repository-Inhalt (inkl. des zuvor im Quellverzeichnis
/// hinterlegten Initialisierungsskripts) in das Arbeitsverzeichnis, das <c>Aufgabe.LokalerKlonPfad</c>
/// entspricht. Im <c>InSourceDirectory</c>-Modus enthält dieser Pfad dagegen nur eine Pointer-Datei auf
/// das Quellverzeichnis (siehe <see cref="Softwareschmiede.Domain.Interfaces.IGitPlugin.ResolveEffectiveRepositoryPathAsync"/>),
/// wodurch das Skript dort nicht auffindbar wäre.
///
/// Beide Szenarien starten (wie z. B. <c>End2EndTest.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E</c>
/// in <c>E2E_WorkingDirectory.cs</c>) einen tatsächlich laufenden CLI-Prozess und bleiben deshalb als
/// gemeinsamer <c>[Fact]</c> mit eigenem App-Lifecycle bestehen; beide Phasen stoppen den
/// CLI-Prozess vor dem Aufräumen explizit, bevor die jeweils nächste Phase beginnt.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_RepositoryInitializationExecutionTests : WpfTestBase
{
    private const string MarkerFileName = "init-marker.txt";

    /// <summary>
    /// Führt beide Ausführungsszenarien nacheinander im selben App-Lifecycle aus: Erfolgreiche
    /// Ausführung des Initialisierungsskripts nach dem Klonen, danach Fehlertoleranz bei einem
    /// fehlschlagenden Initialisierungsskript.
    /// </summary>
    [Fact]
    public async Task InitializationscriptExecution()
    {
        SkipWhenConPtyNotAvailable();

        var mainWindow = LaunchAppAndGetMainWindow();

        await InitializationscriptWillAfterCloneExecuted_E2E(mainWindow);
        await FailingInitializationscript_BlocksWorkItemNot_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein aktives Initialisierungsskript ist konfiguriert.
    /// Erwartung: Nach dem Klonen wird das Skript automatisch ausgeführt (sichtbar an der von ihm
    /// erzeugten Marker-Datei), bevor die CLI startet.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitializationscriptWillAfterCloneExecuted_E2E(Window mainWindow)
    {
        SetupProjectWithNewWorkItemForStartedApp(mainWindow, "Init-Happy-Repo", "Init-Happy-Projekt", useInSourceDirectoryMode: false);

        await SeedInitializationscriptAsync(
            "scripts/init.ps1",
            $"\"init erfolgreich\" | Out-File -FilePath '{MarkerFileName}' -Encoding utf8\r\n");

        StartAndPluginSelect(mainWindow, "Softwareschmiede.KiSimulator");

        var stopButton = WaitForElement(mainWindow, cf => cf.ByName("StopCli"), Medium);
        Assert.NotNull(stopButton);

        var localClonePath = await WaitForLocalClonePathAsync();
        Assert.False(string.IsNullOrWhiteSpace(localClonePath), "Aufgabe hat keinen lokalen Klonpfad erhalten.");

        var markerPath = Path.Combine(localClonePath!, MarkerFileName);
        Assert.True(File.Exists(markerPath), $"Erwartete Marker-Datei '{markerPath}' wurde vom Initialisierungsskript nicht erzeugt.");

        StopCliAndCleanOn(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein aktives Initialisierungsskript ist konfiguriert, das fehlschlägt.
    /// Erwartung: Die Aufgabe wird trotzdem normal gestartet (CLI läuft, kein Fehlerbanner), der Fehler
    /// wird als Protokolleintrag mit entsprechendem Hinweis festgehalten.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task FailingInitializationscript_BlocksWorkItemNot_E2E(Window mainWindow)
    {
        SetupProjectWithNewWorkItemForStartedApp(mainWindow, "Init-Fail-Repo", "Init-Fail-Projekt", useInSourceDirectoryMode: false);

        await SeedInitializationscriptAsync("scripts/fail.ps1", "exit 1\r\n");

        StartAndPluginSelect(mainWindow, "Softwareschmiede.KiSimulator");

        var stopButton = WaitForElement(mainWindow, cf => cf.ByName("StopCli"), Medium);
        Assert.NotNull(stopButton);

        var errorBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("ErrorMessage"));
        Assert.Null(errorBanner);

        var noticeFound = await WaitForInitializationscriptErrorLogAsync();
        Assert.True(noticeFound, "Protokolleintrag mit Hinweis zum fehlgeschlagenen Initialisierungsskript wurde nicht gefunden.");

        StopCliAndCleanOn(mainWindow);
    }

    private void StopCliAndCleanOn(Window mainWindow)
    {
        var stopButton = WaitForElement(mainWindow, cf => cf.ByName("StopCli"), Short);
        stopButton.AsButton().ClickInForeground();
        WaitUntilGone(mainWindow, cf => cf.ByName("StopCli"), Medium);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Legt das Initialisierungsskript im Quellverzeichnis des (einzigen) zugewiesenen Repositories ab
    /// und hinterlegt die zugehörige aktive <see cref="RepositoryInitializationConfiguration"/> direkt
    /// in der Test-Datenbank.
    /// </summary>
    private async Task SeedInitializationscriptAsync(string relativeScriptPath, string scriptContent)
    {
        await using var db = OpenTestDbContext();
        var repository = db.GitRepositories.Single();

        var scriptFullPath = Path.Combine(repository.RepositoryUrl, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar));
        var scriptDirectory = Path.GetDirectoryName(scriptFullPath);
        if (!string.IsNullOrWhiteSpace(scriptDirectory))
        {
            Directory.CreateDirectory(scriptDirectory);
        }
        await File.WriteAllTextAsync(scriptFullPath, scriptContent);

        db.Add(new RepositoryInitializationConfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            InitializationScriptRelativePath = relativeScriptPath,
            Active = true
        });
        await db.SaveChangesAsync();
    }

    private async Task<string?> WaitForLocalClonePathAsync()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var workItem = db.WorkItems.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(workItem?.LocalClonePath))
            {
                return workItem.LocalClonePath;
            }

            await Task.Delay(200);
        }

        return null;
    }

    private async Task<bool> WaitForInitializationscriptErrorLogAsync()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            if (db.LogEntries.Any(p => p.Content.Contains("Repository-Initialisierungsskript konnte nicht ausgeführt werden")))
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }
}
