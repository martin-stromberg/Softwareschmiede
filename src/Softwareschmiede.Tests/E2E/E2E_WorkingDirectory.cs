using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Ausführung der CLI in einem konfigurierten Arbeitsunterverzeichnis (Issue #98).
///
/// Da aktuell kein Git-Plugin <c>IGitPlugin.GetRepositoryStructureAsync</c> implementiert (die
/// Verzeichnisstruktur-Vorschau im Zuweisungsdialog fällt daher auf "." zurück), wird das
/// Arbeitsverzeichnis hier direkt in der Test-Datenbank hinterlegt (<see cref="WpfTestBase.OpenTestDbContext"/>),
/// nachdem das Repository über die UI zugewiesen wurde. Die eigentliche Auswirkung (CLI-Start im
/// konfigurierten Unterverzeichnis bzw. Fehlerbehandlung) wird vollständig über die UI verifiziert.
///
/// CI-Ausschluss: dotnet test --filter "Category!=E2E"
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public sealed class E2E_WorkingDirectory : WpfTestBase
{
    /// <summary>
    /// Szenario: Repository mit konfiguriertem Arbeitsunterverzeichnis wird gestartet.
    /// Erwartung: CLI startet erfolgreich (Stoppen-Button erscheint), kein Fehlerbanner.
    /// </summary>
    [Fact]
    public async Task AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Repo", "WorkingDir-Projekt");

        await SeedWorkingDirectoryAsync("backend", createSubdirectory: true);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var stoppenButton = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Medium);
        Assert.NotNull(stoppenButton);

        var fehlerMeldung = mainWindow.FindFirstDescendant(cf => cf.ByName("FehlerMeldung"));
        Assert.Null(fehlerMeldung);
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis existiert nach dem Start nicht im Repository.
    /// Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    [Fact]
    public async Task AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Missing-Repo", "WorkingDir-Missing-Projekt");

        await SeedWorkingDirectoryAsync("does-not-exist", createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Medium);
        Assert.NotNull(fehlerBanner);

        var stoppenButton = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        Assert.Null(stoppenButton);
    }

    /// <summary>
    /// Szenario: Konfiguriertes Arbeitsverzeichnis versucht, das Repository-Verzeichnis per Path-Traversal
    /// zu verlassen. Erwartung: Fehlerbanner erscheint, CLI startet nicht.
    /// </summary>
    [Fact]
    public async Task AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E()
    {
        var mainWindow = SetupProjectMitNeuerAufgabe("WorkingDir-Traversal-Repo", "WorkingDir-Traversal-Projekt");

        await SeedWorkingDirectoryAsync(Path.Combine("..", "..", "etc"), createSubdirectory: false);

        new WindowsCredentialStore().SetCredential("LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "true");
        StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");

        var fehlerBanner = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Medium);
        Assert.NotNull(fehlerBanner);

        var stoppenButton = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
        Assert.Null(stoppenButton);
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
}
