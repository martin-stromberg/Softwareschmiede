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

/// <summary>Persistent recovery of unchanged comments after ambiguous aliases are corrected.</summary>
public sealed class ScmEventMonitoringServiceTests_AmbiguityRecovery
{
    /// <summary>Ambiguity survives empty polls and a full service restart without losing the original comment.</summary>
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
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    var project = new Projekt { Id = Guid.NewGuid(), Name = "Alias recovery" };
                    var tasks = new[] { "https://github.com/owner/repo", "git@github.com:owner/repo.git" }
                        .Select(url => new Aufgabe
                        {
                            Id = Guid.NewGuid(),
                            Projekt = project,
                            Titel = url,
                            GitRepository = new GitRepository
                            {
                                Id = Guid.NewGuid(),
                                Projekt = project,
                                RepositoryUrl = url,
                                RepositoryName = "repo",
                                PluginTyp = fixture.Repository.ProviderPrefix
                            },
                            IssueReferenz = new IssueReferenz { Id = Guid.NewGuid(), IssueNummer = 7, Titel = "Issue" }
                        }).ToArray();
                    remainingTask = tasks[0].Id;
                    db.AddRange(tasks);
                    await db.SaveChangesAsync();
                }
                ConfigureSource(fixture, observedAt);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
                    Assert.Contains(await db.ScmObservationCheckpoints.ToListAsync(), x => x.Diagnostic == "Mehrdeutige Zuordnung zu Aufgaben");
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
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    db.IssueReferenzen.Remove(await db.IssueReferenzen.SingleAsync(x => x.AufgabeId != remainingTask));
                    await db.SaveChangesAsync();
                }
                ConfigureSource(fixture, observedAt, includeComment: false);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
                using (var scope = fixture.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                    Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
                    Assert.Contains(await db.ScmObservationCheckpoints.ToListAsync(), x => x.Diagnostic == "Mehrdeutige Zuordnung zu Aufgaben");
                }
                clock.Now = clock.Now.AddDays(1);
                ConfigureSource(fixture, observedAt);
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
                await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
                using var verification = fixture.Services.CreateScope();
                var verifiedDb = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                var item = Assert.Single(await verifiedDb.ScmIncomingEvents.ToListAsync());
                Assert.Equal(remainingTask, item.AufgabeId);
                Assert.Equal("$Softwareschmiede unchanged", ScmEventStore.Observation(item).Body);
                Assert.All(await verifiedDb.ScmObservationCheckpoints.ToListAsync(), x => Assert.Null(x.Diagnostic));
                Assert.All(await verifiedDb.ScmContentSnapshots.ToListAsync(), x => Assert.Null(x.Diagnostic));
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
