using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Der explizite Benutzerabbruch ist ein atomarer Endstatus, den auch laufende Versuche respektieren.</summary>
public sealed class ScmEventCancelTests
{
    /// <summary>A cancelled waiting event never dispatches, activates or answers again.</summary>
    [Fact]
    public async Task Cancel_WaitingEvent_EndsAllProcessing()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        await fixture.DispatchAsync(eventId);
        Assert.Equal("Wartend", (await fixture.EventAsync(eventId)).Status);
        var answered = fixture.Responses.Count;
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken));
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Abgebrochen", item.Status);
        Assert.NotNull(item.Reason);
        Assert.Null(item.NextAttemptUtc);
        await fixture.DispatchAsync(eventId);
        item = await fixture.EventAsync(eventId);
        Assert.Equal("Abgebrochen", item.Status);
        Assert.Equal(answered, fixture.Responses.Count);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Both final states reject a cancellation without touching the record.</summary>
    [Theory]
    [InlineData("Verarbeitet")]
    [InlineData("Abgebrochen")]
    public async Task Cancel_FinalEvent_IsRejected(string status)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == eventId).ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, status), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken));
        }
        Assert.Equal(status, (await fixture.EventAsync(eventId)).Status);
    }

    /// <summary>Ein abgebrochenes früheres Ereignis blockiert die Warteschlangenreihenfolge seiner Aufgabe nicht mehr.</summary>
    [Fact]
    public async Task Cancel_PredecessorEvent_LetsSuccessorDispatchContinue()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, firstId) = await fixture.AddAsync(configured: true);
        Guid secondId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var first = await db.ScmIncomingEvents.AsNoTracking().SingleAsync(x => x.Id == firstId, cancellationToken: TestContext.Current.CancellationToken);
            var observation = ScmEventStore.Observation(first) with { RemoteNodeId = "second-node" };
            var time = DateTimeOffset.UtcNow;
            await scope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(taskId, "second-source", new(new(observation.Reference, observation.ContentKind, null, time), [observation]), ct: TestContext.Current.CancellationToken);
            secondId = await db.ScmIncomingEvents.Where(x => x.WorkItemId == taskId && x.Id != firstId).Select(x => x.Id).SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        }
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(firstId, TestContext.Current.CancellationToken));
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(firstId)).Status);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Ein während des Prompt-Schreibens eintreffender Abbruch gewinnt gegen das verfolgte Zustellungsergebnis.</summary>
    [Fact]
    public async Task Cancel_DuringPromptWrite_ClosesEventWithoutDeliveredStatus()
    {
        ScmDispatchFixture? fixture = null;
        var eventId = Guid.Empty;
        var prompts = new List<string>();
        var profile = new ScmAiProfile(ScmAiProfile.Simulator.Prefix, ScmAiProfile.Simulator.Arguments,
            ScmAiProfile.Simulator.Banner, ScmAiProfile.Simulator.Prompt, ScmAiProfile.Simulator.EnvironmentVariables,
            async prompt =>
            {
                prompts.Add(prompt);
                using var scope = fixture!.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId);
            });
        fixture = new ScmDispatchFixture(ai: profile);
        await using (fixture)
        {
            await fixture.InitializeAsync();
            var (_, id) = await fixture.AddAsync(configured: true);
            eventId = id;
            await fixture.DispatchAsync(eventId);
            var item = await fixture.EventAsync(eventId);
            Assert.Single(prompts);
            Assert.NotNull(item.DeliveryAttemptId);
            Assert.Equal("Abgebrochen", item.Status);
        }
    }

    /// <summary>Ein zugestelltes Ereignis, das während der CLI-Arbeit abgebrochen wird, wird ohne Abgleich geschlossen.</summary>
    [Fact]
    public async Task Cancel_DeliveredEvent_StopsReconciliation()
    {
        var clock = new TestClock();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken));
        clock.Now = clock.Now.AddMinutes(10);
        await fixture.DispatchAsync(eventId);
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
