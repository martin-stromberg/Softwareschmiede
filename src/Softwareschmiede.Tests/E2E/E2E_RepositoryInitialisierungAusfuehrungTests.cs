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
/// gemeinsamer <c>[SkippableFact]</c> mit eigenem App-Lifecycle bestehen; beide Phasen stoppen den
/// CLI-Prozess vor dem Aufräumen explizit, bevor die jeweils nächste Phase beginnt.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_RepositoryInitialisierungAusfuehrungTests : WpfTestBase
{
    private const string MarkerDateiName = "init-marker.txt";

    /// <summary>
    /// Führt beide Ausführungsszenarien nacheinander im selben App-Lifecycle aus: Erfolgreiche
    /// Ausführung des Initialisierungsskripts nach dem Klonen, danach Fehlertoleranz bei einem
    /// fehlschlagenden Initialisierungsskript.
    /// </summary>
    [SkippableFact]
    public async Task InitialisierungsskriptAusfuehrung()
    {
        SkipWennConPtyNichtVerfuegbar();

        var mainWindow = LaunchAppAndGetMainWindow();

        await InitialisierungsskriptWirdNachKlonAusgefuehrt_E2E(mainWindow);
        await FehlschlagendesInitialisierungsskript_BlockiertAufgabeNicht_E2E(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein aktives Initialisierungsskript ist konfiguriert.
    /// Erwartung: Nach dem Klonen wird das Skript automatisch ausgeführt (sichtbar an der von ihm
    /// erzeugten Marker-Datei), bevor die CLI startet.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task InitialisierungsskriptWirdNachKlonAusgefuehrt_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, "Init-Happy-Repo", "Init-Happy-Projekt", useInSourceDirectoryMode: false);

        await SeedInitialisierungsskriptAsync(
            "scripts/init.ps1",
            $"\"init erfolgreich\" | Out-File -FilePath '{MarkerDateiName}' -Encoding utf8\r\n");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var lokalerKlonPfad = await WaitForLokalerKlonPfadAsync();
        Assert.False(string.IsNullOrWhiteSpace(lokalerKlonPfad), "Aufgabe hat keinen lokalen Klonpfad erhalten.");

        var markerPfad = Path.Combine(lokalerKlonPfad!, MarkerDateiName);
        Assert.True(File.Exists(markerPfad), $"Erwartete Marker-Datei '{markerPfad}' wurde vom Initialisierungsskript nicht erzeugt.");

        StoppeCliUndRaeumeAuf(mainWindow);
    }

    /// <summary>
    /// Szenario: Ein aktives Initialisierungsskript ist konfiguriert, das fehlschlägt.
    /// Erwartung: Die Aufgabe wird trotzdem normal gestartet (CLI läuft, kein Fehlerbanner), der Fehler
    /// wird als Protokolleintrag mit entsprechendem Hinweis festgehalten.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task FehlschlagendesInitialisierungsskript_BlockiertAufgabeNicht_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, "Init-Fail-Repo", "Init-Fail-Projekt", useInSourceDirectoryMode: false);

        await SeedInitialisierungsskriptAsync("scripts/fail.ps1", "exit 1\r\n");

        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var fehlerBanner = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerBanner);

        var hinweisGefunden = await WaitForInitialisierungsskriptFehlerProtokollAsync();
        Assert.True(hinweisGefunden, "Protokolleintrag mit Hinweis zum fehlgeschlagenen Initialisierungsskript wurde nicht gefunden.");

        StoppeCliUndRaeumeAuf(mainWindow);
    }

    private void StoppeCliUndRaeumeAuf(Window mainWindow)
    {
        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Short);
        stoppenButton.AsButton().Click();
        WaitUntilGone(mainWindow, cf => cf.ByName("CliStoppen"), Medium);

        NavigateBackFromTaskToProject(mainWindow);
        DeleteCurrentProject(mainWindow);
        NavigateBackToDashboard(mainWindow);
    }

    /// <summary>
    /// Legt das Initialisierungsskript im Quellverzeichnis des (einzigen) zugewiesenen Repositories ab
    /// und hinterlegt die zugehörige aktive <see cref="RepositoryInitialisierungKonfiguration"/> direkt
    /// in der Test-Datenbank.
    /// </summary>
    private async Task SeedInitialisierungsskriptAsync(string relativeScriptPath, string scriptContent)
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

        db.Add(new RepositoryInitialisierungKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            InitialisierungsskriptRelativePath = relativeScriptPath,
            Aktiv = true
        });
        await db.SaveChangesAsync();
    }

    private async Task<string?> WaitForLokalerKlonPfadAsync()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var aufgabe = db.Aufgaben.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(aufgabe?.LokalerKlonPfad))
            {
                return aufgabe.LokalerKlonPfad;
            }

            await Task.Delay(200);
        }

        return null;
    }

    private async Task<bool> WaitForInitialisierungsskriptFehlerProtokollAsync()
    {
        var deadline = DateTime.UtcNow + Medium;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            if (db.Protokolleintraege.Any(p => p.Inhalt.Contains("Repository-Initialisierungsskript konnte nicht ausgeführt werden")))
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }
}
