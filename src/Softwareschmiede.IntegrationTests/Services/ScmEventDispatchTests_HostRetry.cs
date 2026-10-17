using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Host response retries must not reopen a separately blocked event dispatch.</summary>
public sealed class ScmEventDispatchTests_HostRetry
{
    /// <summary>Only an explicit retry or the event deadline may release the dispatch delay.</summary>
    [Theory]
    [InlineData(ScmResponseKind.Reaction, true)]
    [InlineData(ScmResponseKind.Comment, true)]
    [InlineData(ScmResponseKind.Reaction, false)]
    public async Task DueHostResponse_DoesNotBypassDispatchDelay(ScmResponseKind delayedResponse, bool manualRetry)
    {
        var clock = new TestClock();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        fixture.Ki.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var hostAttempts = 0;
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == delayedResponse), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest request, CancellationToken _) =>
            {
                if (++hostAttempts == 1)
                    throw new ScmProviderException(ScmProviderFailureKind.RateLimit, "Fixture rate limit", clock.Now.AddSeconds(10));
                fixture.Responses.Add(request);
                return Task.FromResult(new ScmResponseResult("remote", null, false));
            });
        await fixture.DispatchAsync(eventId);
        Assert.Equal(long.MaxValue, (await fixture.EventAsync(eventId)).NextAttemptUtc);
        var retryAt = manualRetry ? long.MaxValue : clock.Now.AddHours(2).ToUnixTimeMilliseconds();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmIncomingEvents
                .Where(x => x.Id == eventId).ExecuteUpdateAsync(x => x.SetProperty(e => e.NextAttemptUtc, retryAt));

        fixture.Ki.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        clock.Now = clock.Now.AddHours(1);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();

        Assert.Equal(2, hostAttempts);
        fixture.Ki.Verify(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        Assert.Equal(retryAt, (await fixture.EventAsync(eventId)).NextAttemptUtc);
        if (manualRetry)
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId);
        }
        else
        {
            clock.Now = clock.Now.AddHours(1);
            await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
        }
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();
        Assert.Single(fixture.Prompts);
    }

    /// <summary>A completed event still reconciles its independent receipt outbox.</summary>
    [Fact]
    public async Task CompletedEvent_RetriesHostResponseWithoutNewDispatch()
    {
        var clock = new TestClock();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.RateLimit, "Fixture rate limit", clock.Now.AddSeconds(10)));
        await fixture.DispatchAsync(eventId);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmIncomingEvents
                .Where(x => x.Id == eventId).ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, "Verarbeitet"));
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScmResponseResult("remote", null, false));
        clock.Now = clock.Now.AddHours(1);

        await fixture.Services.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync();

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal("Succeeded", (await db.ScmOutgoingActions.SingleAsync()).Status);
        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        fixture.Ki.Verify(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
