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

/// <summary>Issue-Beschreibungs-Updates laufen als deterministische Hostaktion über die Outbox.</summary>
public sealed class ScmEventDispatchTests_IssueBodyUpdate
{
    /// <summary>Die eingereihte Hostaktion erreicht den Responder genau einmal mit dem
    /// gespeicherten Ursprung und der deterministischen Aktions-ID des Ereignisses.</summary>
    [Fact]
    public async Task IssueBodyAction_ExecutesViaOutboxWithDeterministicId()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var action = await EnqueueIssueBodyAsync(fixture, eventId);

        await fixture.DispatchAsync(eventId);

        var request = Assert.Single(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
        Assert.Equal(action.Id.ToString("D"), request.ActionId);
        Assert.Equal(ScmEventStore.ActionId(eventId, "issuebody").ToString("D"), request.ActionId);
        Assert.Equal("Verbesserte Beschreibung.", request.Body);
        Assert.Equal(fixture.Repository, request.Reference.Repository);
        var stored = await ActionAsync(fixture, action.Id);
        Assert.Equal("Succeeded", stored.Status);
        Assert.Equal("remote", stored.RemoteId);
        Assert.Equal("issuebody", stored.Purpose);
        Assert.Null(stored.AttemptId);
    }

    /// <summary>Eine remote bereits angewendete Aktion wird vom Abgleich erkannt — der
    /// Schreibaufruf bleibt aus, das Ergebnis wird ohne Doppelschreiben übernommen.</summary>
    [Fact]
    public async Task IssueBodyAction_AlreadyApplied_SkipsExecute()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var action = await EnqueueIssueBodyAsync(fixture, eventId);
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ReconcileResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == ScmResponseKind.IssueBodyUpdate), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScmResponseResult("issue-77", "https://github.com/owner/repo/issues/7", true));

        await fixture.DispatchAsync(eventId);

        Assert.DoesNotContain(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
        var stored = await ActionAsync(fixture, action.Id);
        Assert.Equal("Succeeded", stored.Status);
        Assert.Equal("issue-77", stored.RemoteId);
    }

    /// <summary>Ein temporärer Fehler geht mit Backoff in die Wiederholung und wird beim
    /// nächsten fälligen Dispatch ohne neuen Zustellversuch erfolgreich nachgeholt.</summary>
    [Fact]
    public async Task IssueBodyAction_TemporaryFailure_RetriesAfterBackoff()
    {
        var clock = new TestClock();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var action = await EnqueueIssueBodyAsync(fixture, eventId);
        var calls = 0;
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == ScmResponseKind.IssueBodyUpdate), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest _, CancellationToken _) =>
                ++calls == 1
                    ? Task.FromException<ScmResponseResult>(new ScmProviderException(ScmProviderFailureKind.Temporary, "Kurzzeitig nicht erreichbar."))
                    : Task.FromResult(new ScmResponseResult("issue-77")));

        await fixture.DispatchAsync(eventId);

        var failed = await ActionAsync(fixture, action.Id);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal("Temporary", failed.ErrorCode);
        Assert.Equal("Kurzzeitig nicht erreichbar.", failed.Error);
        Assert.InRange(failed.NextAttemptUtc ?? 0,
            clock.Now.ToUnixTimeMilliseconds() + 30_000, clock.Now.ToUnixTimeMilliseconds() + 33_000);

        clock.Now = clock.Now.AddMinutes(2);
        await fixture.DispatchAsync(eventId);

        Assert.Equal(2, calls);
        var stored = await ActionAsync(fixture, action.Id);
        Assert.Equal("Succeeded", stored.Status);
        Assert.Equal("issue-77", stored.RemoteId);
        Assert.Null(stored.NextAttemptUtc);
        Assert.Null(stored.ErrorCode);
    }

    /// <summary>Ein Sperrfehler blockiert die Aktion hart bis zur manuellen Korrektur; erst der
    /// ausdrückliche Neuversand gibt sie wieder frei.</summary>
    [Fact]
    public async Task IssueBodyAction_PermissionFailure_LocksUntilRetry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var action = await EnqueueIssueBodyAsync(fixture, eventId);
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == ScmResponseKind.IssueBodyUpdate), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permission, "Schreibrecht fehlt."));

        await fixture.DispatchAsync(eventId);

        var failed = await ActionAsync(fixture, action.Id);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal("Permission", failed.ErrorCode);
        Assert.Equal("Schreibrecht fehlt.", failed.Error);
        Assert.Equal(long.MaxValue, failed.NextAttemptUtc);

        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScmResponseResult("issue-77"));
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId, TestContext.Current.CancellationToken);

        var stored = await ActionAsync(fixture, action.Id);
        Assert.Equal("Succeeded", stored.Status);
        Assert.Equal("issue-77", stored.RemoteId);
        Assert.Null(stored.NextAttemptUtc);
    }

    /// <summary>Ein Responder ohne die Antwortfähigkeit meldet einen ehrlichen typisierten
    /// Fehler — die Aktion bleibt sichtbar fehlgeschlagen statt still ignoriert zu werden.</summary>
    [Fact]
    public async Task IssueBodyAction_UnsupportedPlugin_ReportsHonestError()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var action = await EnqueueIssueBodyAsync(fixture, eventId);
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == ScmResponseKind.IssueBodyUpdate), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permanent, "Antwortaktion wird nicht unterstützt."));

        await fixture.DispatchAsync(eventId);

        var stored = await ActionAsync(fixture, action.Id);
        Assert.Equal("Failed", stored.Status);
        Assert.Equal("Permanent", stored.ErrorCode);
        Assert.Equal("Antwortaktion wird nicht unterstützt.", stored.Error);
        Assert.Equal(long.MaxValue, stored.NextAttemptUtc);
        Assert.DoesNotContain(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
    }

    /// <summary>Reiht die Beschreibungsaktualisierung als Hostaktion ein — mit derselben
    /// deterministischen Aktions-ID-Konvention wie die bestehenden Host-Zwecke.</summary>
    private static async Task<ScmOutgoingAction> EnqueueIssueBodyAsync(ScmDispatchFixture fixture, Guid eventId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId);
        var request = new ScmResponseRequest(ScmEventStore.Observation(item).Reference,
            ScmEventStore.ActionId(item.Id, "issuebody").ToString("D"), ScmResponseKind.IssueBodyUpdate,
            "Verbesserte Beschreibung.");
        return await scope.ServiceProvider.GetRequiredService<ScmEventOutbox>().EnsureAsync(item, "issuebody", request, CancellationToken.None);
    }

    private static async Task<ScmOutgoingAction> ActionAsync(ScmDispatchFixture fixture, Guid actionId)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>()
            .ScmOutgoingActions.AsNoTracking().SingleAsync(x => x.Id == actionId);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
