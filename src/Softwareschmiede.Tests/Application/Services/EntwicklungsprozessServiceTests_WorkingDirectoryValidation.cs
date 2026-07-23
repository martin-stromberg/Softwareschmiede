using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests für die Verdrahtung von <see cref="GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync"/>
/// in <see cref="EntwicklungsprozessService.ProzessStartenAsync"/>: die Validierung des konfigurierten
/// Arbeitsverzeichnisses soll direkt nach einem erfolgreichen Git-Klon erfolgen (frühes, klares Fehlerbild),
/// statt erst beim späteren CLI-Start.
/// </summary>
public sealed class EntwicklungsprozessServiceTests_WorkingDirectoryValidation : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;
    private readonly ProtokollService _protokollService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IArbeitsverzeichnisResolver> _arbeitsverzeichnisResolverMock;
    private readonly GitOrchestrationService _gitOrchestrationService;
    private readonly EntwicklungsprozessService _sut;
    private readonly Guid _projektId = new("66666666-6666-6666-6666-666666666666");

    /// <summary>EntwicklungsprozessServiceTests_WorkingDirectoryValidation.</summary>
    public EntwicklungsprozessServiceTests_WorkingDirectoryValidation()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _projektService = new ProjektService(_db, new Mock<ILogger<ProjektService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);

        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.SetupGet(p => p.PluginName).Returns("Mock Git");
        _gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Mock.Git");
        _gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        _gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);
        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginActivationService = new PluginActivationService(new AppEinstellungService(_db, new Mock<ILogger<AppEinstellungService>>().Object), pluginManagerMock.Object, new Mock<ILogger<PluginActivationService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            pluginActivationService,
            new Mock<ILogger<PluginSelectionService>>().Object);

        _gitOrchestrationService = new GitOrchestrationService(
            _aufgabeService,
            _projektService,
            _protokollService,
            _gitPluginMock.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);

        _arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();

        _sut = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            pluginSelectionService,
            _arbeitsverzeichnisResolverMock.Object,
            new EntwicklungsprozessServiceOptions(
                ProjektService: _projektService,
                GitOrchestrationService: _gitOrchestrationService),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Arbeitsverzeichnis-Validierung-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
    }

    private string SetupCloneWithDirectoryCreation()
    {
        var uniqueBase = Path.Combine(Path.GetTempPath(), $"sw-test-{Guid.NewGuid():N}");
        _arbeitsverzeichnisResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.ArbeitsverzeichnisResolutionResult(uniqueBase, false, "configured", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                return Task.CompletedTask;
            });
        return uniqueBase;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }

    private async Task<GitRepository> CreateRepositoryWithWorkingDirectoryAsync(string? workingDirectoryRelativePath)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Mock.Git",
            RepositoryUrl = $"https://example.test/{Guid.NewGuid():N}",
            RepositoryName = "repo-workdir-validation",
            Aktiv = true
        };
        repository.StartKonfiguration = new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            WorkingDirectoryRelativePath = workingDirectoryRelativePath,
            Aktiv = true,
            GitRepository = repository
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    /// <summary>
    /// ProzessStartenAsync bricht sofort nach dem Klon mit einer DirectoryNotFoundException ab, wenn das
    /// konfigurierte Arbeitsverzeichnis im geklonten Repository nicht existiert — ohne dass zuvor ein
    /// Branch angelegt wird (frühes Fehlerbild direkt nach dem Klon, wie im Plan beschrieben).
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldThrowDirectoryNotFoundImmediatelyAfterClone_WhenConfiguredWorkingDirectoryMissing()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("does-not-exist");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Arbeitsverzeichnis fehlt", null, repository.Id);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            var act = () => _sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            await act.Should().ThrowAsync<DirectoryNotFoundException>();
            _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
                "die Validierung soll vor der Branch-Erstellung fehlschlagen, nicht erst beim späteren CLI-Start");
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// ProzessStartenAsync setzt den Prozess normal fort, wenn das konfigurierte Arbeitsverzeichnis nach
    /// dem Klon existiert.
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldContinue_WhenConfiguredWorkingDirectoryExists()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("backend");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Arbeitsverzeichnis vorhanden", null, repository.Id);
        var clonePath = SetupCloneWithDirectoryCreation();
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(Path.Combine(path, "backend"));
                return Task.CompletedTask;
            });

        try
        {
            // Act
            await _sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
            updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// ProzessStartenAsync validiert das Arbeitsverzeichnis nicht, wenn kein
    /// <see cref="GitOrchestrationService"/> konfiguriert ist (Rückwärtskompatibilität für Aufrufer, die
    /// den optionalen Dienst nicht bereitstellen).
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldNotValidate_WhenGitOrchestrationServiceNotConfigured()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("does-not-exist");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Ohne GitOrchestrationService", null, repository.Id);
        var clonePath = SetupCloneWithDirectoryCreation();

        var sutOhneValidation = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            new PluginSelectionService(
                CreatePassthroughPluginManagerMock(),
                new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object),
                new PluginActivationService(new AppEinstellungService(_db, new Mock<ILogger<AppEinstellungService>>().Object), CreatePassthroughPluginManagerMock(), new Mock<ILogger<PluginActivationService>>().Object),
                new Mock<ILogger<PluginSelectionService>>().Object),
            _arbeitsverzeichnisResolverMock.Object,
            new EntwicklungsprozessServiceOptions(ProjektService: _projektService),
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

        try
        {
            // Act
            await sutOhneValidation.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
            updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    private IPluginManager CreatePassthroughPluginManagerMock()
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);
        return pluginManagerMock.Object;
    }
}
