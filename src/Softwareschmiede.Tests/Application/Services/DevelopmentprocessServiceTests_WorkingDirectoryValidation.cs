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
/// in <see cref="DevelopmentProcessService.StartProcessAsync"/>: die Validierung des konfigurierten
/// Arbeitsverzeichnisses soll direkt nach einem erfolgreichen Git-Klon erfolgen (frühes, klares Fehlerbild),
/// statt erst beim späteren CLI-Start.
/// </summary>
public sealed class DevelopmentprocessServiceTests_WorkingDirectoryValidation : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly ProjectService _projectService;
    private readonly LogService _logService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IWorkingDirectoryResolver> _workingDirectoryResolverMock;
    private readonly GitOrchestrationService _gitOrchestrationService;
    private readonly DevelopmentProcessService _sut;
    private readonly Guid _projectId = new("66666666-6666-6666-6666-666666666666");

    /// <summary>EntwicklungsprozessServiceTests_WorkingDirectoryValidation.</summary>
    public DevelopmentprocessServiceTests_WorkingDirectoryValidation()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, new Mock<ILogger<WorkItemService>>().Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));
        _projectService = new ProjectService(_db, new Mock<ILogger<ProjectService>>().Object);
        _logService = new LogService(_db, new Mock<ILogger<LogService>>().Object);

        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.SetupGet(p => p.PluginName).Returns("Mock Git");
        _gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Mock.Git");
        _gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        _gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IAiPlugin>().Object);
        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginActivationService = new PluginActivationService(new AppSettingService(_db, new Mock<ILogger<AppSettingService>>().Object), pluginManagerMock.Object, new Mock<ILogger<PluginActivationService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            pluginActivationService,
            new Mock<ILogger<PluginSelectionService>>().Object);

        _gitOrchestrationService = new GitOrchestrationService(
            _workItemService,
            _projectService,
            _logService,
            _gitPluginMock.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);

        _workingDirectoryResolverMock = new Mock<IWorkingDirectoryResolver>();

        _sut = new DevelopmentProcessService(
            _workItemService,
            _logService,
            _gitPluginMock.Object,
            pluginSelectionService,
            _workingDirectoryResolverMock.Object,
            new DevelopmentProcessServiceOptions(
                ProjectService: _projectService,
                GitOrchestrationService: _gitOrchestrationService),
            new Mock<ILogger<DevelopmentProcessService>>().Object);

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Arbeitsverzeichnis-Validierung-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
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
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.WorkingDirectoryResolutionResult(uniqueBase, false, "configured", null));
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
            ProjectId = _projectId,
            PluginType = "Mock.Git",
            RepositoryUrl = $"https://example.test/{Guid.NewGuid():N}",
            RepositoryName = "repo-workdir-validation",
            Active = true
        };
        repository.StartConfiguration = new RepositoryStartConfiguration
        {
            Id = Guid.NewGuid(),
            WorkingDirectoryRelativePath = workingDirectoryRelativePath,
            Active = true,
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
    public async Task StartProcessAsync_ShouldThrowDirectoryNotFoundImmediatelyAfterClone_WhenConfiguredWorkingDirectoryMissing()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("does-not-exist");
        var workItem = await _workItemService.CreateAsync(_projectId, "Arbeitsverzeichnis fehlt", null, repository.Id, TestContext.Current.CancellationToken);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            var act = () => _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl);

            // Assert
            await act.Should().ThrowAsync<DirectoryNotFoundException>();
            _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never,
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
    public async Task StartProcessAsync_ShouldContinue_WhenConfiguredWorkingDirectoryExists()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("backend");
        var workItem = await _workItemService.CreateAsync(_projectId, "Arbeitsverzeichnis vorhanden", null, repository.Id, TestContext.Current.CancellationToken);
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
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
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
    public async Task StartProcessAsync_ShouldNotValidate_WhenGitOrchestrationServiceNotConfigured()
    {
        // Arrange
        var repository = await CreateRepositoryWithWorkingDirectoryAsync("does-not-exist");
        var workItem = await _workItemService.CreateAsync(_projectId, "Ohne GitOrchestrationService", null, repository.Id, TestContext.Current.CancellationToken);
        var clonePath = SetupCloneWithDirectoryCreation();

        var sutWithoutValidation = new DevelopmentProcessService(
            _workItemService,
            _logService,
            _gitPluginMock.Object,
            new PluginSelectionService(
                CreatePassthroughPluginManagerMock(),
                new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object),
                new PluginActivationService(new AppSettingService(_db, new Mock<ILogger<AppSettingService>>().Object), CreatePassthroughPluginManagerMock(), new Mock<ILogger<PluginActivationService>>().Object),
                new Mock<ILogger<PluginSelectionService>>().Object),
            _workingDirectoryResolverMock.Object,
            new DevelopmentProcessServiceOptions(ProjectService: _projectService),
            new Mock<ILogger<DevelopmentProcessService>>().Object);

        try
        {
            // Act
            await sutWithoutValidation.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
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
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IAiPlugin>().Object);
        return pluginManagerMock.Object;
    }
}
