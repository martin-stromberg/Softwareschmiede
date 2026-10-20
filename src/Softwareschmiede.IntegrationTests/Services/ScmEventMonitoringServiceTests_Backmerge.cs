using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Backmerge-Erkennung im regulären SCM-Prüflauf: enumerierte Pull Requests werden
/// gegen Hauptbranch und Projekt-Basisbranch geprüft und ggf. per Admin-Merge bestätigt.</summary>
public sealed class ScmEventMonitoringServiceTests_Backmerge
{
    /// <summary>Global aktivierter Backmerge: ein enumerierter PR main → staging wird gemergt.</summary>
    [Fact]
    public async Task Monitor_BackmergePullRequest_IsMergedAutomatically()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.CompletePullRequestAsync("owner/repo", 5,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                && o.MergeMethod == PullRequestMergeMethod.Merge && o.AllowProtectedBranchBypass),
            It.IsAny<CancellationToken>()), Times.Once);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey.StartsWith("backmerge/"));
        Assert.Equal(long.MaxValue, checkpoint.NextAttemptUtc);
        Assert.Equal("Merged", checkpoint.ErrorCode);
    }

    /// <summary>Deaktivierter Modus: der enumerierte PR wird nicht beim Provider nachgefragt.</summary>
    [Fact]
    public async Task Monitor_BackmergeDisabled_NoStatusQueryNoMerge()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        ConfigureBackmerge(fixture);

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Projektbezogener Modus mit gesetzter Projekt-Freigabe: Merge läuft durch.</summary>
    [Fact]
    public async Task Monitor_ProjectScopedModeWithFlag_Merges()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var projektId = await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Projektbezogen);
        await SetProjektFlagAsync(fixture, projektId, true);
        ConfigureBackmerge(fixture);
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein normaler Feature-PR (feature → staging) wird nicht gemergt.</summary>
    [Fact]
    public async Task Monitor_NonBackmergePullRequest_IsNotMerged()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureStatus(fixture, source: "feature/work", target: "staging");
        fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5)]);

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey.StartsWith("backmerge/"));
        Assert.Equal(long.MaxValue, checkpoint.NextAttemptUtc);
        Assert.Contains("Kein Backmerge", checkpoint.Diagnostic);
    }

    /// <summary>Ein nicht mergebarer Backmerge bleibt offen und wird im nächsten Intervall erneut geprüft.</summary>
    [Fact]
    public async Task Monitor_UnmergeableBackmerge_RetriesNextPoll()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture, mergeStatus: PullRequestMergeStatus.Blocked);

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey.StartsWith("backmerge/"));
        Assert.True(checkpoint.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.True(checkpoint.NextAttemptUtc < long.MaxValue);
        Assert.Equal("WaitingForMergeable", checkpoint.ErrorCode);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task<Guid> AddRepositoryWithBasisbranchAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Backmerge" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginTyp = fixture.Repository.ProviderPrefix,
            Aktiv = true,
            DefaultSourceBranchName = "staging"
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static async Task SetModusAsync(ScmDispatchFixture fixture, ScmBackmergeModus modus)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetScmBackmergeModusAsync(modus);
    }

    private static async Task SetProjektFlagAsync(ScmDispatchFixture fixture, Guid projektId, bool aktiv)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetBackmergeProjektAktivAsync(projektId, aktiv);
    }

    /// <summary>Konfiguriert Enumeration, Hauptbranch und PR-Status als mergebaren Backmerge (main → staging).</summary>
    private static void ConfigureBackmerge(ScmDispatchFixture fixture,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable)
    {
        fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5)]);
        ConfigureStatus(fixture, source: "main", target: "staging", mergeStatus: mergeStatus);
    }

    private static void ConfigureStatus(ScmDispatchFixture fixture, string source, string target,
        PullRequestStatus status = PullRequestStatus.Open,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable)
    {
        fixture.Git.Setup(x => x.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        fixture.Git.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestStatusInfo(PullRequestProvider.GitHub, "owner/repo", 5, "PR_5",
                "https://github.com/owner/repo/pull/5", "Backmerge", source, target, "sha", null,
                status, mergeStatus, DateTimeOffset.UtcNow));
    }

    private static void ConfigureMerge(ScmDispatchFixture fixture, PullRequestCompletionResult result)
        => fixture.Git.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
}
