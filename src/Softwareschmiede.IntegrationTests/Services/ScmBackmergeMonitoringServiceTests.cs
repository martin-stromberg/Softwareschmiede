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

/// <summary>Backmerge-Automatisierung im eigenen Prüfzyklus (<see cref="ScmBackmergeMonitoringService"/>):
/// enumerierte Pull Requests werden gegen Hauptbranch und Projekt-Basisbranch geprüft und regulär
/// gemergt; Draft-Aktivierung und Admin-Fallback sind jeweils opt-in. Der Dienst läuft unabhängig von
/// der Ereignisüberwachung und immer unter dem Benutzerkonto.</summary>
public sealed class ScmBackmergeMonitoringServiceTests
{
    /// <summary>Global aktivierter Backmerge: ein enumerierter PR main → staging wird zuerst regulär
    /// gemergt; der Admin-Fallback wird ohne gesetzte Option nicht aufgerufen.</summary>
    [Fact]
    public async Task Monitor_BackmergePullRequest_IsMergedRegularly()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));

        await Monitor(fixture).RunOnceAsync();

        VerifyMerge(fixture, allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(fixture, allowProtectedBranchBypass: true, Times.Never());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey.StartsWith("backmerge/"));
        Assert.Equal(long.MaxValue, checkpoint.NextAttemptUtc);
        Assert.Equal("Merged", checkpoint.ErrorCode);
    }

    /// <summary>Deaktivierter Modus: der Prüfzyklus lässt jede Provider-Arbeit aus —
    /// weder Repository-Auflösung noch Enumeration oder Statusabfrage.</summary>
    [Fact]
    public async Task Monitor_BackmergeDisabled_NoProviderWork()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        ConfigureBackmerge(fixture);

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deaktivierte Ereignisüberwachung („Aufgaben ausführen“) hindert die
    /// Backmerge-Automatisierung („Prozessabläufe“) nicht: beide Laufzeit-Schalter sind unabhängig.</summary>
    [Fact]
    public async Task Monitor_EventMonitoringDisabled_BackmergeStillRuns()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false);

        await Monitor(fixture).RunOnceAsync();

        VerifyMerge(fixture, allowProtectedBranchBypass: false, Times.Once());
    }

    /// <summary>Die Ereignisüberwachung ruft keinen Backmerge mehr auf: Ihr Lauf enumeriert die
    /// Referenzen nur für die Ereignisquellen und führt weder Statusabfrage noch Merge aus.</summary>
    [Fact]
    public async Task EventMonitoringCycle_DoesNotTriggerBackmerge()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));

        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.False(await db.ScmObservationCheckpoints.AnyAsync(x => x.SourceKey.StartsWith("backmerge/")));
    }

    /// <summary>Die Repository-Auflösung des Backmerge-Prüfzyklus läuft stets unter dem
    /// Benutzerkonto — auch wenn die Ereignisüberwachung auf „Separates Konto“ konfiguriert ist.</summary>
    [Fact]
    public async Task Monitor_RepositoryResolution_UsesBenutzerkontoContext()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetScmKontoModusAsync(ScmKontoModus.SeparatesKonto);
        var contexts = new List<ScmRepositoryContext>();
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
            {
                contexts.Add(context);
                return Task.FromResult(fixture.Repository);
            });

        await Monitor(fixture).RunOnceAsync();

        var context = Assert.Single(contexts);
        Assert.Equal(ScmKontoModus.Benutzerkonto, context.Konto);
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

    /// <summary>Mit aktivierter Admin-Option wird ein blockierter regulärer Merge in einem zweiten
    /// Versuch mit Protected-Branch-Bypass bestätigt.</summary>
    [Fact]
    public async Task Monitor_AdminMergeEnabled_BlockedRegularMergeFallsBack()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        ConfigureBackmerge(fixture);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetScmBackmergeAdminMergeAsync(true);
        fixture.Git.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.Is<PullRequestCompletionOptions>(o => !o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.BlockedResult("protected branch"));
        fixture.Git.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.Is<PullRequestCompletionOptions>(o => o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await Monitor(fixture).RunOnceAsync();

        VerifyMerge(fixture, allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(fixture, allowProtectedBranchBypass: true, Times.Once());
        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey.StartsWith("backmerge/"));
        Assert.Equal(long.MaxValue, checkpoint.NextAttemptUtc);
    }

    /// <summary>Mit aktivierter Draft-Aktivierung wird ein Draft-Backmerge bereitgestellt, erneut
    /// geprüft und regulär gemergt.</summary>
    [Fact]
    public async Task Monitor_DraftPullRequest_WithActivation_IsReadiedAndMerged()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryWithBasisbranchAsync(fixture);
        await SetModusAsync(fixture, ScmBackmergeModus.Global);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetScmBackmergeDraftAktivierungAsync(true);
        fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5)]);
        fixture.Git.Setup(x => x.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        var abfragen = 0;
        fixture.Git.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++abfragen == 1
                ? StatusInfo(mergeStatus: PullRequestMergeStatus.Blocked, isDraft: true)
                : StatusInfo(mergeStatus: PullRequestMergeStatus.Mergeable));
        fixture.Git.Setup(x => x.MarkPullRequestReadyAsync("owner/repo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("bereit"));
        ConfigureMerge(fixture, PullRequestCompletionResult.Completed("merge-sha"));

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.Verify(x => x.MarkPullRequestReadyAsync("owner/repo", 5, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.Verify(x => x.GetPullRequestStatusAsync("owner/repo", 5, It.IsAny<CancellationToken>()), Times.Exactly(2));
        VerifyMerge(fixture, allowProtectedBranchBypass: false, Times.Once());
    }

    private static ScmBackmergeMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmBackmergeMonitoringService>();

    private static PullRequestStatusInfo StatusInfo(PullRequestStatus status = PullRequestStatus.Open,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable,
        string source = "main", string target = "staging", bool isDraft = false)
        => new(PullRequestProvider.GitHub, "owner/repo", 5, "PR_5",
            "https://github.com/owner/repo/pull/5", "Backmerge", source, target, "sha", null,
            status, mergeStatus, DateTimeOffset.UtcNow, isDraft);

    private static void VerifyMerge(ScmDispatchFixture fixture, bool allowProtectedBranchBypass, Times times)
        => fixture.Git.Verify(x => x.CompletePullRequestAsync("owner/repo", 5,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                && o.MergeMethod == PullRequestMergeMethod.Merge
                && o.AllowProtectedBranchBypass == allowProtectedBranchBypass),
            It.IsAny<CancellationToken>()), times);

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
            .ReturnsAsync(StatusInfo(status, mergeStatus, source, target));
    }

    private static void ConfigureMerge(ScmDispatchFixture fixture, PullRequestCompletionResult result)
        => fixture.Git.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
}
