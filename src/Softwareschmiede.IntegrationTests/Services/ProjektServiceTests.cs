using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="ProjektService"/> mit echter SQLite-Datenbank.</summary>
public sealed class ProjektServiceTests
{
    /// <summary>
    /// Testet, dass ein Projekt erfolgreich erstellt und mit einem neuen Context wieder geladen werden kann.
    /// Prüft die tatsächliche Persistenz in der SQLite-Datenbankdatei.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistProjekt_WhenValidNameGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        // Act
        var created = await service.CreateAsync("Testprojekt Alpha", "Beschreibung des Testprojekts");

        // Assert – zweiter Context auf gleicher Datei liest zurück
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projekte.FindAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Testprojekt Alpha");
        loaded.Beschreibung.Should().Be("Beschreibung des Testprojekts");
        loaded.Status.Should().Be(ProjektStatus.Aktiv);
    }

    /// <summary>
    /// Testet, dass GetAllAsync alle vorhandenen Projekte alphabetisch sortiert zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ShouldReturnProjectsAlphabeticallySorted_WhenMultipleProjectsExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        await service.CreateAsync("Zebra Projekt", null);
        await service.CreateAsync("Alpha Projekt", null);
        await service.CreateAsync("Mitte Projekt", null);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha Projekt");
        result[1].Name.Should().Be("Mitte Projekt");
        result[2].Name.Should().Be("Zebra Projekt");
    }

    /// <summary>
    /// Testet, dass GetDetailAsync Repositories und Aufgaben eines Projekts per Include lädt.
    /// </summary>
    [Fact]
    public async Task GetDetailAsync_ShouldIncludeRepositoriesAndAufgaben_WhenProjektHasRelatedData()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        var projekt = await service.CreateAsync("Projekt mit Repos", null);
        await service.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/test/repo", "test-repo");
        await service.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/test/repo2", "test-repo2");

        // Act
        var detail = await service.GetDetailAsync(projekt.Id);

        // Assert
        detail.Should().NotBeNull();
        detail!.Repositories.Should().HaveCount(2);
        detail.Repositories.Should().AllSatisfy(r => r.ProjektId.Should().Be(projekt.Id));
    }

    /// <summary>
    /// Testet, dass ArchivierenAsync den Projektstatus auf Archiviert setzt und persistiert.
    /// </summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldSetStatusToArchiviert_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Zu archivierendes Projekt", null);

        // Act
        await service.ArchivierenAsync(projekt.Id);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projekte.FindAsync(projekt.Id);

        loaded!.Status.Should().Be(ProjektStatus.Archiviert);
    }

    /// <summary>
    /// Testet, dass DeleteAsync ein Projekt und seine Aufgaben kaskadierend löscht.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteAufgaben_WhenProjektIsDeleted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektService = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        var projekt = await projektService.CreateAsync("Zu löschendes Projekt", null);
        await aufgabeService.CreateAsync(projekt.Id, "Aufgabe 1", null);
        await aufgabeService.CreateAsync(projekt.Id, "Aufgabe 2", null);

        // Act
        await projektService.DeleteAsync(projekt.Id);

        // Assert – Projekt und Aufgaben sind weg
        await using var db2 = db.CreateNewContext();
        var geladenesP = await db2.Projekte.FindAsync(projekt.Id);
        var aufgaben = db2.Aufgaben.Where(a => a.ProjektId == projekt.Id).ToList();

        geladenesP.Should().BeNull();
        aufgaben.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass AddRepositoryAsync ein Repository zum Projekt hinzufügt und persistiert.
    /// </summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldPersistRepository_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Projekt mit Repository", null);

        // Act
        var repo = await service.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/myrepo",
            "myrepo");

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(repo.Id);

        loaded.Should().NotBeNull();
        loaded!.RepositoryName.Should().Be("myrepo");
        loaded.PluginTyp.Should().Be("GitHub");
        loaded.ProjektId.Should().Be(projekt.Id);
        loaded.Aktiv.Should().BeTrue();
    }

    /// <summary>
    /// Testet, dass AddRepositoryAsync im LocalDirectory-Flow SourceDirectory als Pflichtfeld verwendet
    /// und den Quellpfad korrekt als RepositoryUrl persistiert.
    /// </summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldPersistSourceDirectory_WhenLocalDirectoryPluginIsUsed()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Projekt mit lokalem Verzeichnis", null);
        var sourceDirectory = @"C:\repos\integration-local";

        // Act
        var repo = await service.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            new Dictionary<string, string>
            {
                ["SourceDirectory"] = sourceDirectory
            });

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(repo.Id);

        loaded.Should().NotBeNull();
        loaded!.RepositoryUrl.Should().Be(sourceDirectory);
        loaded.RepositoryName.Should().Be("integration-local");
        loaded.PluginTyp.Should().Be("LocalDirectoryPlugin");
    }

    /// <summary>
    /// Testet, dass UpdateAsync Name und Beschreibung eines Projekts korrekt persistiert.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Ursprünglicher Name", "Alt");

        // Act
        await service.UpdateAsync(projekt.Id, "Neuer Name", "Neue Beschreibung");

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projekte.FindAsync(projekt.Id);

        loaded!.Name.Should().Be("Neuer Name");
        loaded.Beschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>
    /// Testet, dass GetByIdAsync null zurückgibt, wenn das Projekt nicht existiert.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenProjektDoesNotExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Testet, dass DeleteAsync ein Repository korrekt entfernt.
    /// </summary>
    [Fact]
    public async Task RemoveRepositoryAsync_ShouldDeleteRepository_WhenRepositoryExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Projekt", null);
        var repo = await service.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/x/y", "y");

        // Act
        await service.RemoveRepositoryAsync(repo.Id);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(repo.Id);
        loaded.Should().BeNull();
    }
}
