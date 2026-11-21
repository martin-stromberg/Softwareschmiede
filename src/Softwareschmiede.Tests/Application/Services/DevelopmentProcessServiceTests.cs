using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den EntwicklungsprozessService.</summary>
public sealed class DevelopmentProcessServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Mock<IAiPlugin> _aiPluginMock;
    private readonly Mock<IWorkingDirectoryResolver> _workingDirectoryResolverMock;
    private readonly AiExecutionService _aiExecutionService;
    private readonly DevelopmentProcessService _sut;
    private readonly Guid _projectId = new Guid("44444444-4444-4444-4444-444444444444");
    private const string CloneBaseDirectory = DevelopmentProcessService.CloneBaseDirectory;

    /// <summary>EntwicklungsprozessServiceTests.</summary>
    public DevelopmentProcessServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, new Mock<ILogger<WorkItemService>>().Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));
        _logService = new LogService(_db, new Mock<ILogger<LogService>>().Object);
        _gitPluginMock = new Mock<IGitPlugin>();
        _aiPluginMock = new Mock<IAiPlugin>();
        _aiPluginMock.SetupGet(p => p.PluginName).Returns("Test KI");
        _aiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        _aiPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _aiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _workingDirectoryResolverMock = new Mock<IWorkingDirectoryResolver>();
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(Path.GetTempPath(), false, "configured", null));

        _aiExecutionService = TestAiExecutionServiceFactory.Create();

        _sut = new DevelopmentProcessService(
            _workItemService,
            _logService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_aiPluginMock.Object),
            _workingDirectoryResolverMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _aiExecutionService),
            new Mock<ILogger<DevelopmentProcessService>>().Object);

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Entwicklungsprozess-Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _aiExecutionService.Dispose();
        _db.Dispose();
    }

    private PluginSelectionService CreatePluginSelectionService(params IAiPlugin[] aiPlugins)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns(aiPlugins);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(aiPlugins.First());

        var defaultService = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var activationService = new PluginActivationService(new AppSettingService(_db, NullLogger<AppSettingService>.Instance), pluginManagerMock.Object, new Mock<ILogger<PluginActivationService>>().Object);
        return new PluginSelectionService(pluginManagerMock.Object, defaultService, activationService, new Mock<ILogger<PluginSelectionService>>().Object);
    }

    private DevelopmentProcessService CreateSut(
        DevelopmentProcessServiceOptions? options = null,
        ILogger<DevelopmentProcessService>? logger = null)
    {
        return new DevelopmentProcessService(
            _workItemService,
            _logService,
            _gitPluginMock.Object,
            CreatePluginSelectionService(_aiPluginMock.Object),
            _workingDirectoryResolverMock.Object,
            options ?? new DevelopmentProcessServiceOptions(),
            logger ?? new Mock<ILogger<DevelopmentProcessService>>().Object);
    }

    private string SetupCloneWithDirectoryCreation(string? gitignoreContent = null)
    {
        var uniqueBase = Path.Combine(Path.GetTempPath(), $"sw-test-{Guid.NewGuid():N}");
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(uniqueBase, false, "configured", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                if (gitignoreContent != null)
                    File.WriteAllText(Path.Combine(path, ".gitignore"), gitignoreContent);
                return Task.CompletedTask;
            });
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return uniqueBase;
    }

    private void SetupCloneMocks()
    {
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }

    private async Task<GitRepository> CreateTestRepositoryAsync(string url, string name, RepositoryStartConfiguration? config = null)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Softwareschmiede.GitHub",
            RepositoryUrl = url,
            RepositoryName = name,
            Active = true
        };
        if (config is not null)
        {
            config.GitRepository = repository;
            repository.StartConfiguration = config;
        }
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    /// <summary>StartProcessAsync klont das Repository und legt einen Branch an.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldCloneAndCreateBranch_WhenWorkItemExists()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Login Feature implementieren", null, ct: TestContext.Current.CancellationToken);
        SetupCloneMocks();

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.Is<string>(b => b.Contains("login-feature")), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
        updatedWorkItem.BranchName.Should().Contain("login-feature");
    }

    /// <summary>StartProcessAndCliAsync klont, setzt Status auf Started und startet die CLI.</summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldStartCliAndSetStatusStarted_WhenStartSucceeds()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Kombinierter Start", null, ct: TestContext.Current.CancellationToken);
        SetupCloneMocks();
        _aiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        _aiPluginMock.SetupTerminalSpec("cmd.exe", "/c exit 0");

        // Act
        await _sut.StartProcessAndCliAsync(workItem.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

        // Assert
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
        _aiExecutionService.IsRunning(workItem.Id).Should().BeTrue();
    }

    /// <summary>StartProcessAndCliAsync setzt Status zurück, wenn das Klonen fehlschlägt.</summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldRollbackStatus_WhenRepositoryCloneFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Klonen fehlschlägt", null, ct: TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Klonen fehlgeschlagen"));

        // Act
        var act = () => _sut.StartProcessAndCliAsync(workItem.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.New);
    }

    /// <summary>StartProcessAndCliAsync setzt Status zurück, wenn der CLI-Start fehlschlägt.</summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldRollbackStatus_WhenCliStartFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "CLI-Start fehlschlägt", null, ct: TestContext.Current.CancellationToken);
        SetupCloneMocks();
        _aiPluginMock.Setup(p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CLI-Start fehlgeschlagen"));

        // Act
        var act = () => _sut.StartProcessAndCliAsync(workItem.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.New);
    }

    /// <summary>
    /// StartProcessAndCliAsync übergibt dem KI-Plugin das konfigurierte Arbeitsunterverzeichnis
    /// (aus der RepositoryStartKonfiguration), statt den Repository-Root zu verwenden.
    /// </summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldUseConfiguredWorkingDirectory_WhenStartConfigHasWorkingDirectory()
    {
        // Arrange
        var repository = await CreateTestRepositoryAsync(
            "https://github.com/test/repo-workdir",
            "repo-workdir",
            new RepositoryStartConfiguration { Id = Guid.NewGuid(), WorkingDirectoryRelativePath = "backend", Active = true });

        var workItem = await _workItemService.CreateAsync(_projectId, "Start mit Arbeitsverzeichnis", null, repository.Id, TestContext.Current.CancellationToken);

        var clonePath = SetupCloneWithDirectoryCreation();
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(Path.Combine(path, "backend"));
                return Task.CompletedTask;
            });

        string? usedPath = null;
        _aiPluginMock.Setup(p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((path, _, _) => usedPath = path)
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
            });

        var projectService = new ProjectService(_db, new Mock<ILogger<ProjectService>>().Object);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(ProjectService: projectService, AiExecutionService: _aiExecutionService));

        try
        {
            // Act
            await sut.StartProcessAndCliAsync(workItem.Id, repository.RepositoryUrl, null, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

            // Assert
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
            usedPath.Should().Be(Path.Combine(updatedWorkItem.LocalClonePath!, "backend"));
        }
        finally
        {
            await _aiExecutionService.StopCliAsync(workItem.Id, TestContext.Current.CancellationToken);
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>
    /// StartProcessAndCliAsync bricht mit Rollback ab, wenn das konfigurierte Arbeitsverzeichnis
    /// nach dem Klon nicht existiert.
    /// </summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldRollback_WhenConfiguredWorkingDirectoryMissing()
    {
        // Arrange
        var repository = await CreateTestRepositoryAsync(
            "https://github.com/test/repo-workdir-missing",
            "repo-workdir-missing",
            new RepositoryStartConfiguration { Id = Guid.NewGuid(), WorkingDirectoryRelativePath = "does-not-exist", Active = true });

        var workItem = await _workItemService.CreateAsync(_projectId, "Start mit fehlendem Arbeitsverzeichnis", null, repository.Id, TestContext.Current.CancellationToken);

        var clonePath = SetupCloneWithDirectoryCreation();

        var projectService = new ProjectService(_db, new Mock<ILogger<ProjectService>>().Object);
        var pluginSelectionService = CreatePluginSelectionService(_aiPluginMock.Object);
        // GitOrchestrationService wird hier bewusst mitkonfiguriert (wie auch in der echten App-Verdrahtung,
        // siehe App.xaml.cs), damit das fehlende Arbeitsverzeichnis direkt nach dem Klon erkannt wird — und
        // nicht erst durch CreateIssueFileAsync/UpdateGitignoreAsync stillschweigend angelegt wird, bevor der
        // spätere CLI-Start es prüfen könnte.
        var gitOrchestrationService = new GitOrchestrationService(
            _workItemService,
            projectService,
            _logService,
            _gitPluginMock.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(ProjectService: projectService, AiExecutionService: _aiExecutionService, GitOrchestrationService: gitOrchestrationService));

        try
        {
            // Act
            var act = () => sut.StartProcessAndCliAsync(workItem.Id, repository.RepositoryUrl, null, "Softwareschmiede.TestKi");

            // Assert
            await act.Should().ThrowAsync<DirectoryNotFoundException>();
            var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.New);
        }
        finally
        {
            DeleteDirectoryIfExists(clonePath);
        }
    }

    /// <summary>StartProcessAsync blockiert den Start nicht, wenn das Repository-Startskript fehlschlägt.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldContinue_WhenRepositoryStartScriptFails()
    {
        // Arrange
        var repository = await CreateTestRepositoryAsync(
            "https://github.com/test/repo-start-script",
            "repo-start-script",
            new RepositoryStartConfiguration { Id = Guid.NewGuid(), StartScriptRelativePath = "scripts/start.ps1", Active = true });

        var workItem = await _workItemService.CreateAsync(_projectId, "Startskript robust starten", null, repository.Id, TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock.Setup(runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "script failed"));
        var repositoryStartscriptService = new RepositoryStartScriptService(
            cliRunnerMock.Object,
            new Mock<ILogger<RepositoryStartScriptService>>().Object);
        var projectService = new ProjectService(_db, new Mock<ILogger<ProjectService>>().Object);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(ProjectService: projectService, RepositoryStartScriptService: repositoryStartscriptService));

        // Act
        await sut.StartProcessAsync(workItem.Id, repository.RepositoryUrl, ct: TestContext.Current.CancellationToken);

        // Assert
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem.Should().NotBeNull();
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
        var log = await _logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        log.Should().Contain(entry =>
            entry.Type == LogEntryType.GitAction
            && entry.Content.Contains("Hinweis: Das Repository-Startskript konnte nicht ausgeführt werden", StringComparison.Ordinal));
    }

    /// <summary>StartProcessAsync erstellt einen Issue-Branch wenn die Aufgabe eine Issue-Referenz hat.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldCreateIssueBranch_WhenWorkItemHasIssueReference()
    {
        var issue = new Issue(321, "Issue Branch", "Body", ["enhancement"], null, "https://github.com/test/repo/issues/321");
        var workItem = await _workItemService.CreateFromIssueAsync(_projectId, issue, ct: TestContext.Current.CancellationToken);

        SetupCloneMocks();

        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        _gitPluginMock.Verify(g => g.CreateBranchAsync(
            It.IsAny<string>(),
            It.Is<string>(branch => branch.StartsWith($"task/issue-321-{workItem.Id:N}") && branch.Contains("-issue-branch")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var updatedWorkItem = await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.BranchName.Should().StartWith($"task/issue-321-{workItem.Id:N}");
    }

    /// <summary>StartProcessAndCliAsync startet Aufgaben ohne Issue über das eindeutige Projekt-Repository.</summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldStartTaskWithoutIssueReference_WhenSingleProjectRepositoryExists()
    {
        var repository = await CreateTestRepositoryAsync("https://github.com/test/repo-ohne-issue", "repo-ohne-issue");

        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Issue", "Beschreibung", ct: TestContext.Current.CancellationToken);
        var cloneBase = SetupCloneWithDirectoryCreation();
        _aiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 5 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        _aiPluginMock.SetupTerminalSpec("cmd.exe", "/c ping 127.0.0.1 -n 5 > nul");
        var projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(ProjectService: projectService, AiExecutionService: _aiExecutionService));

        try
        {
            await sut.StartProcessAndCliAsync(workItem.Id, string.Empty, null, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

            var updatedWorkItem = await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
            updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
            updatedWorkItem.IssueReference.Should().BeNull();
            updatedWorkItem.BranchName.Should().StartWith($"task/{workItem.Id:N}-aufgabe-ohne-issue");
            _gitPluginMock.Verify(g => g.CloneRepositoryAsync(repository.RepositoryUrl, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await _aiExecutionService.StopCliAsync(workItem.Id, TestContext.Current.CancellationToken);
            DeleteDirectoryIfExists(cloneBase);
        }
    }

    /// <summary>StartProcessAsync wirft Exception wenn Aufgabe nicht gefunden.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldThrowInvalidOperationException_WhenWorkItemDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.StartProcessAsync(nonExistentId, "https://github.com/test/repo");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>StartProcessAsync lehnt eine aktiv pausierte Aufgabe ab, ohne das Repository zu klonen (Issue 151).</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldThrow_WhenWorkItemPaused()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        SetupCloneMocks();

        // Act
        var act = () => _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pausiert*");
        _gitPluginMock.Verify(
            g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>StartProcessAndCliAsync lehnt eine pausierte Aufgabe ab, ohne den Rollback-Pfad auszulösen (Issue 151).</summary>
    [Fact]
    public async Task StartProcessStartAndCliAsync_ShouldThrowWithoutRollback_WhenWorkItemPaused()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        SetupCloneMocks();

        // Act
        var act = () => _sut.StartProcessAndCliAsync(workItem.Id, "https://github.com/test/repo", null, "Softwareschmiede.TestKi");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pausiert*");
        (await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken))!.Status.Should().Be(WorkItemStatus.New,
            "die Pausen-Prüfung läuft vor dem try-Block und darf keinen Status-Rollback auslösen");
        _gitPluginMock.Verify(
            g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>CliNeustartenAsync lehnt eine aktiv pausierte Aufgabe ab (Issue 151).</summary>
    [Fact]
    public async Task CliRestartAsync_ShouldThrow_WhenWorkItemPaused()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = WorkItemStatus.Started;
        tracked.PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
        tracked.LocalClonePath = @"C:\repos\task-pausiert";
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var act = () => _sut.RestartCliAsync(workItem.Id, "Softwareschmiede.TestKi", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pausiert*");
    }

    /// <summary>AbschliessenAsync setzt den Status auf Beendet und fügt einen Statusübergang ins Protokoll ein.</summary>
    [Fact]
    public async Task CompleteAsync_ShouldSetStatusCompletedAndAddLog_WhenWorkItemExists()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Abzuschließende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var result = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Completed);

        var log = await _logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        log.Should().Contain(p => p.Type == LogEntryType.StatusTransition);
    }

    /// <summary>AbschliessenAsync zieht einen geplanten zeitgesteuerten Prompt zurück, wenn
    /// PromptScheduleDispatchService in den Options konfiguriert ist.</summary>
    [Fact]
    public async Task CompleteAsync_ShouldCancelScheduledPrompt_WhenPromptPlanned()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe mit geplantem Prompt", null, ct: TestContext.Current.CancellationToken);
        var promptScheduleDispatchService = new PromptScheduleDispatchService(
            _aiExecutionService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(
            AiExecutionService: _aiExecutionService,
            PromptScheduleDispatchService: promptScheduleDispatchService));
        await promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test-Prompt", DateTimeOffset.UtcNow.AddHours(1));
        promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull();

        // Act
        await sut.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().BeNull();
    }

    /// <summary>AbschliessenAsync beendet eine laufende CLI-Session über StopCliAsync
    /// (nachweisbar am IntentionallyStopped-Flag des Handles).</summary>
    [Fact]
    public async Task CompleteAsync_ShouldStopRunningCli_WhenSessionActive()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe mit laufender CLI", null, ct: TestContext.Current.CancellationToken);
        var directory = Path.Combine(Path.GetTempPath(), $"sw-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        _aiPluginMock.Setup(p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.TerminalSessionStartSpec
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
            });

        try
        {
            var handle = await _aiExecutionService.StartTerminalSessionAsync(workItem.Id, _aiPluginMock.Object, directory, ct: TestContext.Current.CancellationToken);
            handle.IntentionallyStopped.Should().BeFalse();

            // Act
            await _sut.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

            // Assert
            handle.IntentionallyStopped.Should().BeTrue();
        }
        finally
        {
            await _aiExecutionService.StopCliAsync(workItem.Id, TestContext.Current.CancellationToken);
            DeleteDirectoryIfExists(directory);
        }
    }

    /// <summary>CommitDurchfuehrenAsync wirft Exception wenn kein LocalClonePath gesetzt.</summary>
    [Fact]
    public async Task CommitPerformAsync_ShouldThrowInvalidOperationException_WhenNoClonePath()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Commit ohne Klon", null, ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => _sut.ExecuteCommitAsync(workItem.Id, "Commit message");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lokalen Klonpfad*");
    }

    /// <summary>PushDurchfuehrenAsync wirft Exception wenn kein LocalClonePath gesetzt.</summary>
    [Fact]
    public async Task PushPerformAsync_ShouldThrowInvalidOperationException_WhenNoClonePath()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Push ohne Klon", null, ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => _sut.ExecutePushAsync(workItem.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>PullRequestErstellenAsync ergänzt im älteren Prozesspfad die Closing-Direktive aus der Issue-Referenz.</summary>
    [Fact]
    public async Task CreatePullRequestAsync_ShouldAppendClosingDirective_WhenLegacyPathHasIssueReference()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Legacy PR mit Issue", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/legacy-pr", @"C:\repos\task-legacy-pr", ct: TestContext.Current.CancellationToken);
        _db.IssueReferences.Add(new IssueReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            IssueNumber = 17,
            Title = "Issue 17",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expectedBody = $"Legacy Body{Environment.NewLine}{Environment.NewLine}Closes #17";
        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "test/repo",
                "feature/legacy-pr",
                null,
                "Legacy Titel",
                expectedBody,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(17, "Legacy Titel", "https://example/pr/17", "feature/legacy-pr"));

        await _sut.CreatePullRequestAsync(workItem.Id, "test/repo", "Legacy Titel", "Legacy Body", TestContext.Current.CancellationToken);

        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "test/repo",
            "feature/legacy-pr",
            null,
            "Legacy Titel",
            expectedBody,
            It.IsAny<CancellationToken>()), Times.Once);

        var log = await _logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        log.Should().Contain(e => e.Type == LogEntryType.GitAction && e.Content.Contains("Issue #17"));
    }

    /// <summary>StartProcessAsync verwendet das konfigurierte Arbeitsverzeichnis als Basis für den Klon-Pfad.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldUseConfiguredWorkdirBase_ForClonePath()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Workdir Konfiguriert", null, ct: TestContext.Current.CancellationToken);
        var configuredBase = Path.Combine(Path.GetTempPath(), "custom-workdir-base");
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(configuredBase, false, "configured", configuredBase));
        SetupCloneMocks();

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var expectedPath = Path.Combine(configuredBase, CloneBaseDirectory, workItem.Id.ToString());
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(
            "https://github.com/test/repo",
            expectedPath,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartProcessAsync verwendet den Fallback-Pfad wenn der Resolver einen Fallback zurückgibt.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldUseFallbackPath_WhenResolverReturnsFallback()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Workdir Fallback", null, ct: TestContext.Current.CancellationToken);
        var fallbackBase = Path.GetTempPath();
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(fallbackBase, true, "no-configured-path", null));
        SetupCloneMocks();

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var expectedPath = Path.Combine(fallbackBase, CloneBaseDirectory, workItem.Id.ToString());
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync(
            "https://github.com/test/repo",
            expectedPath,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartProcessAsync checkt einen existierenden Branch aus wenn der Basis-Branch nicht der Default-Branch ist.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldCheckoutExistingBranch_WhenBaseBranchIsNotDefault()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Existing Branch", null, ct: TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", "feature/existing-branch", ct: TestContext.Current.CancellationToken);

        // Assert
        _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(
            It.IsAny<string>(),
            "feature/existing-branch",
            It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.BranchName.Should().Be("feature/existing-branch");
    }

    /// <summary>StartProcessAsync erstellt einen neuen Task-Branch, wenn Basis- und Default-Branch nur in der Groß-/Kleinschreibung abweichen.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldCreateTaskBranch_WhenBaseBranchEqualsDefaultBranch_CaseInsensitive()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Case Insensitive Branch", null, ct: TestContext.Current.CancellationToken);
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginMock.Setup(g => g.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", "MAIN", ct: TestContext.Current.CancellationToken);

        // Assert
        _gitPluginMock.Verify(g => g.GetDefaultBranchAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.Once);
        _gitPluginMock.Verify(g => g.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gitPluginMock.Verify(g => g.CreateBranchAsync(
            It.IsAny<string>(),
            It.Is<string>(branch => branch.StartsWith("task/", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.BranchName.Should().StartWith("task/");
        updatedWorkItem.BranchName.Should().NotBe("MAIN");
    }

    /// <summary>StartProcessAsync löscht ein bereits vorhandenes Klon-Verzeichnis vor dem Klonen.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldDeleteExistingCloneDirectory_BeforeClone()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Delete Existing Clone", null, ct: TestContext.Current.CancellationToken);
        var configuredBase = Path.Combine(Path.GetTempPath(), $"workdir-existing-{Guid.NewGuid():N}");
        var expectedClonePath = Path.Combine(configuredBase, CloneBaseDirectory, workItem.Id.ToString());
        Directory.CreateDirectory(expectedClonePath);
        var readOnlyFile = Path.Combine(expectedClonePath, "readonly.txt");
        await File.WriteAllTextAsync(readOnlyFile, "content", TestContext.Current.CancellationToken);
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(configuredBase, false, "configured", configuredBase));
        SetupCloneMocks();

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        _gitPluginMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expectedClonePath, It.IsAny<CancellationToken>()), Times.Once);
        // Das alte, schreibgeschützte Verzeichnis wird vor dem Klonen komplett entfernt. Dass anschließend
        // CreateIssueFileAsync das (vom Clone-Mock nicht neu angelegte) Zielverzeichnis als Sicherheitsnetz
        // per Directory.CreateDirectory wiederherstellt, ist gewolltes Verhalten und kein Hinweis auf ein
        // fehlgeschlagenes Löschen — daher wird hier gezielt die alte Datei statt des ganzen Verzeichnisses geprüft.
        File.Exists(readOnlyFile).Should().BeFalse();
        DeleteDirectoryIfExists(configuredBase);
    }

    /// <summary>StartProcessAsync wirft eine Exception wenn der Repository-Kontext mehrdeutig ist.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldThrow_WhenRepositoryContextIsAmbiguous()
    {
        // Arrange
        await CreateTestRepositoryAsync("https://github.com/test/repo-a", "repo-a");
        await CreateTestRepositoryAsync("https://github.com/test/repo-b", "repo-b");

        var workItem = await _workItemService.CreateAsync(_projectId, "Mehrdeutiger Repository-Kontext", null, ct: TestContext.Current.CancellationToken);
        var projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        var sut = CreateSut(new DevelopmentProcessServiceOptions(ProjectService: projectService));

        // Act
        var act = () => sut.StartProcessAsync(workItem.Id, "https://github.com/test/unknown");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kein eindeutiges Repository*");
        _gitPluginMock.Verify(git => git.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>CreateIssueFileAsync erstellt eine issue.md mit korrektem Markdown-Inhalt.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenWorkItemExists()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Login Feature implementieren", "Benutzer soll sich einloggen können.", ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var issueFilePath = Path.Combine(expectedClonePath, "issue.md");
        File.Exists(issueFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(issueFilePath, TestContext.Current.CancellationToken);
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        content.Should().Contain("# Aufgabe: Login Feature implementieren");
        content.Should().Contain(workItem.Id.ToString());
        content.Should().Contain($"**Branch:** {updatedWorkItem!.BranchName}");
        content.Should().Contain("**Erstellt:**");
        content.Should().Contain("## Anforderung");
        content.Should().Contain("Benutzer soll sich einloggen können.");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>CreateIssueFileAsync verwendet Fallback-Text wenn AnforderungsBeschreibung null oder leer ist.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldUseFallbackText_WhenRequirementDescriptionIsNullOrEmpty()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Beschreibung", null, ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var issueFilePath = Path.Combine(expectedClonePath, "issue.md");
        File.Exists(issueFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(issueFilePath, TestContext.Current.CancellationToken);
        content.Should().Contain("[Keine Anforderungsbeschreibung verfügbar]");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>CreateIssueFileAsync loggt Warnung wenn Datei nicht erstellt werden kann, wirft keine Exception.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldLogWarning_WhenFileCreationFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe Datei-Fehler", "Beschreibung", ct: TestContext.Current.CancellationToken);
        var loggerMock = new Mock<ILogger<DevelopmentProcessService>>();
        var sut = CreateSut(logger: loggerMock.Object);

        // CloneRepositoryAsync erstellt das Verzeichnis und legt eine schreibgeschützte issue.md an,
        // damit File.WriteAllTextAsync deterministisch scheitert.
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                var issueFilePath = Path.Combine(path, "issue.md");
                File.WriteAllText(issueFilePath, "existing content");
                File.SetAttributes(issueFilePath, FileAttributes.ReadOnly);
                return Task.CompletedTask;
            });
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clonePath = Path.Combine(Path.GetTempPath(), CloneBaseDirectory, workItem.Id.ToString());

        // Act
        var act = () => sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e != null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);

        DeleteDirectoryIfExists(clonePath);
    }

    /// <summary>CreateIssueFileAsync schreibt Verknüpftes-Issue-Abschnitt mit Kennung und Titel wenn IssueReferenz mit gültiger Nummer gesetzt ist.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenceIsSet()
    {
        // Arrange
        var issue = new Issue(42, "Anmeldung fehlerhaft", "Beschreibung des Issues", [], null, "https://github.com/test/repo/issues/42");
        var workItem = await _workItemService.CreateFromIssueAsync(_projectId, issue, ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var issueFilePath = Path.Combine(expectedClonePath, "issue.md");
        File.Exists(issueFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(issueFilePath, TestContext.Current.CancellationToken);
        content.Should().Contain("## Verknüpftes Issue");
        content.Should().Contain("#42");
        content.Should().Contain("Anmeldung fehlerhaft");

        // Reihenfolge: Issue-Abschnitt nach Metadaten, vor ## Anforderung
        var posIssueSection = content.IndexOf("## Verknüpftes Issue", StringComparison.Ordinal);
        var posRequirement = content.IndexOf("## Anforderung", StringComparison.Ordinal);
        posIssueSection.Should().BeLessThan(posRequirement);

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>CreateIssueFileAsync lässt den Issue-Abschnitt weg wenn IssueReferenz null ist.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenceIsNull()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Issue", "Normale Beschreibung", ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var issueFilePath = Path.Combine(expectedClonePath, "issue.md");
        File.Exists(issueFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(issueFilePath, TestContext.Current.CancellationToken);
        content.Should().NotContain("## Verknüpftes Issue");
        content.Should().Contain("## Anforderung");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>CreateIssueFileAsync lässt den Issue-Abschnitt weg wenn IssueReferenz gesetzt ist, aber IssueNumber null ist.</summary>
    [Fact]
    public async Task CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueNumberIsNull()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Nummer", "Beschreibung ohne Nummer", ct: TestContext.Current.CancellationToken);
        _db.IssueReferences.Add(new IssueReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            IssueNumber = null,
            Title = "Issue ohne Nummer",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var issueFilePath = Path.Combine(expectedClonePath, "issue.md");
        File.Exists(issueFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(issueFilePath, TestContext.Current.CancellationToken);
        content.Should().NotContain("## Verknüpftes Issue");
        content.Should().Contain("## Anforderung");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>UpdateGitignoreAsync erstellt eine neue .gitignore mit dem Eintrag issue.md wenn keine existiert.</summary>
    [Fact]
    public async Task UpdateGitignoreAsync_ShouldCreateGitignore_WhenFileDoesNotExist()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Gitignore neu", null, ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var gitignorePath = Path.Combine(expectedClonePath, ".gitignore");
        File.Exists(gitignorePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(gitignorePath, TestContext.Current.CancellationToken);
        lines.Should().Contain("issue.md");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>UpdateGitignoreAsync fügt issue.md zu einer bestehenden .gitignore hinzu.</summary>
    [Fact]
    public async Task UpdateGitignoreAsync_ShouldAddIssueEntry_WhenGitignoreExists()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Gitignore erweitern", null, ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation("*.log\nbin/\n");
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var gitignorePath = Path.Combine(expectedClonePath, ".gitignore");
        var lines = await File.ReadAllLinesAsync(gitignorePath, TestContext.Current.CancellationToken);
        lines.Should().Contain("*.log");
        lines.Should().Contain("issue.md");

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>UpdateGitignoreAsync fügt issue.md nicht doppelt ein wenn der Eintrag bereits existiert.</summary>
    [Fact]
    public async Task UpdateGitignoreAsync_ShouldNotAddDuplicate_WhenIssueEntryAlreadyExists()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Gitignore kein Duplikat", null, ct: TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation("*.log\nissue.md\n");
        var expectedClonePath = Path.Combine(uniqueBase, CloneBaseDirectory, workItem.Id.ToString());

        // Act
        await _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var gitignorePath = Path.Combine(expectedClonePath, ".gitignore");
        var lines = await File.ReadAllLinesAsync(gitignorePath, TestContext.Current.CancellationToken);
        lines.Count(l => l == "issue.md").Should().Be(1);

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>
    /// StartProcessAsync schreibt issue.md und den .gitignore-Eintrag in das konfigurierte Arbeitsverzeichnis
    /// (RepositoryStartKonfiguration.WorkingDirectoryRelativePath), statt in den Repository-Root.
    /// </summary>
    [Fact]
    public async Task StartProcessAsync_ShouldWriteIssueFileAndGitignoreIntoWorkingDirectory_WhenWorkingDirectoryConfigured()
    {
        // Arrange
        var repository = await CreateTestRepositoryAsync(
            "https://github.com/test/repo-issuefile-workdir",
            "repo-issuefile-workdir",
            new RepositoryStartConfiguration { Id = Guid.NewGuid(), WorkingDirectoryRelativePath = "backend", Active = true });

        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Datei im Arbeitsverzeichnis", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var uniqueBase = SetupCloneWithDirectoryCreation();
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
            var repositoryRoot = updatedWorkItem!.LocalClonePath!;
            var workingDirectory = Path.Combine(repositoryRoot, "backend");

            File.Exists(Path.Combine(repositoryRoot, "issue.md")).Should().BeFalse();
            File.Exists(Path.Combine(workingDirectory, "issue.md")).Should().BeTrue();

            File.Exists(Path.Combine(repositoryRoot, ".gitignore")).Should().BeFalse();
            var gitignorePath = Path.Combine(workingDirectory, ".gitignore");
            File.Exists(gitignorePath).Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(gitignorePath, TestContext.Current.CancellationToken);
            lines.Should().Contain("issue.md");
        }
        finally
        {
            DeleteDirectoryIfExists(uniqueBase);
        }
    }

    /// <summary>UpdateGitignoreAsync loggt Warnung wenn .gitignore nicht geschrieben werden kann, wirft keine Exception.</summary>
    [Fact]
    public async Task UpdateGitignoreAsync_ShouldLogWarning_WhenFileOperationFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Gitignore schreibgeschützt", null, ct: TestContext.Current.CancellationToken);
        var uniqueBase = Path.Combine(Path.GetTempPath(), $"sw-test-{Guid.NewGuid():N}");
        var loggerMock = new Mock<ILogger<DevelopmentProcessService>>();
        var sut = CreateSut(logger: loggerMock.Object);

        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(uniqueBase, false, "configured", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                var gitignorePath = Path.Combine(path, ".gitignore");
                File.WriteAllText(gitignorePath, "*.log\n");
                File.SetAttributes(gitignorePath, FileAttributes.ReadOnly);
                return Task.CompletedTask;
            });
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = () => sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>StartProcessAsync setzt den Prozess fort wenn CreateIssueFileAsync fehlschlägt.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldContinue_WhenIssueFileCreationFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Prozess trotz Datei-Fehler", "Beschreibung", ct: TestContext.Current.CancellationToken);
        // CloneRepositoryAsync erstellt das Verzeichnis nicht → Datei-Operationen schlagen fehl
        SetupCloneMocks();

        // Act
        var act = () => _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().NotThrowAsync();
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>StartProcessAsync setzt den Prozess fort wenn UpdateGitignoreAsync fehlschlägt.</summary>
    [Fact]
    public async Task StartProcessAsync_ShouldContinue_WhenGitignoreUpdateFails()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Prozess trotz Gitignore-Fehler", "Beschreibung", ct: TestContext.Current.CancellationToken);
        var uniqueBase = Path.Combine(Path.GetTempPath(), $"sw-test-{Guid.NewGuid():N}");
        _workingDirectoryResolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(uniqueBase, false, "configured", null));
        _gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string path, CancellationToken _) =>
            {
                Directory.CreateDirectory(path);
                var gitignorePath = Path.Combine(path, ".gitignore");
                File.WriteAllText(gitignorePath, "*.log\n");
                File.SetAttributes(gitignorePath, FileAttributes.ReadOnly);
                return Task.CompletedTask;
            });
        _gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = () => _sut.StartProcessAsync(workItem.Id, "https://github.com/test/repo");

        // Assert
        await act.Should().NotThrowAsync();
        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.Status.Should().Be(WorkItemStatus.Started);

        DeleteDirectoryIfExists(uniqueBase);
    }

    /// <summary>GetRemoteBranchesAsync löst das gewünschte SCM-Plugin über den Prefix auf und liefert dessen Branches zurück.</summary>
    [Fact]
    public async Task GetRemoteBranchesAsync_ShouldResolvePluginBySelectedPrefix_AndReturnPluginBranches()
    {
        // Arrange
        var defaultGitPluginMock = new Mock<IGitPlugin>();
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Default Git");
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("LocalDirectoryPlugin");
        defaultGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        defaultGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        defaultGitPluginMock.Setup(plugin => plugin.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main"]);

        var selectedGitPluginMock = new Mock<IGitPlugin>();
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("GitHub");
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Softwareschmiede.GitHub");
        selectedGitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        selectedGitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        selectedGitPluginMock.Setup(plugin => plugin.GetRemoteBranchesAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["develop", "release/1.0"]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns([defaultGitPluginMock.Object, selectedGitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(defaultGitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(_aiPluginMock.Object);

        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginActivationService = new PluginActivationService(new AppSettingService(_db, NullLogger<AppSettingService>.Instance), pluginManagerMock.Object, new Mock<ILogger<PluginActivationService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            pluginActivationService,
            new Mock<ILogger<PluginSelectionService>>().Object);

        var sut = new DevelopmentProcessService(
            _workItemService,
            _logService,
            defaultGitPluginMock.Object,
            pluginSelectionService,
            _workingDirectoryResolverMock.Object,
            new Mock<ILogger<DevelopmentProcessService>>().Object);

        // Act
        var result = (await sut.GetRemoteBranchesAsync("https://github.com/test/repo", "  softwaRESchmiede.githUB  ", TestContext.Current.CancellationToken)).ToArray();

        // Assert
        result.Should().BeEquivalentTo(["develop", "release/1.0"]);
        selectedGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.Once);
        defaultGitPluginMock.Verify(plugin => plugin.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// CliNeustartenAsync meldet bei einer noch lebenden Terminal-Session den laufenden Prozess über
    /// CliLaufAktiv (statt eines fehlenden Gestartet-Ereignisses), damit CliProcessManager die
    /// Laufdaten-Persistenz nachziehen kann (Issue 342, H3).
    /// </summary>
    [Fact]
    public async Task CliRestartAsync_ReportsRunActive_AtLiveSession()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Neustart mit lebender Session", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/live-session", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        _aiPluginMock.SetupTerminalSpec("cmd.exe", "/k");
        var handle = await _aiExecutionService.StartTerminalSessionAsync(workItem.Id, _aiPluginMock.Object, Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        var runActiveCount = 0;
        _aiExecutionService.CliRunActive += _ => runActiveCount++;

        var sut = CreateSut(new DevelopmentProcessServiceOptions(AiExecutionService: _aiExecutionService));
        await sut.RestartCliAsync(workItem.Id, "Softwareschmiede.TestKi", null, TestContext.Current.CancellationToken);

        runActiveCount.Should().Be(1, "der Early-Return bei lebender Session meldet den laufenden Prozess");
        _aiExecutionService.GetTerminalSession(workItem.Id).Should().BeSameAs(handle.Session);
    }

}
