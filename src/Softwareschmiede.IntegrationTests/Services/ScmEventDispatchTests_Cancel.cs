using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Abgebrochene Ereignisse sind für den Poll endgültig: kein Dispatch und kein Remote-Outbox-Flush.</summary>
public sealed class ScmEventDispatchTests_Cancel
{
    /// <summary>Ein abgebrochenes Ereignis wird niemals zugestellt und löst keine Remote-Antwort aus.</summary>
    [Fact]
    public async Task Monitor_SkipsCancelledEventEntirely()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken));
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Eine noch ausstehende Host-Antwort eines abgebrochenen Ereignisses wird remote nicht wiederholt.</summary>
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
        // Der Versuch hat mindestens eine Hostaktion remote ausgelöst (Empfangsreaktion und
        // ggf. Warte-Kommentar der Stabilisierungsfrist), die alle im Rate-Limit hängen bleiben.
        var callsAfterDispatch = calls;
        Assert.True(callsAfterDispatch >= 1, "Der Versuch hat keine Hostaktion remote ausgelöst.");
        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken));
        clock.Now = clock.Now.AddHours(1);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
        Assert.Equal(callsAfterDispatch, calls);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
