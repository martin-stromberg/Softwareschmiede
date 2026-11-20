using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>
/// Integrationstests für <see cref="DevelopmentProcessService"/> mit echter SQLite-Datenbank.
/// CLI-Plugins (IGitPlugin, IKiPlugin) werden gemockt.
/// </summary>
public sealed class EntwicklungsprozessServiceTests
{
    /// <summary>Hilfsmethode: Erstellt Testprojekt und Testaufgabe ohne AgentenpaketName.</summary>
    private static async Task<(Guid ProjectId, Guid WorkItemId)> CreateTestDataAsync(
        DatabaseFixture db,
        string aufgabeTitel = "Entwicklungsaufgabe")
    {
        var projektService = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var project = await projektService.CreateAsync("Entwicklungstestprojekt", null);
        var workItem = await workItemService.CreateAsync(project.Id, aufgabeTitel, "Mach etwas Tolles");
        return (project.Id, workItem.Id);
    }

    /// <summary>Erstellt eine konfigurierte Instanz von <see cref="DevelopmentProcessService"/>.</summary>
    private static DevelopmentProcessService CreateService(
        DatabaseFixture db,
        Mock<IGitPlugin> gitMock,
        Mock<IKiPlugin> kiMock,
        IWorkingDirectoryResolver? workingDirectoryResolver = null)
    {
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var logService = new LogService(db.Context, NullLogger<LogService>.Instance);
        workingDirectoryResolver ??= new RuntimeWorkingDirectoryResolver(
            new WorkingDirectorySettingsService(db.Context, NullLogger<WorkingDirectorySettingsService>.Instance),
            NullLogger<RuntimeWorkingDirectoryResolver>.Instance);

        return new DevelopmentProcessService(
            workItemService,
            logService,
            gitMock.Object,
            CreatePluginSelectionService(db, gitMock.Object, kiMock.Object),
            workingDirectoryResolver,
            NullLogger<DevelopmentProcessService>.Instance);
    }

