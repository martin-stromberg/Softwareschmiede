using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Cancelled events are final for the poll: no dispatch and no remote outbox flush.</summary>
public sealed class ScmEventDispatchTests_Cancel
{
    /// <summary>A cancelled event is never delivered and never answers remotely.</summary>
    [Fact]
    public async Task Monitor_SkipsCancelledEventEntirely()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>A still pending host response of a cancelled event is not retried remotely.</summary>
    [Fact]
    public async Task Monitor_CancelledEvent_DoesNotFlushPendingOutbox()
    {
        var clock = new TestClock();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var calls = 0;
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest _, CancellationToken _) =>
            {
                calls++;
                throw new ScmProviderException(ScmProviderFailureKind.RateLimit, "Fixture rate limit", clock.Now.AddSeconds(10));
            });
        await fixture.DispatchAsync(eventId);
        Assert.Equal(1, calls);
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));
        clock.Now = clock.Now.AddHours(1);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
        Assert.Equal(1, calls);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
