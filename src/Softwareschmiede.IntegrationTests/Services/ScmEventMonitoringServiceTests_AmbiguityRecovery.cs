using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Dauerhafte Wiederherstellung unveränderter Kommentare, nachdem mehrdeutige Aliase korrigiert wurden.</summary>
public sealed class ScmEventMonitoringServiceTests_AmbiguityRecovery
{
    /// <summary>Mehrdeutigkeit übersteht leere Polls und einen kompletten Dienst-Neustart, ohne den ursprünglichen Kommentar zu verlieren.</summary>
    [Fact]
    public async Task Monitor_AliasRemovedAfterRestartAndPollWindow_AcceptsUnchangedCommentExactlyOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "scm-ambiguity-" + Guid.NewGuid().ToString("N"));
        var clock = new PollClock();
        var observedAt = clock.GetUtcNow();
        Guid remainingTask;
        try
        {
            await using (var fixture = new ScmDispatchFixture(root, clock))
            {
                await fixture.InitializeAsync();
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                    var project = new Project { Id = Guid.NewGuid(), Name = "Alias recovery" };
                    var tasks = new[] { "https://github.com/owner/repo", "git@github.com:owner/repo.git" }
                        .Select(url => new WorkItem
                        {
                            Id = Guid.NewGuid(),
                            Project = project,
                            Title = url,
                            GitRepository = new GitRepository
                            {
                                Id = Guid.NewGuid(),
                                Project = project,
                                RepositoryUrl = url,
                                RepositoryName = "repo",
                                PluginType = fixture.Repository.ProviderPrefix
                            },
                            IssueReference = new IssueReference { Id = Guid.NewGuid(), IssueNumber = 7, Title = "Issue" }
                        }).ToArray();
                    remainingTask = tasks[0].Id;
                    db.AddRange(tasks);
                    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
                ConfigureSource(fixture, observedAt);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                    Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
                    Assert.Contains(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => x.Diagnostic == "Mehrdeutige Zuordnung zu Aufgaben");
                }
                Assert.Empty(fixture.Prompts);
                Assert.Empty(fixture.Sessions);
                Assert.Empty(fixture.Responses);
            }
            clock.Now = observedAt.AddDays(1);
            await using (var fixture = new ScmDispatchFixture(root, clock))
            {
                await fixture.InitializeAsync();
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                    db.IssueReferences.Remove(await db.IssueReferences.SingleAsync(x => x.WorkItemId != remainingTask, cancellationToken: TestContext.Current.CancellationToken));
                    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
                ConfigureSource(fixture, observedAt, includeComment: false);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                    Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
                    Assert.Contains(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => x.Diagnostic == "Mehrdeutige Zuordnung zu Aufgaben");
                }
                clock.Now = clock.Now.AddDays(1);
                ConfigureSource(fixture, observedAt);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
                using var verification = fixture.Services.CreateScope();
                var verifiedDb = verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
                var item = Assert.Single(await verifiedDb.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal(remainingTask, item.WorkItemId);
                Assert.Equal("$Softwareschmiede unchanged", ScmEventStore.Observation(item).Body);
                Assert.All(await verifiedDb.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => Assert.Null(x.Diagnostic));
                Assert.All(await verifiedDb.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => Assert.Null(x.Diagnostic));
                Assert.Equal(2, fixture.Responses.Count);
                Assert.Empty(fixture.Prompts);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void ConfigureSource(ScmDispatchFixture fixture, DateTimeOffset observedAt, bool includeComment = true)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                includeComment && request.ContentKind == ScmContentKind.IssueComment && (request.SinceUtc is null || request.SinceUtc <= observedAt)
                    ? [new ScmEventObservation(request.Reference, request.ContentKind, "original-comment", "42", observedAt, observedAt, null,
                        "U_1", "owner", null, null, "$Softwareschmiede unchanged", "https://github.com/owner/repo/issues/7", observedAt)] : [])));

    private sealed class PollClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
