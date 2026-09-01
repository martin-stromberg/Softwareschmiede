using FlaUI.Core.AutomationElements;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Ausführung der CLI in einem konfigurierten Arbeitsunterverzeichnis (Issue #98).
///
/// Der Testmodus lädt als SCM-Plugin ausschließlich <c>LocalDirectoryPlugin</c>. Die Dialog-Tests
/// verifizieren damit deterministisch denselben Result-Pfad für erfolgreiche Strukturabrufe und
/// Fehler-Fallbacks, den Remote-Plugins wie Bitbucket verwenden. Die Start-Tests hinterlegen das
/// Arbeitsverzeichnis weiterhin gezielt in der Test-Datenbank, weil dort die spätere CLI-Auswirkung
/// im Vordergrund steht.
///
/// Konsolidierung (Issue #153): <see cref="RepositoryZuweisung"/> führt fünf der sechs Szenarien
/// (beide Repository-Zuweisungs-Pfade, die Arbeitsverzeichnis-Bearbeitung sowie die beiden
/// Start-Fehlerfälle) als aufeinanderfolgende Phasen in einem gemeinsamen App-Lifecycle aus. Jede
/// Phase räumt ihr Projekt bzw. ihre Aufgabe über <see cref="Views.ProjectDetailView.DeleteProject"/> /
/// <see cref="Views.TaskDetailView.DeleteTask"/> wieder auf, bevor die nächste Phase beginnt - damit bleiben
/// DB-Abfragen wie <c>Single()</c>/<c>SingleOrDefault()</c> gültig, obwohl mehrere Projekte/Repositories
/// nacheinander im selben Prozess angelegt werden. Die Fallback-Zuweisung
/// (<see cref="RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E"/>)
/// initialisiert ihr lokales Quellverzeichnis bewusst ohne Git (<c>CreateLocalSourceDirectory(..., false)</c>):
/// Der zuvor beobachtete <see cref="UnauthorizedAccessException"/> beim Löschen des Repository-Ordners
/// trat gezielt bei einem tatsächlich initialisierten Git-Repository auf (der Zuweisungsdialog hält
/// währenddessen einen Datei-Zugriff auf das Git-Verzeichnis offen); da dieser Test lediglich den
/// Fallback-Pfad ohne Strukturabruf prüft, ist kein echtes Git-Repository nötig.
///
/// <see cref="AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E"/> bleibt
/// als eigenständiger <c>[SkippableFact]</c> mit eigenem App-Lifecycle bestehen, da er als einziges
/// Szenario einen erfolgreich laufenden CLI-Prozess (Status "CliStoppen") hinterlässt und daher vor
/// einer Konsolidierung mit den übrigen Phasen zusätzliches Stoppen des Prozesses vor dem Aufräumen
/// erfordern würde.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt fünf Arbeitsverzeichnis-Szenarien nacheinander in einem gemeinsamen App-Lifecycle aus:
    /// Repository-Zuweisung mit fehlgeschlagenem Strukturabruf (manueller Pfad), Repository-Zuweisung
    /// mit erfolgreichem Strukturabruf (Auswahlbox), Arbeitsverzeichnis-Bearbeitung mit fehlgeschlagenem
    /// Strukturabruf, sowie die beiden Start-Fehlerfälle (fehlendes Arbeitsverzeichnis, Path-Traversal).
    /// Jede Phase räumt ihr Projekt bzw. ihre Aufgabe auf, bevor die nächste beginnt.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem alle Phasen ausgeführt werden.</param>
    protected async Task RepositoryZuweisung(Window mainWindow)
    {
        await RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E(mainWindow);
        await RepositoryZuweisen_MitErfolgreichemStrukturabruf_ZeigtUndSpeichertArbeitsverzeichnis_E2E(mainWindow);

        await ArbeitsverzeichnisBearbeiten_MitFehlgeschlagenemStrukturabruf_ZeigtUndBestaetigtVorhandenenWert_E2E(mainWindow);
        await AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E(mainWindow);
        await AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E(mainWindow);
    }

    private string CreateLocalSourceDirectoryWithSubdirectories(string repositoryFolderName, params string[] subdirectories)
    {
        var sourceDirectory = CreateLocalSourceDirectory(repositoryFolderName);
        foreach (var subdirectory in subdirectories)
        {
            Directory.CreateDirectory(Path.Combine(sourceDirectory, repositoryFolderName, subdirectory));
        }
        return sourceDirectory;
    }


    /// <summary>
    /// Szenario: Repository-Zuweisung mit erfolgreichem Strukturabruf.
    /// Erwartung: Die Auswahlbox zeigt Unterverzeichnisse und die Auswahl wird gespeichert.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task RepositoryZuweisen_MitErfolgreichemStrukturabruf_ZeigtUndSpeichertArbeitsverzeichnis_E2E(Window mainWindow)
    {
        var repoFolderName = "WorkingDir-Assign-Success-Repo";
        var sourceDirectory = CreateLocalSourceDirectoryWithSubdirectories(repoFolderName, "backend", "frontend");
        var projektName = "WorkingDir-Assign-Success-Projekt";

        var settings = new SettingsView(mainWindow).ForceShow();
        var dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory);
        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject(projektName);
        var projectDetail = projectList.OpenProject(projektName);

        var dialog = new RepositoryAssignDialogView(mainWindow).ForceShow();
        dialog.SelectFirstRepository();
        dialog.SelectWorkingDirectory("backend");
        var projectDetailAfterAssign = dialog.Confirm();

        var saved = await WaitForSavedWorkingDirectoryAsync(repoFolderName, "backend");
        Assert.Equal("backend", saved);

        var projectListAfterDelete = projectDetailAfterAssign.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Repository-Zuweisung mit fehlgeschlagenem Strukturabruf.
    /// Erwartung: Eine TextBox erscheint und speichert einen manuellen relativen Pfad.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E(Window mainWindow)
    {
        var repositoryName = "WorkingDir-Assign-Fallback-Repo";
        var sourceDirectory = CreateLocalSourceDirectory(repositoryName, false);
        var repositoryPath = Path.Combine(sourceDirectory, repositoryName);

        var settings = new SettingsView(mainWindow).ForceShow();
        var dashboard = settings.ConfigureLocalDirectoryPlugin(sourceDirectory);
        var projectList = dashboard.Menu.NavigateToProjects();
        projectList.CreateProject("WorkingDir-Assign-Fallback-Projekt");
        var projectDetail = projectList.OpenProject("WorkingDir-Assign-Fallback-Projekt");

        var dialog = new RepositoryAssignDialogView(mainWindow).ForceShow();

        // Repository-Verzeichnis wird gezielt zwischen dem Erscheinen des Listeneintrags und dem Klick
        // darauf gelöscht, um den Fallback-Pfad (fehlgeschlagener Strukturabruf) deterministisch auszulösen.
        var repositoryItem = dialog.WaitForFirstRepositoryItem();
        Directory.Delete(repositoryPath, recursive: true);
        repositoryItem.Click();

        dialog.SetManualWorkingDirectory(@"manual\backend");
        var projectDetailAfterAssign = dialog.Confirm();

        var saved = await WaitForSavedWorkingDirectoryAsync(repositoryName, "manual/backend");
        Assert.Equal("manual/backend", saved);

        var projectListAfterDelete = projectDetailAfterAssign.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Arbeitsverzeichnis-Bearbeitung mit fehlgeschlagenem Strukturabruf.
    /// Erwartung: Der vorhandene manuelle Wert erscheint im Textfeld und kann bestätigt werden.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task ArbeitsverzeichnisBearbeiten_MitFehlgeschlagenemStrukturabruf_ZeigtUndBestaetigtVorhandenenWert_E2E(Window mainWindow)
    {
        var projektName = "WorkingDir-Edit-Fallback-Projekt";
        var repositoryName = "WorkingDir-Edit-Fallback-Repo";
        var repositoryUrl = Path.Combine(Path.GetTempPath(), $"softwareschmiede_e2e_missing_repo_{Guid.NewGuid():N}");
        const string existingWorkingDirectory = "legacy/backend";

        await SeedProjectRepositoryWithWorkingDirectoryAsync(projektName, repositoryName, repositoryUrl, existingWorkingDirectory);

        var projectList = new ProjectListView(mainWindow).ForceShow();
        var projectDetail = projectList.OpenProject(projektName);

        var dialog = new ArbeitsverzeichnisBearbeitenDialogView(mainWindow).ForceShow();
        Assert.Equal(existingWorkingDirectory, dialog.GetManualPath());

        var projectDetailAfterSave = dialog.Confirm();

        var saved = await WaitForSavedWorkingDirectoryAsync(repositoryName, existingWorkingDirectory);
        Assert.Equal(existingWorkingDirectory, saved);

        var projectListAfterDelete = projectDetailAfterSave.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Repository mit konfiguriertem Arbeitsunterverzeichnis wird gestartet.
    /// Erwartung: CLI startet erfolgreich (Stoppen-Button erscheint), kein Fehlerbanner.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    protected async Task AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E(Window mainWindow)
    {
        SetupProjectMitNeuerAufgabe(mainWindow, "WorkingDir-Repo", "WorkingDir-Projekt");

        await SeedWorkingDirectoryAsync("backend", createSubdirectory: true);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();

        Assert.False(new ErrorView(mainWindow).IsVisible);

        taskDetail.GoBack();
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var projectListAfterDelete = projectDetail.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis existiert nach dem Start nicht im Repository.
    /// Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    /// <remarks>
    /// Regressionsabdeckung für den Fix in <see cref="WpfTestBase.WaitForElement"/>: Dieser Test
    /// wartet selbst auf das Element "FehlerMeldung" (Zeile unten), das zugleich die proaktive
    /// Fail-Fast-Diagnose in <c>WaitForElement</c> auslöst. Vor dem Fix führte das dazu, dass der Test
    /// fälschlich mit einer <see cref="InvalidOperationException"/> statt mit dem erwarteten Treffer
    /// abbrach (nicht-atomare UI-Automation-Aufrufe, siehe Doku an <c>WaitForElement</c>). Ein isolierter
    /// Unit-Test für <c>WaitForElement</c> selbst ist nicht sinnvoll möglich: <c>AutomationElement</c> und
    /// <c>ConditionFactory</c> sind eng an eine echte, native UI-Automation-Instanz (FlaUI/UIA3, COM-basiert)
    /// gekoppelt und bieten keine Testschnittstellen/-doubles für eine In-Memory-Simulation. Die Verifikation
    /// erfolgt daher über diesen und den analogen Path-Traversal-Test (beide mehrfach wiederholt grün, siehe
    /// continue.md).
    /// </remarks>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E(Window mainWindow)
    {
        _ = SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, "WorkingDir-Missing-Repo", "WorkingDir-Missing-Projekt");

        await SeedWorkingDirectoryAsync("does-not-exist", createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        Assert.False(string.IsNullOrWhiteSpace(new ErrorView(mainWindow).GetErrorMessage()));
        Assert.False(taskDetail.IsCliRunning());

        var projectDetail = taskDetail.DeleteTask();
        var projectListAfterDelete = projectDetail.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis versucht, das Repository-Verzeichnis per Path-Traversal
    /// zu verlassen. Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem diese Phase ausgeführt wird.</param>
    private async Task AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E(Window mainWindow)
    {
        _ = SetupProjectMitNeuerAufgabeForStartedApp(mainWindow, "WorkingDir-Traversal-Repo", "WorkingDir-Traversal-Projekt");

        await SeedWorkingDirectoryAsync(Path.Combine("..", "..", "etc"), createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        Assert.False(string.IsNullOrWhiteSpace(new ErrorView(mainWindow).GetErrorMessage()));
        Assert.False(taskDetail.IsCliRunning());

        var projectDetail = taskDetail.DeleteTask();
        var projectListAfterDelete = projectDetail.DeleteProject();
        projectListAfterDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Hinterlegt für das (einzige) zugewiesene Repository ein Arbeitsverzeichnis direkt in der
    /// Test-Datenbank und legt optional das entsprechende Unterverzeichnis im lokalen Quellordner an.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad, der als Arbeitsverzeichnis hinterlegt wird.</param>
    /// <param name="createSubdirectory">Ob das Unterverzeichnis im lokalen Quellordner tatsächlich angelegt werden soll.</param>
    private async Task SeedWorkingDirectoryAsync(string relativePath, bool createSubdirectory)
    {
        await using var db = OpenTestDbContext();
        var repository = db.GitRepositories.Single();

        if (createSubdirectory)
        {
            Directory.CreateDirectory(Path.Combine(repository.RepositoryUrl, relativePath));
        }

        db.Add(new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            WorkingDirectoryRelativePath = relativePath,
            Aktiv = true
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedProjectRepositoryWithWorkingDirectoryAsync(
        string projektName,
        string repositoryName,
        string repositoryUrl,
        string workingDirectory)
    {
        await using var db = OpenTestDbContext();
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = projektName,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            PluginTyp = "LocalDirectoryPlugin",
            RepositoryName = repositoryName,
            RepositoryUrl = repositoryUrl,
            Aktiv = true
        };
        var configuration = new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repository.Id,
            WorkingDirectoryRelativePath = workingDirectory,
            Aktiv = true
        };

        db.Projekte.Add(projekt);
        db.GitRepositories.Add(repository);
        db.RepositoryStartKonfigurationen.Add(configuration);
        await db.SaveChangesAsync();
    }

    private async Task<string?> WaitForSavedWorkingDirectoryAsync(string repositoryName, string expected)
    {
        var deadline = DateTime.UtcNow + Medium;
        string? saved = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenTestDbContext();
            var repo = db.GitRepositories.FirstOrDefault(r => r.RepositoryName == repositoryName);
            saved = repo is null
                ? null
                : db.RepositoryStartKonfigurationen.Where(c => c.GitRepositoryId == repo.Id).SingleOrDefault()?.WorkingDirectoryRelativePath;
            if (string.Equals(saved, expected, StringComparison.Ordinal))
                return saved;

            await Task.Delay(200);
        }

        return saved;
    }
}
