using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den ProjektService.</summary>
public sealed class ProjektServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ILogger<ProjektService>> _loggerMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly ProjektService _sut;

    public ProjektServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<ProjektService>>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private static Mock<IGitPlugin> CreatePluginMockWithRepositories(params AvailableRepository[] repositories)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns("TestPlugin");
        mock.Setup(p => p.PluginPrefix).Returns("TestPlugin");
        mock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);
        return mock;
    }

    /// <summary>GetUnassignedRepositoriesAsync gibt alle Repositories zurück, wenn keine zugeordnet sind.</summary>
    [Fact]
    public async Task GetUnassignedRepositoriesAsync_ShouldReturnAllRepositories_WhenAllUnassigned()
    {
        // Arrange
        var repo1 = new AvailableRepository("repo1", DateTime.UtcNow, "owner/repo1", "https://github.com/owner/repo1");
        var repo2 = new AvailableRepository("repo2", DateTime.UtcNow, "owner/repo2", "https://github.com/owner/repo2");
        var plugin = CreatePluginMockWithRepositories(repo1, repo2);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);

        // Act
        var result = (await sut.GetUnassignedRepositoriesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Url == "https://github.com/owner/repo1");
        result.Should().Contain(r => r.Url == "https://github.com/owner/repo2");
    }

    /// <summary>GetUnassignedRepositoriesAsync filtert zugeordnete Repositories heraus.</summary>
    [Fact]
    public async Task GetUnassignedRepositoriesAsync_ShouldExcludeAssignedRepositories()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Test-Projekt", null);
        await _sut.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/owner/repo1", "owner/repo1");

        var repoAssigned = new AvailableRepository("repo1", DateTime.UtcNow, "owner/repo1", "https://github.com/owner/repo1");
        var repoFree = new AvailableRepository("repo2", DateTime.UtcNow, "owner/repo2", "https://github.com/owner/repo2");
        var plugin = CreatePluginMockWithRepositories(repoAssigned, repoFree);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);

        // Act
        var result = (await sut.GetUnassignedRepositoriesAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Single().Url.Should().Be("https://github.com/owner/repo2");
    }

    /// <summary>GetUnassignedRepositoriesAsync sortiert primär nach UpdatedAt absteigend, sekundär nach Name aufsteigend.</summary>
    [Fact]
    public async Task GetUnassignedRepositoriesAsync_ShouldSortByUpdatedAtDescendingThenByNameAscending()
    {
        // Arrange
        var older = DateTime.UtcNow.AddDays(-2);
        var newer = DateTime.UtcNow.AddDays(-1);
        var repos = new[]
        {
            new AvailableRepository("b-repo", older, "owner/b-repo", "https://github.com/owner/b-repo"),
            new AvailableRepository("a-repo", newer, "owner/a-repo", "https://github.com/owner/a-repo"),
            new AvailableRepository("c-repo", older, "owner/c-repo", "https://github.com/owner/c-repo"),
        };
        var plugin = CreatePluginMockWithRepositories(repos);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);

        // Act
        var result = (await sut.GetUnassignedRepositoriesAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("a-repo");
        result[1].Name.Should().Be("b-repo");
        result[2].Name.Should().Be("c-repo");
    }

    /// <summary>GetUnassignedRepositoriesAsync ignoriert fehlerhafte Plugins und gibt Ergebnisse der anderen zurück.</summary>
    [Fact]
    public async Task GetUnassignedRepositoriesAsync_ShouldHandlePluginError_AndContinueWithOtherPlugins()
    {
        // Arrange
        var faultyPlugin = new Mock<IGitPlugin>();
        faultyPlugin.Setup(p => p.PluginName).Returns("FaultyPlugin");
        faultyPlugin.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Verbindungsfehler"));

        var workingRepo = new AvailableRepository("working-repo", DateTime.UtcNow, "owner/working", "https://github.com/owner/working");
        var workingPlugin = CreatePluginMockWithRepositories(workingRepo);

        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins())
            .Returns([faultyPlugin.Object, workingPlugin.Object]);
        var sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);

        // Act
        var result = (await sut.GetUnassignedRepositoriesAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Single().Url.Should().Be("https://github.com/owner/working");
    }

    /// <summary>GetUnassignedRepositoriesAsync gibt eine leere Liste zurück, wenn alle Repositories zugeordnet sind.</summary>
    [Fact]
    public async Task GetUnassignedRepositoriesAsync_ShouldReturnEmptyList_WhenAllRepositoriesAssigned()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Test-Projekt", null);
        await _sut.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/owner/repo1", "owner/repo1");

        var repo = new AvailableRepository("repo1", DateTime.UtcNow, "owner/repo1", "https://github.com/owner/repo1");
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = new ProjektService(_db, _loggerMock.Object, _pluginManagerMock.Object);

        // Act
        var result = (await sut.GetUnassignedRepositoriesAsync()).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>CreateAsync erstellt ein neues Projekt mit Aktiv-Status und gibt es zurück.</summary>
    [Fact]
    public async Task CreateAsync_ShouldCreateProjektWithAktivStatus_WhenCalledWithValidName()
    {
        // Arrange
        const string name = "Testprojekt";
        const string beschreibung = "Eine Beschreibung";

        // Act
        var result = await _sut.CreateAsync(name, beschreibung);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.Beschreibung.Should().Be(beschreibung);
        result.Status.Should().Be(ProjektStatus.Aktiv);
        result.ErstellungsDatum.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>CreateAsync persistiert das Projekt in der Datenbank.</summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistProjekt_WhenCalledWithValidName()
    {
        // Arrange & Act
        var result = await _sut.CreateAsync("Persistiertes Projekt", null);

        // Assert
        var persisted = await _sut.GetByIdAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Persistiertes Projekt");
    }

    /// <summary>GetAllAsync gibt alle Projekte alphabetisch sortiert zurück.</summary>
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllProjekteOrderedByName_WhenMultipleProjekteExist()
    {
        // Arrange
        await _sut.CreateAsync("Zebra Projekt", null);
        await _sut.CreateAsync("Alpha Projekt", null);
        await _sut.CreateAsync("Mitte Projekt", null);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha Projekt");
        result[1].Name.Should().Be("Mitte Projekt");
        result[2].Name.Should().Be("Zebra Projekt");
    }

    /// <summary>GetByIdAsync gibt null zurück wenn kein Projekt mit der ID existiert.</summary>
    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>UpdateAsync aktualisiert Name und Beschreibung des Projekts.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateNameAndBeschreibung_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Alter Name", "Alte Beschreibung");

        // Act
        var result = await _sut.UpdateAsync(projekt.Id, "Neuer Name", "Neue Beschreibung");

        // Assert
        result.Name.Should().Be("Neuer Name");
        result.Beschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>UpdateAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.UpdateAsync(nonExistentId, "Name", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentId}*");
    }

    /// <summary>ArchivierenAsync setzt den Status auf Archiviert.</summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldSetStatusToArchiviert_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Zu archivierendes Projekt", null);

        // Act
        await _sut.ArchivierenAsync(projekt.Id);

        // Assert
        var result = await _sut.GetByIdAsync(projekt.Id);
        result!.Status.Should().Be(ProjektStatus.Archiviert);
    }

    /// <summary>ArchivierenAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task ArchivierenAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.ArchivierenAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>DeleteAsync entfernt das Projekt aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveProjekt_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Zu löschendes Projekt", null);

        // Act
        await _sut.DeleteAsync(projekt.Id);

        // Assert
        var result = await _sut.GetByIdAsync(projekt.Id);
        result.Should().BeNull();
    }

    /// <summary>DeleteAsync wirft InvalidOperationException wenn Projekt nicht gefunden.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldThrowInvalidOperationException_WhenProjektDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.DeleteAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>AddRepositoryAsync fügt ein Repository zum Projekt hinzu.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldAddRepository_WhenProjektExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt mit Repository", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/repo",
            "test/repo");

        // Assert
        repo.Should().NotBeNull();
        repo.ProjektId.Should().Be(projekt.Id);
        repo.RepositoryUrl.Should().Be("https://github.com/test/repo");
        repo.RepositoryName.Should().Be("test/repo");
        repo.Aktiv.Should().BeTrue();
    }

    /// <summary>AddRepositoryAsync übernimmt SourceDirectory als RepositoryUrl für LocalDirectoryPlugin.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldUseSourceDirectoryForLocalDirectoryPlugin_WhenFieldValuesAreValid()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Lokales Projekt", null);
        var sourceDirectory = @"C:\repos\local-source";

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            new Dictionary<string, string>
            {
                ["SourceDirectory"] = sourceDirectory
            });

        // Assert
        repo.RepositoryUrl.Should().Be(sourceDirectory);
        repo.RepositoryName.Should().Be("local-source");
        repo.PluginTyp.Should().Be("LocalDirectoryPlugin");
    }

    /// <summary>AddRepositoryAsync validiert SourceDirectory für LocalDirectoryPlugin als Pflichtfeld.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldThrow_WhenSourceDirectoryIsMissingForLocalDirectoryPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Lokales Projekt ohne SourceDirectory", null);

        // Act
        var act = () => _sut.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            new Dictionary<string, string>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SourceDirectory*Pflichtfeld*");
    }

    /// <summary>AddRepositoryAsync mappt im String-Overload repositoryUrl auf SourceDirectory für LocalDirectoryPlugin.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldMapRepositoryUrlToSourceDirectory_WhenUsingStringOverloadForLocalDirectoryPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Lokales Projekt per String-Overload", null);
        var sourceDirectory = @"C:\repos\local-source";

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            sourceDirectory,
            string.Empty);

        // Assert
        repo.RepositoryUrl.Should().Be(sourceDirectory);
        repo.RepositoryName.Should().Be("local-source");
        repo.PluginTyp.Should().Be("LocalDirectoryPlugin");
    }

    /// <summary>AddRepositoryAsync trimmt SourceDirectory und leitet den Namen trotz Trailing-Separator korrekt ab.</summary>
    [Theory]
    [InlineData(@"  C:\repos\team-a  ", "team-a")]
    [InlineData(@"C:\repos\team-b\", "team-b")]
    public async Task AddRepositoryAsync_ShouldHandleTrimAndTrailingSeparator_ForLocalDirectorySourceDirectory(
        string sourceDirectoryInput,
        string expectedRepositoryName)
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Lokales Projekt mit Randfällen", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            new Dictionary<string, string>
            {
                ["SourceDirectory"] = sourceDirectoryInput
            });

        // Assert
        repo.RepositoryUrl.Should().Be(sourceDirectoryInput.Trim());
        repo.RepositoryName.Should().Be(expectedRepositoryName);
    }

    /// <summary>AddRepositoryAsync validiert RepositoryUrl für GitHub-Plugins als Pflichtfeld.</summary>
    [Theory]
    [InlineData("GitHub")]
    [InlineData("Softwareschmiede.GitHub")]
    public async Task AddRepositoryAsync_ShouldThrow_WhenRepositoryUrlIsMissingForGitHubPlugin(string pluginPrefix)
    {
        // Arrange
        var projekt = await _sut.CreateAsync("GitHub-Projekt ohne URL", null);

        // Act
        var act = () => _sut.AddRepositoryAsync(
            projekt.Id,
            pluginPrefix,
            new Dictionary<string, string>
            {
                ["RepositoryName"] = "owner/repo"
            });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RepositoryUrl*Pflichtfeld*");
    }

    /// <summary>AddRepositoryAsync validiert RepositoryName für GitHub-Plugins als Pflichtfeld.</summary>
    [Theory]
    [InlineData("GitHub")]
    [InlineData("Softwareschmiede.GitHub")]
    public async Task AddRepositoryAsync_ShouldThrow_WhenRepositoryNameIsMissingForGitHubPlugin(string pluginPrefix)
    {
        // Arrange
        var projekt = await _sut.CreateAsync("GitHub-Projekt ohne Name", null);

        // Act
        var act = () => _sut.AddRepositoryAsync(
            projekt.Id,
            pluginPrefix,
            new Dictionary<string, string>
            {
                ["RepositoryUrl"] = "https://github.com/owner/repo"
            });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RepositoryName*Pflichtfeld*");
    }

    /// <summary>AddRepositoryAsync leitet für Non-GitHub-Plugins den RepositoryName aus der URL ab, wenn kein Name gesetzt ist.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldDeriveRepositoryName_FromRepositoryUrl_WhenNameMissing_ForNonGitHubPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Custom-SCM ohne Namen", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.CustomScm",
            new Dictionary<string, string>
            {
                ["RepositoryUrl"] = "https://github.com/owner/repo"
            });

        // Assert
        repo.RepositoryName.Should().Be("repo");
    }

    /// <summary>AddRepositoryAsync leitet für Non-GitHub-Plugins den RepositoryName aus einem lokalen Pfad ab.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldDeriveRepositoryName_FromLocalPath_WhenNameMissing_ForNonGitHubPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Custom-SCM mit lokalem Pfad", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.CustomScm",
            new Dictionary<string, string>
            {
                ["RepositoryUrl"] = @"C:\repos\my-repo\"
            });

        // Assert
        repo.RepositoryName.Should().Be("my-repo");
    }

    /// <summary>AddRepositoryAsync schlägt fehl, wenn aus einer URL kein valider RepositoryName abgeleitet werden kann.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldThrow_WhenDerivedRepositoryNameIsEmpty_ForNonGitHubPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Custom-SCM mit nicht ableitbarem Namen", null);

        // Act
        var act = () => _sut.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.CustomScm",
            new Dictionary<string, string>
            {
                ["RepositoryUrl"] = "https://github.com/"
            });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*konnte kein RepositoryName ermittelt werden*");
    }

    /// <summary>AddRepositoryAsync bevorzugt einen expliziten RepositoryName gegenüber einer Ableitung.</summary>
    [Fact]
    public async Task AddRepositoryAsync_ShouldPreferExplicitRepositoryName_OverDerivedName_ForNonGitHubPlugin()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Custom-SCM mit Override-Namen", null);

        // Act
        var repo = await _sut.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.CustomScm",
            new Dictionary<string, string>
            {
                ["RepositoryUrl"] = "https://github.com/owner/repo",
                ["RepositoryName"] = "override-name"
            });

        // Assert
        repo.RepositoryName.Should().Be("override-name");
    }

    /// <summary>RemoveRepositoryAsync entfernt ein Repository aus dem Projekt.</summary>
    [Fact]
    public async Task RemoveRepositoryAsync_ShouldRemoveRepository_WhenRepositoryExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt für Repository-Entfernung", null);
        var repo = await _sut.AddRepositoryAsync(projekt.Id, "GitHub", "https://github.com/test/repo2", "test/repo2");

        // Act
        await _sut.RemoveRepositoryAsync(repo.Id);

        // Assert
        var detail = await _sut.GetDetailAsync(projekt.Id);
        detail!.Repositories.Should().BeEmpty();
    }

    /// <summary>SaveRepositoryStartKonfigurationAsync erstellt und persistiert eine neue Startkonfiguration.</summary>
    [Fact]
    public async Task SaveRepositoryStartKonfigurationAsync_ShouldCreateConfiguration_WhenRepositoryExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt mit Startkonfiguration", null);
        var repository = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/start",
            "test/start");

        // Act
        var saved = await _sut.SaveRepositoryStartKonfigurationAsync(
            repository.Id,
            " scripts/start.ps1 ",
            true);

        // Assert
        saved.GitRepositoryId.Should().Be(repository.Id);
        saved.StartScriptRelativePath.Should().Be("scripts/start.ps1");
        saved.Aktiv.Should().BeTrue();

        var persisted = await _sut.GetRepositoryStartKonfigurationAsync(repository.Id);
        persisted.Should().NotBeNull();
        persisted!.StartScriptRelativePath.Should().Be("scripts/start.ps1");
    }

    /// <summary>SaveRepositoryStartKonfigurationAsync aktualisiert eine bestehende Startkonfiguration statt eine neue anzulegen.</summary>
    [Fact]
    public async Task SaveRepositoryStartKonfigurationAsync_ShouldUpdateExistingConfiguration_WhenAlreadyPresent()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt mit bestehender Konfiguration", null);
        var repository = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/start-existing",
            "test/start-existing");

        var first = await _sut.SaveRepositoryStartKonfigurationAsync(
            repository.Id,
            "scripts/start.ps1",
            true);

        // Act
        var updated = await _sut.SaveRepositoryStartKonfigurationAsync(
            repository.Id,
            "scripts/updated.ps1",
            false);

        // Assert
        updated.Id.Should().Be(first.Id);
        updated.StartScriptRelativePath.Should().Be("scripts/updated.ps1");
        updated.Aktiv.Should().BeFalse();
    }

    /// <summary>GetRepositoryStartKonfigurationAsync gibt null zurück, wenn keine Konfiguration existiert.</summary>
    [Fact]
    public async Task GetRepositoryStartKonfigurationAsync_ShouldReturnNull_WhenNoConfigurationExists()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt ohne Konfiguration", null);
        var repository = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/no-config",
            "test/no-config");

        // Act
        var result = await _sut.GetRepositoryStartKonfigurationAsync(repository.Id);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>SaveRepositoryStartKonfigurationAsync validiert absoluten Skriptpfad.</summary>
    [Fact]
    public async Task SaveRepositoryStartKonfigurationAsync_ShouldThrow_WhenScriptPathIsAbsolute()
    {
        // Arrange
        var projekt = await _sut.CreateAsync("Projekt mit invalider Konfiguration", null);
        var repository = await _sut.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/invalid",
            "test/invalid");
        var absolutePath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "start.ps1");

        // Act
        var act = () => _sut.SaveRepositoryStartKonfigurationAsync(
            repository.Id,
            absolutePath,
            true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*relativ*");
    }

}
