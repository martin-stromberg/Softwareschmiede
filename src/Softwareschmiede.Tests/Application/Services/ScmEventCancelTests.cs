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
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));
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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == eventId).ExecuteUpdateAsync(x => x.SetProperty(p => p.Status, status));
            Assert.False(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));
        }
        Assert.Equal(status, (await fixture.EventAsync(eventId)).Status);
    }

    /// <summary>A cancelled earlier event no longer blocks the queue order of its task.</summary>
    [Fact]
    public async Task Cancel_PredecessorEvent_LetsSuccessorDispatchContinue()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, firstId) = await fixture.AddAsync(configured: true);
        Guid secondId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var first = await db.ScmIncomingEvents.AsNoTracking().SingleAsync(x => x.Id == firstId);
            var observation = ScmEventStore.Observation(first) with { RemoteNodeId = "second-node" };
            var time = DateTimeOffset.UtcNow;
            await scope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(taskId, "second-source",
                new(new(observation.Reference, observation.ContentKind, null, time), [observation]));
            secondId = await db.ScmIncomingEvents.Where(x => x.AufgabeId == taskId && x.Id != firstId).Select(x => x.Id).SingleAsync();
        }
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(firstId));
        await fixture.DispatchAsync(secondId);
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(firstId)).Status);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(secondId)).Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>A cancellation landing during the prompt write wins over the tracked delivery outcome.</summary>
    [Fact]
    public async Task Cancel_DuringPromptWrite_ClosesEventWithoutDeliveredStatus()
    {
        ScmDispatchFixture? fixture = null;
        var eventId = Guid.Empty;
        var prompts = new List<string>();
        var profile = new ScmKiProfile(ScmKiProfile.Simulator.Prefix, ScmKiProfile.Simulator.Arguments,
            ScmKiProfile.Simulator.Banner, ScmKiProfile.Simulator.Prompt, ScmKiProfile.Simulator.EnvironmentVariables,
            async prompt =>
            {
                prompts.Add(prompt);
                using var scope = fixture!.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId);
            });
        fixture = new ScmDispatchFixture(ki: profile);
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

    /// <summary>A delivered event that is cancelled while the CLI works is closed without reconciliation.</summary>
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
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));
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
