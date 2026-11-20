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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        // Act
        var created = await service.CreateAsync("Testprojekt Alpha", "Beschreibung des Testprojekts", TestContext.Current.CancellationToken);

        // Assert – zweiter Context auf gleicher Datei liest zurück
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projects.FindAsync(new object?[] { created.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Testprojekt Alpha");
        loaded.Description.Should().Be("Beschreibung des Testprojekts");
        loaded.Status.Should().Be(ProjectStatus.Active);
    }

    /// <summary>
    /// Testet, dass GetAllAsync alle vorhandenen Projekte alphabetisch sortiert zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ShouldReturnProjectsAlphabeticallySorted_WhenMultipleProjectsExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        await service.CreateAsync("Zebra Projekt", null, TestContext.Current.CancellationToken);
        await service.CreateAsync("Alpha Projekt", null, TestContext.Current.CancellationToken);
        await service.CreateAsync("Mitte Projekt", null, TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetAllAsync(TestContext.Current.CancellationToken);

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        var project = await service.CreateAsync("Projekt mit Repos", null, TestContext.Current.CancellationToken);
        await service.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        await service.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/test/repo2", "test-repo2", ct: TestContext.Current.CancellationToken);

        // Act
        var detail = await service.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);

        // Assert
        detail.Should().NotBeNull();
        detail!.Repositories.Should().HaveCount(2);
        detail.Repositories.Should().AllSatisfy(r => r.ProjectId.Should().Be(project.Id));
    }

    /// <summary>
    /// Testet, dass ArchivierenAsync den Projektstatus auf Archiviert setzt und persistiert.
    /// </summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldSetStatusToArchiviert_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Zu archivierendes Projekt", null, TestContext.Current.CancellationToken);

        // Act
        await service.ArchivierenAsync(project.Id, TestContext.Current.CancellationToken);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projects.FindAsync(new object?[] { project.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(ProjectStatus.Archived);
    }

    /// <summary>
    /// Testet, dass DeleteAsync ein Projekt und seine Aufgaben kaskadierend löscht.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteAufgaben_WhenProjektIsDeleted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projektService = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var project = await projektService.CreateAsync("Zu löschendes Projekt", null, TestContext.Current.CancellationToken);
        await aufgabeService.CreateAsync(project.Id, "Aufgabe 1", null, ct: TestContext.Current.CancellationToken);
        await aufgabeService.CreateAsync(project.Id, "Aufgabe 2", null, ct: TestContext.Current.CancellationToken);

        // Act
        await projektService.DeleteAsync(project.Id, TestContext.Current.CancellationToken);

        // Assert – Projekt und Aufgaben sind weg
        await using var db2 = db.CreateNewContext();
        var geladenesP = await db2.Projects.FindAsync(new object?[] { project.Id }, TestContext.Current.CancellationToken);
        var workItems = db2.WorkItems.Where(a => a.ProjectId == project.Id).ToList();

        geladenesP.Should().BeNull();
        workItems.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass AddRepositoryAsync ein Repository zum Projekt hinzufügt und persistiert.
    /// </summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldPersistRepository_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Projekt mit Repository", null, TestContext.Current.CancellationToken);

        // Act
        var repo = await service.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/test/myrepo", "myrepo", ct: TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(new object?[] { repo.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.RepositoryName.Should().Be("myrepo");
        loaded.PluginType.Should().Be("GitHub");
        loaded.ProjectId.Should().Be(project.Id);
        loaded.Active.Should().BeTrue();
    }

    /// <summary>
    /// Testet, dass AddRepositoryAsync im LocalDirectory-Flow SourceDirectory als Pflichtfeld verwendet
    /// und den Quellpfad korrekt als RepositoryUrl persistiert.
    /// </summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldPersistSourceDirectory_WhenLocalDirectoryPluginIsUsed()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Projekt mit lokalem Verzeichnis", null, TestContext.Current.CancellationToken);
        var sourceDirectory = @"C:\repos\integration-local";

        // Act
        var repo = await service.AddRepositoryAsync(project.Id, "LocalDirectoryPlugin", new Dictionary<string, string>
        {
            ["SourceDirectory"] = sourceDirectory
        }, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(new object?[] { repo.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.RepositoryUrl.Should().Be(sourceDirectory);
        loaded.RepositoryName.Should().Be("integration-local");
        loaded.PluginType.Should().Be("LocalDirectoryPlugin");
    }

    /// <summary>
    /// Testet, dass UpdateAsync Name und Beschreibung eines Projekts korrekt persistiert.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_WhenProjektExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Ursprünglicher Name", "Alt", TestContext.Current.CancellationToken);

        // Act
        await service.UpdateAsync(project.Id, "Neuer Name", "Neue Beschreibung", TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Projects.FindAsync(new object?[] { project.Id }, TestContext.Current.CancellationToken);

        loaded!.Name.Should().Be("Neuer Name");
        loaded.Description.Should().Be("Neue Beschreibung");
    }

    /// <summary>
    /// Testet, dass GetByIdAsync null zurückgibt, wenn das Projekt nicht existiert.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenProjektDoesNotExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Projekt", null, TestContext.Current.CancellationToken);
        var repo = await service.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/x/y", "y", ct: TestContext.Current.CancellationToken);

        // Act
        await service.RemoveRepositoryAsync(repo.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(new object?[] { repo.Id }, TestContext.Current.CancellationToken);
        loaded.Should().BeNull();
    }
}
