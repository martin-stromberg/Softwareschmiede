using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests für die Ausführung des Repository-Initialisierungsskripts im Aufgaben-Lifecycle von
/// <see cref="EntwicklungsprozessService"/>: Ausführung nach dem Klonen, Fehlertoleranz und Reihenfolge
/// gegenüber dem Repository-Startskript.
/// </summary>
public sealed class EntwicklungsprozessServiceTests_Initialisierungsskript : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IArbeitsverzeichnisResolver> _arbeitsverzeichnisResolverMock;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly Guid _projektId = new("88888888-8888-8888-8888-888888888888");

    /// <summary>EntwicklungsprozessServiceTests_Initialisierungsskript.</summary>
    public EntwicklungsprozessServiceTests_Initialisierungsskript()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);

        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.SetupGet(p => p.PluginName).Returns("Mock Git");
        _gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Mock.Git");
        _gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        _gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);
        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginActivationService = new PluginActivationService(new AppEinstellungService(_db, new Mock<ILogger<AppEinstellungService>>().Object), pluginManagerMock.Object, new Mock<ILogger<PluginActivationService>>().Object);
        _pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            pluginActivationService,
            new Mock<ILogger<PluginSelectionService>>().Object);

        _arbeitsverzeichnisResolverMock = new Mock<IArbeitsverzeichnisResolver>();
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
            .ReturnsAsync(new ArbeitsverzeichnisResolutionResult(uniqueBase, false, "configured", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                return Task.CompletedTask;
            });
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return uniqueBase;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }

    private async Task<GitRepository> CreateRepositoryAsync(
        RepositoryInitialisierungKonfiguration? initialisierungKonfiguration = null,
        RepositoryStartKonfiguration? startKonfiguration = null)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Mock.Git",
            RepositoryUrl = $"https://example.test/{Guid.NewGuid():N}",
            RepositoryName = "repo-init-script",
            Aktiv = true,
            InitialisierungKonfiguration = initialisierungKonfiguration,
            StartKonfiguration = startKonfiguration
        };
        if (initialisierungKonfiguration != null)
        {
            initialisierungKonfiguration.GitRepository = repository;
        }
        if (startKonfiguration != null)
        {
            startKonfiguration.GitRepository = repository;
        }

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Initialisierungsskript-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    private EntwicklungsprozessService CreateSut(EntwicklungsprozessServiceOptions options)
        => new(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
            _pluginSelectionService,
            _arbeitsverzeichnisResolverMock.Object,
            options,
            new Mock<ILogger<EntwicklungsprozessService>>().Object);

    /// <summary>Nach dem Klonen wird das konfigurierte Initialisierungsskript vor dem KI-Start ausgeführt.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldExecuteInitializationScript_AfterClone()
    {
        // Arrange
        var initialisierungKonfiguration = new RepositoryInitialisierungKonfiguration
        {
            Id = Guid.NewGuid(),
            InitialisierungsskriptRelativePath = "scripts/init.ps1",
            Aktiv = true
        };
        var repository = await CreateRepositoryAsync(initialisierungKonfiguration);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Initialisierungsskript nach Klon", null, repository.Id);

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, "ok", string.Empty));
        var repositoryInitialisierungService = new RepositoryInitialisierungService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryInitialisierungService>>().Object);

        var clonePath = SetupCloneWithDirectoryCreation();
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                var scriptDirectory = Path.Combine(path, "scripts");
                Directory.CreateDirectory(scriptDirectory);
                File.WriteAllText(Path.Combine(scriptDirectory, "init.ps1"), "# init");
                return Task.CompletedTask;
            });
        var sut = CreateSut(new EntwicklungsprozessServiceOptions(RepositoryInitialisierungService: repositoryInitialisierungService));

        try
        {
            // Act
            await sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            cliRunnerMock.Verify(
                runner => runner.RunAsync(
                    "powershell.exe",
                    It.Is<IEnumerable<string>>(args => args.Contains("-File") && args.Any(a => a.Contains("init.ps1"))),
                    It.IsAny<string?>(),
                    It.IsAny<IDictionary<string, string>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
            updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>Ein fehlschlagendes Initialisierungsskript blockiert die Aufgabe nicht; der Fehler wird nur geloggt/protokolliert.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldNotBlockTask_WhenInitializationScriptFails()
    {
        // Arrange
        var initialisierungKonfiguration = new RepositoryInitialisierungKonfiguration
        {
            Id = Guid.NewGuid(),
            InitialisierungsskriptRelativePath = "scripts/init.ps1",
            Aktiv = true
        };
        var repository = await CreateRepositoryAsync(initialisierungKonfiguration);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Initialisierungsskript schlägt fehl", null, repository.Id);

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "init failed"));
        var repositoryInitialisierungService = new RepositoryInitialisierungService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryInitialisierungService>>().Object);

        var clonePath = SetupCloneWithDirectoryCreation();
        var sut = CreateSut(new EntwicklungsprozessServiceOptions(RepositoryInitialisierungService: repositoryInitialisierungService));

        try
        {
            // Act
            await sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            var updatedAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
            updatedAufgabe!.Status.Should().Be(AufgabeStatus.Gestartet);
            var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
            protokoll.Should().Contain(entry =>
                entry.Typ == ProtokollTyp.GitAktion
                && entry.Inhalt.Contains("Hinweis: Das Repository-Initialisierungsskript konnte nicht ausgeführt werden", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>Sind sowohl Initialisierungs- als auch Startskript konfiguriert, wird das Initialisierungsskript zuerst ausgeführt.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldExecuteInitializationThenStartScript_InOrder()
    {
        // Arrange
        var initialisierungKonfiguration = new RepositoryInitialisierungKonfiguration
        {
            Id = Guid.NewGuid(),
            InitialisierungsskriptRelativePath = "scripts/init.ps1",
            Aktiv = true
        };
        var startKonfiguration = new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            StartScriptRelativePath = "scripts/start.ps1",
            Aktiv = true
        };
        var repository = await CreateRepositoryAsync(initialisierungKonfiguration, startKonfiguration);
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Reihenfolge Init vor Start", null, repository.Id);

        var ausfuehrungsReihenfolge = new List<string>();
        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                var argList = args.ToList();
                ausfuehrungsReihenfolge.Add(argList.Any(a => a.Contains("init.ps1")) ? "init" : "start");
                return Task.FromResult(new CliResult(0, "ok", string.Empty));
            });

        var repositoryInitialisierungService = new RepositoryInitialisierungService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryInitialisierungService>>().Object);
        var repositoryStartskriptService = new RepositoryStartskriptService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryStartskriptService>>().Object);

        var clonePath = SetupCloneWithDirectoryCreation();
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                var scriptDirectory = Path.Combine(path, "scripts");
                Directory.CreateDirectory(scriptDirectory);
                File.WriteAllText(Path.Combine(scriptDirectory, "init.ps1"), "# init");
                File.WriteAllText(Path.Combine(scriptDirectory, "start.ps1"), "# start");
                return Task.CompletedTask;
            });
        var sut = CreateSut(new EntwicklungsprozessServiceOptions(
            RepositoryInitialisierungService: repositoryInitialisierungService,
            RepositoryStartskriptService: repositoryStartskriptService));

        try
        {
            // Act
            await sut.ProzessStartenAsync(aufgabe.Id, repository.RepositoryUrl);

            // Assert
            ausfuehrungsReihenfolge.Should().Equal("init", "start");
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }
}
