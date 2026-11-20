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

/// <summary>Cross-scope queue fairness and competing manual/SCM setup regression.</summary>
public sealed class ScmEventDispatchTests
{
    /// <summary>Canonical repository identity, not transport URL or plugin display name, decides ambiguity.</summary>
    [Theory]
    [InlineData(ScmReferenceKind.Issue, "git@github.com:owner/repo.git")]
    [InlineData(ScmReferenceKind.Issue, "ssh://git@github.com/owner/repo.git")]
    [InlineData(ScmReferenceKind.PullRequest, "git@github.com:owner/repo.git")]
    [InlineData(ScmReferenceKind.PullRequest, "owner/repo")]
    public async Task Monitor_RepositoryAliasesForDifferentTasks_ReportAmbiguityWithoutDispatch(ScmReferenceKind kind, string alias)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddAliasLinksAsync(fixture, kind, alias);
        ConfigureObservations(fixture);

        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), task => Assert.Equal(WorkItemStatus.New, task.Status));
        var checkpoints = await db.ScmObservationCheckpoints.Where(x => !x.SourceKey.StartsWith("enumeration/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(kind == ScmReferenceKind.Issue ? 2 : 4, checkpoints.Count);
        Assert.All(checkpoints, checkpoint => Assert.Equal("Mehrdeutige Zuordnung zu Aufgaben", checkpoint.Diagnostic));
        Assert.All(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), snapshot => Assert.Equal("Mehrdeutige Zuordnung zu Aufgaben", snapshot.Diagnostic));
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Different issue numbers in one canonical repository remain independently assignable.</summary>
    [Fact]
    public async Task Monitor_RepositoryAliasesWithDifferentReferences_AssignBothTasks()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddAliasLinksAsync(fixture, ScmReferenceKind.Issue, "git@github.com:owner/repo.git", secondNumber: 8);
        ConfigureObservations(fixture);

        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var events = await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var references = await db.WorkItems.Include(x => x.IssueReference).ToDictionaryAsync(x => x.Id, x => x.IssueReference!.IssueNumber, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.All(events, item => Assert.Equal(references[item.WorkItemId], ScmEventStore.Observation(item).Reference.Number));
        Assert.All(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), checkpoint => Assert.Null(checkpoint.Diagnostic));
    }

    /// <summary>A failed alias resolution cannot make another alias appear uniquely assigned; retry is bounded.</summary>
    [Fact]
    public async Task Monitor_UnresolvedAlias_BlocksPartialAssignmentUntilResolutionRecovers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        const string alias = "git@github.com:owner/repo.git";
        await AddAliasLinksAsync(fixture, ScmReferenceKind.Issue, alias);
        ConfigureObservations(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
            It.Is<ScmRepositoryContext>(context => context.RepositoryId == alias), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Authentication, "Zugang erneuern"));
        var monitor = fixture.Services.GetRequiredService<ScmEventMonitoringService>();
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.Is<ScmRepositoryContext>(context => context.RepositoryId == alias), It.IsAny<CancellationToken>()), Times.Once);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints.Where(x => !x.SourceKey.StartsWith("enumeration/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("Authentication", checkpoint.ErrorCode);
            Assert.Equal(long.MaxValue, checkpoint.NextAttemptUtc);
            checkpoint.NextAttemptUtc = null;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
            It.Is<ScmRepositoryContext>(context => context.RepositoryId == alias), It.IsAny<CancellationToken>())).ReturnsAsync(fixture.Repository);
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        using var verification = fixture.Services.CreateScope();
        var verifiedDb = verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await verifiedDb.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(await verifiedDb.ScmObservationCheckpoints.Where(x => !x.SourceKey.StartsWith("enumeration/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            checkpoint => Assert.Equal("Mehrdeutige Zuordnung zu Aufgaben", checkpoint.Diagnostic));
    }

    /// <summary>Older waiting tasks do not starve event 101 for a different start-ready task.</summary>
    [Fact]
    public async Task Monitor_ProcessesPastOneHundredWaitingEvents()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, firstId) = await fixture.AddAsync(WorkItemStatus.Completed, cloned: false);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var first = await db.ScmIncomingEvents.AsNoTracking().SingleAsync(x => x.Id == firstId, cancellationToken: TestContext.Current.CancellationToken);
            for (var index = 1; index < 100; index++)
                db.ScmIncomingEvents.Add(new ScmIncomingEvent
                {
                    EventKey = "older-" + index,
                    WorkItemId = first.WorkItemId,
                    ObservationJson = first.ObservationJson,
                    ReceivedUtc = first.ReceivedUtc
                });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var (_, readyId) = await fixture.AddAsync(configured: true);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
        var item = await fixture.EventAsync(readyId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
        Assert.Equal(101, fixture.Responses.Count(x => x.Kind == Domain.ValueObjects.ScmResponseKind.Reaction));
    }

    /// <summary>A stored github-actions observation passes dispatch revalidation via the trust
    /// list and is delivered like an owner-authored event.</summary>
    [Fact]
    public async Task Dispatch_GitHubActionsAuthoredObservation_Delivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true, authorId: "U_gha", authorLogin: "github-actions[bot]");

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>The second start waits before clone preparation and reuses the first run and its files.</summary>
    [Fact]
    public async Task ConcurrentDispatchAndManualStart_PreserveWorkspaceAndStartOneProcess()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true, cloned: false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.CloneGate = async () => { entered.TrySetResult(); await release.Task; };
        var automatic = fixture.DispatchAsync(eventId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var manualScope = fixture.Services.CreateScope();
        var manual = manualScope.ServiceProvider.GetRequiredService<EntwicklungsprozessService>()
            .ProzessStartenUndCliStartenAsync(taskId, "https://github.com/owner/repo", null, ScmDispatchFixture.KiPrefix, TestContext.Current.CancellationToken);
        Assert.False(manual.IsCompleted);
        release.TrySetResult();
        await Task.WhenAll(automatic, manual).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        using var verification = fixture.Services.CreateScope();
        var task = await verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken);
        Assert.Equal("preserve workspace", await File.ReadAllTextAsync(Path.Combine(task!.LocalClonePath!, "owned.txt"), TestContext.Current.CancellationToken));
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        fixture.Git.Verify(x => x.CloneRepositoryAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>()), Moq.Times.Once);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
    }

    private static async Task AddAliasLinksAsync(ScmDispatchFixture fixture, ScmReferenceKind kind, string alias, int secondNumber = 7)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Repository aliases" };
        foreach (var (address, plugin, number) in new[] { ("https://github.com/owner/repo.git", fixture.Repository.ProviderPrefix, 7), (alias, "Test SCM", secondNumber) })
        {
            var repository = new GitRepository { Id = Guid.NewGuid(), Project = project, RepositoryUrl = address, RepositoryName = "repo", PluginType = plugin };
            var task = new WorkItem { Id = Guid.NewGuid(), Project = project, GitRepository = repository, Title = "Alias " + address, Status = WorkItemStatus.New };
            if (kind == ScmReferenceKind.Issue)
                task.IssueReference = new IssueReference { Id = Guid.NewGuid(), WorkItem = task, IssueNumber = number, Title = "Issue", IssueUrl = "https://github.com/owner/repo/issues/" + number };
            else
                task.PullRequests.Add(new PullRequestReference
                {
                    Id = Guid.NewGuid(),
                    WorkItem = task,
                    Provider = PullRequestProvider.GitHub,
                    RepositoryId = address,
                    PullRequestNumber = number,
                    Title = "PR",
                    Url = "https://github.com/owner/repo/pull/" + number,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    MonitoringPhase = PullRequestMonitoringPhase.NotMonitored
                });
            db.Add(task);
        }
        await db.SaveChangesAsync();
    }

    private static void ConfigureObservations(ScmDispatchFixture fixture)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind, "C_" + request.Reference.Number, "42",
                    time, time, null, "U_1", "owner", null, null, "$Softwareschmiede test", "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }
}
