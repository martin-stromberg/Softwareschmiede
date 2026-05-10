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
    private readonly ProtokollService _protokollService;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly GitOrchestrationService _sut;
    private readonly Guid _projektId = new("55555555-5555-5555-5555-555555555555");

    public GitOrchestrationServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, new Mock<ILogger<AufgabeService>>().Object);
        _protokollService = new ProtokollService(_db, new Mock<ILogger<ProtokollService>>().Object);
        _gitPluginMock = new Mock<IGitPlugin>();
        _sut = new GitOrchestrationService(
            _aufgabeService,
            _protokollService,
            _gitPluginMock.Object,
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

    /// <summary>PullRequestErstellenAsync nutzt die angegebene Repository-ID und protokolliert den PR.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldUseRepositoryOverrideAndLog_WhenRepositoryOverrideProvided()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR Aufgabe", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pr", @"C:\repos\task-4");
        var expectedPr = new PullRequest(42, "PR Titel", "https://example/pr/42", "feature/pr");

        _gitPluginMock
            .Setup(g => g.CreatePullRequestAsync("owner/override", "feature/pr", "Custom title", "Custom body", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPr);

        // Act
        var result = await _sut.PullRequestErstellenAsync(
            aufgabe.Id,
            repositoryIdOverride: "owner/override",
            title: "Custom title",
            body: "Custom body");

        // Assert
        result.Should().Be(expectedPr);
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync("owner/override", "feature/pr", "Custom title", "Custom body", It.IsAny<CancellationToken>()),
            Times.Once);
        var protokoll = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        protokoll.Should().Contain(e => e.Typ == ProtokollTyp.GitAktion && e.Inhalt.Contains("Pull Request erstellt: #42"));
    }

    /// <summary>PullRequestErstellenAsync wirft eine Exception, wenn keine Repository-ID auflösbar ist.</summary>
    [Fact]
    public async Task PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenRepositoryIdIsNotResolvable()
    {
        // Arrange
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR ohne Repo", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/no-repo", @"C:\repos\task-5");

        // Act
        var act = () => _sut.PullRequestErstellenAsync(aufgabe.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*keine Repository-ID angegeben*");
        _gitPluginMock.Verify(
            g => g.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