    private static PluginSelectionService CreatePluginSelectionService(
        DatabaseFixture db,
        IGitPlugin gitPlugin,
        IKiPlugin aiPlugin)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManagerMock.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin]);
        pluginManagerMock.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(aiPlugin);

        var defaultService = new PluginDefaultSettingsService(db.Context, NullLogger<PluginDefaultSettingsService>.Instance);
        var settingService = new AppSettingService(db.Context, NullLogger<AppSettingService>.Instance);
        var activationService = new PluginActivationService(settingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        return new PluginSelectionService(pluginManagerMock.Object, defaultService, activationService, NullLogger<PluginSelectionService>.Instance);
    }

    /// <summary>
    /// Testet, dass ProzessStartenAsync BranchName und LokalerKlonPfad in der DB persistiert.
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldPersistBranchAndKlonPfad_WhenAufgabeExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Login-Feature implementieren");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert – Branch und Klonpfad wurden in der DB gespeichert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItemId }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.BranchName.Should().StartWith("task/");
        loaded.BranchName.Should().Contain(workItemId.ToString("N"));
        loaded.LocalClonePath.Should().NotBeNullOrEmpty();
    }

    /// <summary>Prüft, dass bei Issue-basierten Aufgaben ein Issue-Präfix im Branch-Namen persistiert wird.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldPersistIssueBasedBranch_WhenAufgabeWasCreatedFromIssue()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projektService = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var project = await projektService.CreateAsync("Issue-Branch-Projekt", null, TestContext.Current.CancellationToken);
        var issue = new Issue(55, "Issue-basierte Aufgabe", "Beschreibung", ["feature"], null, "https://github.com/test/repo/issues/55");
        var workItem = await workItemService.CreateFromIssueAsync(project.Id, issue, ct: TestContext.Current.CancellationToken);

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        await service.StartProcessAsync(workItem.Id, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.BranchName.Should().StartWith($"task/issue-55-{workItem.Id:N}");
    }

    /// <summary>
    /// Testet, dass ProzessStartenAsync einen GitAktion-Protokolleintrag in der DB anlegt.
    /// </summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldCreateGitAktionProtokollEintrag_WhenProcessStarted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Feature Aufgabe");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert – Protokolleintrag wurde angelegt
        await using var db2 = db.CreateNewContext();
        var protokolle = await db2.LogEntries
            .Where(p => p.WorkItemId == workItemId && p.Type == LogEntryType.GitAction)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        protokolle.Should().Contain(p => p.Content.Contains("Klon und Branch angelegt"));
    }

    /// <summary>
    /// Testet, dass AbschliessenAsync den Status auf Beendet setzt und einen Statusübergang protokolliert.
    /// </summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusBeendetAndCreateStatusUebergang_WhenAufgabeInBearbeitung()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db);

        // Aufgabe zuerst starten
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        await workItemService.StartAsync(workItemId, "task/test-branch", @"C:\nicht\vorhanden\pfad", ct: TestContext.Current.CancellationToken);

        var gitMock = new Mock<IGitPlugin>();
        var kiMock = new Mock<IKiPlugin>();

        var service = CreateService(db, gitMock, kiMock);

        // Act
        await service.CompleteAsync(workItemId, TestContext.Current.CancellationToken);

        // Assert – Status und Protokolleintrag
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItemId }, TestContext.Current.CancellationToken);
        var statusEintrag = await db2.LogEntries
            .FirstOrDefaultAsync(p => p.WorkItemId == workItemId && p.Type == LogEntryType.StatusTransition, cancellationToken: TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Completed);
        statusEintrag.Should().NotBeNull();
        statusEintrag!.Content.Should().Contain("Beendet");
    }

    /// <summary>Prüft, dass der konfigurierte Basispfad als Arbeitsverzeichnis verwendet wird.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldUseConfiguredBasePath_WhenWorkdirSettingExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Konfiguriertes Arbeitsverzeichnis");
        var configuredBasePath = Path.Combine(Path.GetTempPath(), $"workdir-config-{Guid.NewGuid():N}");

        var workdirSettings = new WorkingDirectorySettingsService(db.Context, NullLogger<WorkingDirectorySettingsService>.Instance);
        await workdirSettings.SaveWorkingDirectoryAsync(configuredBasePath, TestContext.Current.CancellationToken);

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var expected = Path.Combine(configuredBasePath, "softwareschmiede", workItemId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expected, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (Directory.Exists(configuredBasePath))
        {
            Directory.Delete(configuredBasePath, recursive: true);
        }
    }

    /// <summary>Prüft, dass Temp als Fallback-Arbeitsverzeichnis verwendet wird, wenn keine Konfiguration vorliegt.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldUseTempFallback_WhenNoWorkdirSettingExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Fallback ohne Konfiguration");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var expected = Path.Combine(Path.GetTempPath(), "softwareschmiede", workItemId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Prüft, dass bei ungültigem konfiguriertem Pfad auf Temp zurückgefallen wird.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldFallbackToTemp_WhenConfiguredPathIsUnavailable()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Fallback bei ungültigem Laufzeitpfad");

        var invalidAsFile = Path.Combine(Path.GetTempPath(), $"workdir-file-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(invalidAsFile, "not-a-directory", TestContext.Current.CancellationToken);

        db.Context.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = WorkingDirectorySettingsService.RepositoriesWorkdirKey,
            Value = invalidAsFile,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>());

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        var expectedFallback = Path.Combine(Path.GetTempPath(), "softwareschmiede", workItemId.ToString());
        gitMock.Verify(g => g.CloneRepositoryAsync("https://github.com/test/repo", expectedFallback, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (File.Exists(invalidAsFile))
        {
            File.Delete(invalidAsFile);
        }
    }

    /// <summary>Prüft, dass ein Protokolleintrag angelegt wird, wenn der Resolver auf Temp zurückfällt.</summary>
    [Fact]
    public async Task ProzessStartenAsync_ShouldLogFallbackEntry_WhenResolverUsesFallback()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, workItemId) = await CreateTestDataAsync(db, "Fallback Log");

        var gitMock = new Mock<IGitPlugin>();
        gitMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resolverMock = new Mock<IWorkingDirectoryResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkingDirectoryResolutionResult(Path.GetTempPath(), true, "no-configured-path", null));

        var service = CreateService(db, gitMock, new Mock<IKiPlugin>(), resolverMock.Object);

        // Act
        await service.StartProcessAsync(workItemId, "https://github.com/test/repo", ct: TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var entries = await db2.LogEntries
            .Where(p => p.WorkItemId == workItemId && p.Type == LogEntryType.GitAction)
            .Select(p => p.Content)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        entries.Should().Contain(e => e.Contains("Arbeitsverzeichnis-Fallback aktiv"));
    }

    /// <summary>
    /// Testet, dass <see cref="GitRepository.DefaultSourceBranchName"/> in der Datenbank gespeichert und über
    /// einen neuen DbContext wieder eingelesen werden kann (Basis-Branch-Konfiguration, Migration
    /// AddDefaultSourceBranchNameToGitRepository).
    /// </summary>
    [Fact]
    public async Task DefaultSourceBranchName_ShouldBePersisted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projektService = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var project = await projektService.CreateAsync("Basis-Branch-Persistenz-Projekt", null, TestContext.Current.CancellationToken);
        var repository = await projektService.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/test/persist-basis-branch", "test/persist-basis-branch", ct: TestContext.Current.CancellationToken);

        // Act
        var repositoryEntity = await db.Context.GitRepositories.FindAsync(new object?[] { repository.Id }, TestContext.Current.CancellationToken);
        repositoryEntity!.DefaultSourceBranchName = "staging";
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(new object?[] { repository.Id }, TestContext.Current.CancellationToken);
        loaded!.DefaultSourceBranchName.Should().Be("staging");
    }

    /// <summary>
    /// Testet, dass bestehende <see cref="GitRepository"/>-Einträge ohne explizite Konfiguration
    /// <c>DefaultSourceBranchName = null</c> haben (Abwärtskompatibilität nach der Migration).
    /// </summary>
    [Fact]
    public async Task DefaultSourceBranchName_ShouldBeNull_WhenNotConfigured()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projektService = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var project = await projektService.CreateAsync("Basis-Branch-Default-Projekt", null, TestContext.Current.CancellationToken);
        var repository = await projektService.AddRepositoryAsync(project.Id, "GitHub", "https://github.com/test/default-basis-branch", "test/default-basis-branch", ct: TestContext.Current.CancellationToken);

        // Act
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.GitRepositories.FindAsync(new object?[] { repository.Id }, TestContext.Current.CancellationToken);

        // Assert
        loaded!.DefaultSourceBranchName.Should().BeNull();
    }
}
