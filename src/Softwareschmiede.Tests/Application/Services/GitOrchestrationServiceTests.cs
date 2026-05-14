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

/// <summary>Tests für den GitOrchestrationService.</summary>
public sealed class GitOrchestrationServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;
    private readonly ProtokollService _protokollService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly GitOrchestrationService _sut;
    private readonly Guid _projektId = new("55555555-5555-5555-5555-555555555555");

    public GitOrchestrationServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _projektService = new ProjektService(_db, new Mock<ILogger<ProjektService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);
        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.SetupGet(plugin => plugin.PluginName).Returns("Mock Git");
        _gitPluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns("Mock.Git");
        _gitPluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        _gitPluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginMock.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);
        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            new Mock<ILogger<PluginSelectionService>>().Object);
        _sut = new GitOrchestrationService(
            _aufgabeService,
            _projektService,
            _protokollService,
            _gitPluginMock.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Git-Orchestration-Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>IssuesAbrufenAsync liefert die vom Plugin gelieferten Issues.</summary>
    [Fact]
    public async Task IssuesAbrufenAsync_ShouldReturnIssues_WhenPluginProvidesData()
    {
        // Arrange
        var expectedIssues = new[]
        {
            new Issue(11, "Issue A", "Body A", ["bug"], "m1", "https://example/issues/11"),
            new Issue(12, "Issue B", null, ["feature"], null, "https://example/issues/12")
        };
        _gitPluginMock
            .Setup(g => g.GetIssuesAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedIssues);

        // Act
        var result = (await _sut.IssuesAbrufenAsync("owner/repo")).ToList();

        // Assert
        result.Should().BeEquivalentTo(expectedIssues);
        _gitPluginMock.Verify(g => g.GetIssuesAsync("owner/repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CommitAsync führt Commit aus und schreibt einen Git-Protokolleintrag.</summary>
    [Fact]
    public async Task CommitAsync_ShouldCommitAndAddLogEntry_WhenAufgabeHasKlonpfad()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Commit Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/commit", @"C:\repos\task-1");

        // Act
        await _sut.CommitAsync(aufgabe.Id, "feat: add tests");

        // Assert
        _gitPluginMock.Verify(g => g.CommitAsync(@"C:\repos\task-1", "feat: add tests", It.IsAny<CancellationToken>()), Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Commit: feat: add tests"));
    }

    [Fact]
    public async Task CommitAsync_ShouldUseSelectedPlugin_WhenTaskRepositoryContainsTrimmedLowercasePluginType()
    {
        var defaultPluginMock = CreateGitPluginMock("Default Git", "Softwareschmiede.GitHub");
        var selectedPluginMock = CreateGitPluginMock("Local Directory", "LocalDirectoryPlugin");
        var sut = CreateSutWithPlugins(defaultPluginMock, selectedPluginMock);

        var projekt = await _projektService.CreateAsync("Projekt mit explizitem Plugin", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            @"C:\repos\source",
            "source");
        var repositoryEntity = await _db.GitRepositories.FindAsync(repository.Id);
        repositoryEntity!.PluginTyp = "  localdirectoryplugin  ";
        await _db.SaveChangesAsync();

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Commit mit Pluginauswahl", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/selection", @"C:\repos\task-selection");

        await sut.CommitAsync(aufgabe.Id, "feat: selected plugin");

        selectedPluginMock.Verify(
            plugin => plugin.CommitAsync(@"C:\repos\task-selection", "feat: selected plugin", It.IsAny<CancellationToken>()),
            Times.Once);
        defaultPluginMock.Verify(
            plugin => plugin.CommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>ResetAsync protokolliert HEAD als Ziel, wenn keine Target-Ref angegeben wird.</summary>
    [Fact]
    public async Task ResetAsync_ShouldLogHead_WhenTargetRefIsNull()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Reset Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/reset", @"C:\repos\task-2");

        // Act
        await _sut.ResetAsync(aufgabe.Id, "hard", null);

        // Assert
        _gitPluginMock.Verify(g => g.ResetAsync(@"C:\repos\task-2", "hard", null, It.IsAny<CancellationToken>()), Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Reset (hard) auf HEAD"));
    }

    /// <summary>PushAsync wirft eine Exception, wenn kein BranchName vorhanden ist.</summary>
    [Fact]
    public async Task PushAsync_ShouldThrowInvalidOperationException_WhenBranchNameIsMissing()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Push Aufgabe", null);
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        entity!.LokalerKlonPfad = @"C:\repos\task-3";
        entity.BranchName = null;
        await _db.SaveChangesAsync();

        // Act
        var act = () => _sut.PushAsync(aufgabe.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Branch-Namen*");
        _gitPluginMock.Verify(g => g.PushBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PullAsync_ShouldLogNoMergeHint_WhenLocalDirectoryPluginIsUsed()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Pull Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pull", @"C:\repos\task-pull");
        _gitPluginMock.SetupGet(g => g.PluginPrefix).Returns("LocalDirectoryPlugin");

        await _sut.PullAsync(aufgabe.Id);

        _gitPluginMock.Verify(g => g.PullAsync(@"C:\repos\task-pull", It.IsAny<CancellationToken>()), Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Kein Merge"));
    }

    [Fact]
    public async Task PullAsync_ShouldLogRemotePullText_WhenPluginIsNotLocalDirectoryPlugin()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Pull Aufgabe Remote", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pull-remote", @"C:\repos\task-pull-remote");
        _gitPluginMock.SetupGet(g => g.PluginPrefix).Returns("GitHubPlugin");

        await _sut.PullAsync(aufgabe.Id);

        _gitPluginMock.Verify(g => g.PullAsync(@"C:\repos\task-pull-remote", It.IsAny<CancellationToken>()), Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Änderungen vom Remote geholt"));
        protokoll.Should().NotContain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Kein Merge"));
    }

    [Fact]
    public async Task PullAsync_ShouldUseLocalDirectoryPlugin_WhenTaskHasNoLinkedRepositoryAndProjectHasSingleActiveRepository()
    {
        var defaultPluginMock = CreateGitPluginMock("Default Git", "Softwareschmiede.GitHub");
        var localPluginMock = CreateGitPluginMock("Local Directory", "LocalDirectoryPlugin");
        var sut = CreateSutWithPlugins(defaultPluginMock, localPluginMock);

        var projekt = await _projektService.CreateAsync("Projekt mit lokalem Repository", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            @"C:\repos\project-source",
            "source");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Pull ohne Repo-Verknüpfung", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/local-pull", @"C:\repos\task-local-pull");

        await sut.PullAsync(aufgabe.Id);

        localPluginMock.Verify(
            plugin => plugin.PullAsync(@"C:\repos\task-local-pull", It.IsAny<CancellationToken>()),
            Times.Once);
        defaultPluginMock.Verify(
            plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Kein Merge"));
    }

    [Fact]
    public async Task CommitAsync_ShouldFallbackToDefaultPlugin_WhenSelectedPluginTypeCannotBeResolved()
    {
        var defaultPluginMock = CreateGitPluginMock("Default Git", "Softwareschmiede.GitHub");
        var selectedPluginMock = CreateGitPluginMock("Local Directory", "LocalDirectoryPlugin");
        var sut = CreateSutWithPlugins(defaultPluginMock, selectedPluginMock);

        var projekt = await _projektService.CreateAsync("Projekt mit ungültigem Plugin", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Unknown.Plugin",
            "https://example.com/repo.git",
            "repo");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Commit mit ungültigem Plugin", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/fallback", @"C:\repos\task-fallback");

        await sut.CommitAsync(aufgabe.Id, "feat: fallback default");

        defaultPluginMock.Verify(
            plugin => plugin.CommitAsync(@"C:\repos\task-fallback", "feat: fallback default", It.IsAny<CancellationToken>()),
            Times.Once);
        selectedPluginMock.Verify(
            plugin => plugin.CommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommitAsync_ShouldUseDefaultPlugin_WhenProjectRepositorySelectionIsAmbiguous()
    {
        var defaultPluginMock = CreateGitPluginMock("Default Git", "Softwareschmiede.GitHub");
        var selectedPluginMock = CreateGitPluginMock("Local Directory", "LocalDirectoryPlugin");
        var sut = CreateSutWithPlugins(defaultPluginMock, selectedPluginMock);

        var projekt = await _projektService.CreateAsync("Projekt mit mehreren Repositories", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "LocalDirectoryPlugin",
            @"C:\repos\source-a",
            "source-a");
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/example/repo-b",
            "repo-b");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Commit ohne Repo-Verknüpfung", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/ambiguous-selection", @"C:\repos\task-ambiguous");

        await sut.CommitAsync(aufgabe.Id, "feat: default because ambiguous");

        defaultPluginMock.Verify(
            plugin => plugin.CommitAsync(@"C:\repos\task-ambiguous", "feat: default because ambiguous", It.IsAny<CancellationToken>()),
            Times.Once);
        selectedPluginMock.Verify(
            plugin => plugin.CommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MergeToSourceAsync_ShouldCallPluginAndWriteLogEntry()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Merge Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/merge", @"C:\repos\task-merge");

        await _sut.MergeToSourceAsync(aufgabe.Id);

        _gitPluginMock.Verify(g => g.MergeToSourceAsync(@"C:\repos\task-merge", It.IsAny<CancellationToken>()), Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("ins Quellverzeichnis übernommen"));
    }

    /// <summary>MergeToSourceAsync bricht ab, wenn kein lokaler Klonpfad gesetzt ist.</summary>
    [Fact]
    public async Task MergeToSourceAsync_ShouldThrowInvalidOperationException_WhenLocalPathIsMissing()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Merge ohne Pfad", null);

        var act = () => _sut.MergeToSourceAsync(aufgabe.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*keinen lokalen Klonpfad*");
        _gitPluginMock.Verify(g => g.MergeToSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldReturnPluginCapabilities()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Capabilities Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/caps", @"C:\repos\task-caps");
        var expected = new GitActionCapabilities(
            RepositoryKind.LocalDirectory,
            IsWorkingDirectoryCopy: true,
            CanPush: false,
            CanPull: false,
            CanCreatePullRequest: false,
            CanMergeToSource: true);

        _gitPluginMock
            .Setup(g => g.GetGitActionCapabilitiesAsync(@"C:\repos\task-caps", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetGitActionCapabilitiesAsync(aufgabe.Id);

        result.Should().Be(expected);
        _gitPluginMock.Verify(g => g.GetGitActionCapabilitiesAsync(@"C:\repos\task-caps", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>GetGitActionCapabilitiesAsync bricht kontrolliert ab, wenn die Aufgabe nicht existiert.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldThrowInvalidOperationException_WhenTaskIsMissing()
    {
        var act = () => _sut.GetGitActionCapabilitiesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht gefunden*");
        _gitPluginMock.Verify(g => g.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>GetGitActionCapabilitiesAsync gibt einen fehlenden lokalen Pfad unverändert an das Plugin weiter.</summary>
    [Fact]
    public async Task GetGitActionCapabilitiesAsync_ShouldForwardNullLocalPath_WhenTaskHasNoLocalPath()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Capabilities ohne Pfad", null);
        var expected = new GitActionCapabilities(
            RepositoryKind.RemoteGit,
            IsWorkingDirectoryCopy: false,
            CanPush: true,
            CanPull: true,
            CanCreatePullRequest: true,
            CanMergeToSource: false);

        _gitPluginMock
            .Setup(g => g.GetGitActionCapabilitiesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetGitActionCapabilitiesAsync(aufgabe.Id);

        result.Should().Be(expected);
        _gitPluginMock.Verify(g => g.GetGitActionCapabilitiesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullRequestErstellenAsync nutzt das Repository der Aufgabe und protokolliert den PR.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldUseTaskRepositoryAndLog_WhenAufgabeHasLinkedRepository()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Git-Orchestration-Testprojekt", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/repo",
            "test/repo");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR Aufgabe", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pr", @"C:\repos\task-4");
        var expectedPr = new PullRequest(42, "PR Titel", "https://example/pr/42", "feature/pr");

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync("test/repo", "feature/pr", "Custom title", "Custom body", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPr);

        // Act
        var result = await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Custom title", body: "Custom body");

        // Assert
        result.Should().Be(expectedPr);
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync("test/repo", "feature/pr", "Custom title", "Custom body", It.IsAny<CancellationToken>()),
            Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Pull Request erstellt: #42"));
    }

    /// <summary>PullRequestErstellenAsync nutzt das aktive Projekt-Repository, wenn die Aufgabe keines zugewiesen hat.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldUseProjectRepository_WhenAufgabeHasNoLinkedRepository()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt mit Repository", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/project-repo",
            "test/project-repo");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR ohne Repo", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/no-repo", @"C:\repos\task-5");
        var expectedPr = new PullRequest(43, "PR Titel", "https://example/pr/43", "feature/no-repo");

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync("test/project-repo", "feature/no-repo", "Titel aus Aufgabe", "Body aus Aufgabe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPr);

        // Act
        var result = await _sut.PullRequestErstellenAsync(
            aufgabe.Id,
            title: "Titel aus Aufgabe",
            body: "Body aus Aufgabe");

        // Assert
        result.Should().Be(expectedPr);
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync("test/project-repo", "feature/no-repo", "Titel aus Aufgabe", "Body aus Aufgabe", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PullRequestErstellenAsync_ShouldAppendClosingDirectiveAndLogIssue_WhenAufgabeHasIssueReference()
    {
        var projekt = await _projektService.CreateAsync("Projekt mit Issue", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/issue-repo",
            "test/issue-repo");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR mit Issue", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/issue", @"C:\repos\task-issue");
        _db.IssueReferenzen.Add(new IssueReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            IssueNummer = 123,
            Titel = "Issue 123",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync();

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "test/issue-repo",
                "feature/issue",
                "Titel",
                It.Is<string>(body => body == $"Body{Environment.NewLine}{Environment.NewLine}Closes #123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(77, "PR", "https://example/pr/77", "feature/issue"));

        await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: "Body");

        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "test/issue-repo",
            "feature/issue",
            "Titel",
            It.Is<string>(body => body == $"Body{Environment.NewLine}{Environment.NewLine}Closes #123"),
            It.IsAny<CancellationToken>()), Times.Once);

        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Issue #123"));
    }

    [Fact]
    public async Task PullRequestErstellenAsync_ShouldNotDuplicateClosingDirective_WhenBodyAlreadyContainsDirective()
    {
        var projekt = await _projektService.CreateAsync("Projekt mit bestehender Direktive", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/existing-directive",
            "test/existing-directive");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR ohne Duplikat", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/no-dup", @"C:\repos\task-no-dup");
        _db.IssueReferenzen.Add(new IssueReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            IssueNummer = 88,
            Titel = "Issue 88",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync();

        const string existingBody = "Implementierung abgeschlossen.\nFixes #88";
        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "test/existing-directive",
                "feature/no-dup",
                "Titel",
                existingBody,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(78, "PR", "https://example/pr/78", "feature/no-dup"));

        await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: existingBody);

        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "test/existing-directive",
            "feature/no-dup",
            "Titel",
            existingBody,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullRequestErstellenAsync ersetzt einen reinen Whitespace-Body bei bestehender Issue-Referenz durch die Closing-Direktive.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldUseOnlyClosingDirective_WhenBodyIsWhitespaceAndIssueExists()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt mit Whitespace-Body", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/whitespace-body",
            "test/whitespace-body");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR mit Whitespace-Body", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/whitespace", @"C:\repos\task-whitespace");
        _db.IssueReferenzen.Add(new IssueReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            IssueNummer = 501,
            Titel = "Issue 501",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync();

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "test/whitespace-body",
                "feature/whitespace",
                "Titel",
                "Closes #501",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(79, "PR", "https://example/pr/79", "feature/whitespace"));

        // Act
        await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: "   ");

        // Assert
        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "test/whitespace-body",
            "feature/whitespace",
            "Titel",
            "Closes #501",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullRequestErstellenAsync ergänzt die Closing-Direktive der aktuellen Issue, wenn bereits eine Direktive für eine andere Issue vorhanden ist.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldAppendClosingDirectiveForCurrentIssue_WhenBodyContainsDirectiveForAnotherIssue()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt mit anderer Direktive", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/other-directive",
            "test/other-directive");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR mit anderer Direktive", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/other-directive", @"C:\repos\task-other-directive");
        _db.IssueReferenzen.Add(new IssueReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            IssueNummer = 123,
            Titel = "Issue 123",
            LabelsJson = "[]"
        });
        await _db.SaveChangesAsync();

        const string existingBody = "Implementierung abgeschlossen.\nCloses #41";
        var expectedBody = $"{existingBody.TrimEnd()}{Environment.NewLine}{Environment.NewLine}Closes #123";
        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "test/other-directive",
                "feature/other-directive",
                "Titel",
                expectedBody,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(80, "PR", "https://example/pr/80", "feature/other-directive"));

        // Act
        await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: existingBody);

        // Assert
        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "test/other-directive",
            "feature/other-directive",
            "Titel",
            expectedBody,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>PullRequestErstellenAsync bricht bei mehrdeutiger Repository-Zuordnung kontrolliert ab.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenMultipleActiveRepositoriesExist()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt mit mehreren Repositories", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/repo-a",
            "test/repo-a");
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "https://github.com/test/repo-b",
            "test/repo-b");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR mehrdeutig", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/ambiguous", @"C:\repos\task-6");

        // Act
        var act = () => _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: "Body");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mehrere aktive Repositories*");
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>PullRequestErstellenAsync bricht kontrolliert ab, wenn im Projekt kein aktives Repository vorhanden ist.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenNoActiveRepositoryExists()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt ohne Repository", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR ohne Repository", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/no-active-repo", @"C:\repos\task-no-active-repo");

        // Act
        var act = () => _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: "Body");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kein aktives Repository*");
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>PullRequestErstellenAsync löst die Repository-ID korrekt aus einer SSH-Repository-URL auf.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldResolveRepositoryId_FromSshRepositoryUrl()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Projekt mit SSH-Repository", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "GitHub",
            "git@github.com:owner/repo.git",
            "owner/repo");

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "PR mit SSH-URL", null, repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/ssh-url", @"C:\repos\task-ssh-url");
        var expectedPr = new PullRequest(81, "SSH PR", "https://example/pr/81", "feature/ssh-url");

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync(
                "owner/repo",
                "feature/ssh-url",
                "Titel",
                "Body",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPr);

        // Act
        var result = await _sut.PullRequestErstellenAsync(aufgabe.Id, title: "Titel", body: "Body");

        // Assert
        result.Should().Be(expectedPr);
        _gitPluginMock.Verify(g => g.CreatePullRequestAsync(
            "owner/repo",
            "feature/ssh-url",
            "Titel",
            "Body",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private GitOrchestrationService CreateSutWithPlugins(Mock<IGitPlugin> defaultPlugin, params Mock<IGitPlugin>[] additionalPlugins)
    {
        var plugins = new List<IGitPlugin> { defaultPlugin.Object };
        plugins.AddRange(additionalPlugins.Select(plugin => plugin.Object));

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(manager => manager.GetSourceCodeManagementPlugins()).Returns(plugins);
        pluginManagerMock.Setup(manager => manager.GetDefaultSourceCodeManagementPlugin()).Returns(defaultPlugin.Object);
        pluginManagerMock.Setup(manager => manager.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(manager => manager.GetDefaultDevelopmentAutomationPlugin()).Returns(new Mock<IKiPlugin>().Object);

        var pluginDefaultSettings = new PluginDefaultSettingsService(_db, new Mock<ILogger<PluginDefaultSettingsService>>().Object);
        var pluginSelectionService = new PluginSelectionService(
            pluginManagerMock.Object,
            pluginDefaultSettings,
            new Mock<ILogger<PluginSelectionService>>().Object);

        return new GitOrchestrationService(
            _aufgabeService,
            _projektService,
            _protokollService,
            defaultPlugin.Object,
            pluginSelectionService,
            new Mock<ILogger<GitOrchestrationService>>().Object);
    }

    private static Mock<IGitPlugin> CreateGitPluginMock(string pluginName, string pluginPrefix)
    {
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(plugin => plugin.PluginName).Returns(pluginName);
        pluginMock.SetupGet(plugin => plugin.PluginPrefix).Returns(pluginPrefix);
        pluginMock.SetupGet(plugin => plugin.PluginType).Returns(PluginType.SourceCodeManagement);
        pluginMock.Setup(plugin => plugin.GetSettingGroups()).Returns([]);
        pluginMock.Setup(plugin => plugin.CommitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pluginMock.Setup(plugin => plugin.PullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pluginMock.Setup(plugin => plugin.ResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pluginMock.Setup(plugin => plugin.PushBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pluginMock.Setup(plugin => plugin.MergeToSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pluginMock.Setup(plugin => plugin.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        pluginMock.Setup(plugin => plugin.CreatePullRequestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(1, "PR", "https://example/pr/1", "feature/test"));
        pluginMock.Setup(plugin => plugin.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.RemoteGit,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: true,
                CanMergeToSource: false));
        return pluginMock;
    }
}
