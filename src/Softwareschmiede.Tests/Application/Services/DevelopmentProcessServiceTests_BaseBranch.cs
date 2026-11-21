using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests für die Basis-Branch-Konfiguration (<see cref="GitRepository.DefaultSourceBranchName"/>) beim
/// Aufgabenstart: Validierung gegen die Remote-Branches vor dem Klon und Feature-Branch-Erstellung vom
/// konfigurierten Basis-Branch statt vom HEAD.
/// </summary>
public sealed class DevelopmentProcessServiceTests_BaseBranch : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IWorkingDirectoryResolver> _workingDirectoryResolverMock;
    private readonly DevelopmentProcessService _sut;
    private readonly Guid _projectId = new("77777777-7777-7777-7777-777777777777");

    /// <summary>EntwicklungsprozessServiceTests_BasisBranch.</summary>
    public DevelopmentProcessServiceTests_BaseBranch()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, new Mock<ILogger<WorkItemService>>().Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));
        _logService = new LogService(_db, new Mock<ILogger<LogService>>().Object);

        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.SetupGet(p => p.PluginName).Returns("Mock Git");
        _gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Mock.Git");
        _gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        _gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);

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

        _workingDirectoryResolverMock = new Mock<IWorkingDirectoryResolver>();

        _sut = new DevelopmentProcessService(
            _workItemService,
            _logService,
            _gitPluginMock.Object,
            pluginSelectionService,
            _workingDirectoryResolverMock.Object,
            new DevelopmentProcessServiceOptions(),
            new Mock<ILogger<DevelopmentProcessService>>().Object);

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Basis-Branch-Testprojekt",
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

    private async Task<GitRepository> CreateRepositoryAsync(string? defaultSourceBranchName)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Mock.Git",
            RepositoryUrl = $"https://example.test/{Guid.NewGuid():N}",
            RepositoryName = "repo-basis-branch",
            Active = true,
            DefaultSourceBranchName = defaultSourceBranchName
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    /// <summary>
    /// StartProcessAsync bricht mit einer <see cref="GitBranchNotFoundException"/> ab, wenn der konfigurierte
    /// Basis-Branch nicht in den Remote-Branches des Repositories enthalten ist. Der Klon darf dabei nicht
    /// gestartet werden (Validierung vor dem Klon, wie im Plan beschrieben).
    /// </summary>
    [Fact]
    public async Task StartProcessAsync_ShouldThrow_WhenBaseBranchDoesNotExist()
    {
        // Arrange
        var repository = await CreateRepositoryAsync("staging");
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe mit fehlendem Basis-Branch", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.GetRemoteBranchesAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);

        // Act
        var act = () => _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl);

        // Assert
        await act.Should().ThrowAsync<GitBranchNotFoundException>();
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "die Validierung soll vor dem Klon fehlschlagen");
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.New);
    }

    /// <summary>
    /// StartProcessAsync setzt den Prozess normal fort, wenn der konfigurierte Basis-Branch in den
    /// Remote-Branches des Repositories vorhanden ist.
    /// </summary>
    [Fact]
    public async Task StartProcessAsync_ShouldSucceed_WhenBaseBranchExists()
    {
        // Arrange
        var repository = await CreateRepositoryAsync("staging");
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe mit vorhandenem Basis-Branch", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.GetRemoteBranchesAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "staging"]);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), "staging", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
            _gitPluginMock.Verify(g => g.GetRemoteBranchesAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// StartProcessAsync setzt den Prozess normal fort und validiert nicht gegen die Remote-Branches, wenn
    /// kein Basis-Branch konfiguriert ist (Abwärtskompatibilität, Fallback auf Standard-Branch).
    /// </summary>
    [Fact]
    public async Task StartProcessAsync_ShouldSucceed_WhenNoBranchConfigured()
    {
        // Arrange
        var repository = await CreateRepositoryAsync(null);
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Basis-Branch", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
            _gitPluginMock.Verify(g => g.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
                "ohne konfigurierten Basis-Branch soll keine Remote-Validierung stattfinden");
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// Der neue Feature-Branch wird vom konfigurierten Basis-Branch abgezweigt: Der Remote-Branch wird zunächst
    /// lokal nachgezogen (<see cref="IGitPlugin.CheckoutRemoteBranchAsync"/>), anschließend wird der eigentliche
    /// Task-Branch mit <c>sourceBranchName</c> gleich dem Basis-Branch angelegt.
    /// </summary>
    [Fact]
    public async Task SetupBranchAsync_ShouldCreateBranchFromBaseBranch_WhenConfigured()
    {
        // Arrange
        var repository = await CreateRepositoryAsync("staging");
        var workItem = await _workItemService.CreateAsync(_projectId, "Feature-Branch vom Basis-Branch", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.GetRemoteBranchesAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "staging"]);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(repository.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), "staging", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), "staging", It.IsAny<CancellationToken>()), Times.Once);
            _gitPluginMock.Verify(g => g.CreateBranchAsync(
                It.IsAny<string>(),
                It.Is<string>(branch => branch.StartsWith("task/", StringComparison.Ordinal)),
                "staging",
                It.IsAny<CancellationToken>()), Times.Once);
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.BranchName.Should().StartWith("task/");
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// Aufgaben aus einem bestehenden Pull Request checken dessen Quellbranch aus, statt einen neuen
    /// task/-Branch anzulegen.
    /// </summary>
    [Fact]
    public async Task SetupBranchAsync_ShouldCheckoutPullRequestSource_WhenTaskHasReviewSource()
    {
        // Arrange
        var repository = await CreateRepositoryAsync(null);
        var workItem = await _workItemService.CreateAsync(_projectId, "Bestehenden Pull Request reviewen", null, repository.Id, TestContext.Current.CancellationToken);
        _db.PullRequestReferences.Add(new PullRequestReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Role = PullRequestReferenceRole.ReviewSource,
            Provider = PullRequestProvider.GitHub,
            RepositoryId = "owner/repo",
            PullRequestNumber = 17,
            Title = "Bestehenden Pull Request reviewen",
            SourceBranch = "feature/existing-pr",
            SourceRepositoryId = "fork/repo",
            SourceRepositoryUrl = "https://github.com/fork/repo.git",
            SourceRef = "refs/heads/feature/existing-pr",
            TargetBranch = "main",
            HeadSha = "abc123",
            Status = PullRequestStatus.Open,
            MergeStatus = PullRequestMergeStatus.Unknown,
            MonitoringPhase = PullRequestMonitoringPhase.Created,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _gitPluginMock.Setup(g => g.CheckoutPullRequestSourceAsync(
                It.IsAny<string>(),
                It.IsAny<PullRequestCheckoutSpec>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            _gitPluginMock.Verify(g => g.CheckoutPullRequestSourceAsync(
                It.IsAny<string>(),
                It.Is<PullRequestCheckoutSpec>(spec =>
                    spec.TargetRepositoryId == "owner/repo"
                    && spec.SourceRepositoryId == "fork/repo"
                    && spec.SourceRepositoryUrl == "https://github.com/fork/repo.git"
                    && spec.SourceBranch == "feature/existing-pr"
                    && spec.SourceRef == "refs/heads/feature/existing-pr"
                    && spec.HeadSha == "abc123"),
                It.IsAny<CancellationToken>()), Times.Once);
            _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.BranchName.Should().Be("feature/existing-pr");
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// Ohne konfigurierten Basis-Branch wird der Feature-Branch weiterhin vom aktuellen HEAD abgezweigt:
    /// weder <see cref="IGitPlugin.CheckoutRemoteBranchAsync"/> noch ein <c>sourceBranchName</c> werden verwendet.
    /// </summary>
    [Fact]
    public async Task SetupBranchAsync_ShouldCreateBranchFromHead_WhenNotConfigured()
    {
        // Arrange
        var repository = await CreateRepositoryAsync(null);
        var workItem = await _workItemService.CreateAsync(_projectId, "Feature-Branch vom HEAD", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clonePath = SetupCloneWithDirectoryCreation();

        try
        {
            // Act
            await _sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

            // Assert
            _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _gitPluginMock.Verify(g => g.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }
}
