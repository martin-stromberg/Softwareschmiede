using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>End-to-end-Verhalten der verzögerten Zustellung: Vor dem Zielzeitpunkt werden
/// nur Hostaktionen nachgeholt, danach läuft die reguläre Zustellung.</summary>
public sealed class ScmEventDispatchTests_Delay
{
    /// <summary>Die Zustellung ruht bis zum Zielzeitpunkt; der Empfang wird remote quittiert
    /// und die Wiederaufnahme erfolgt ohne Zusatzlogik über die normale Auswahl.</summary>
    [Fact]
    public async Task DeferredEvent_ReceivesWaitingCommentAndDeliversAfterTarget()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true, body: "$Softwareschmiede #verzögert 2h bitte übernehmen");

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verzögert", item.Status);
        Assert.True(item.NextAttemptUtc > DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds());

        // Vor dem Zielzeitpunkt: nur Empfangsaktionen, keine Zustellung und kein CLI-Start.
        await fixture.DispatchAsync(eventId);
        Assert.Equal("Verzögert", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Prompts);
        Assert.Contains(fixture.Responses, x => x.Kind == ScmResponseKind.Reaction);
        Assert.Contains(fixture.Responses, x => x.Kind == ScmResponseKind.Comment && x.Body!.Contains("verzögert"));

        // Zielzeitpunkt erreicht (verkürzt simuliert): Pause und Zurückstellung sind beendet.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.ScmIncomingEvents.Where(x => x.Id == eventId).ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)null));
            await db.Aufgaben.Where(x => x.Id == taskId).ExecuteUpdateAsync(x => x.SetProperty(p => p.PausiertBisUtc, (DateTimeOffset?)null));
        }
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Der Abbruch einer verzögerten Ausführung hebt die Aufgabenpause mit auf.</summary>
    [Fact]
    public async Task Cancel_DeferredEvent_LiftsTaskPause()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true, body: "$Softwareschmiede #verzögert 2h bitte übernehmen");
        Assert.Equal("Verzögert", (await fixture.EventAsync(eventId)).Status);

        using (var scope = fixture.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId));

        Assert.Equal("Abgebrochen", (await fixture.EventAsync(eventId)).Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            Assert.Null((await db.Aufgaben.SingleAsync(x => x.Id == taskId)).PausiertBisUtc);
        }
        await fixture.DispatchAsync(eventId);
        Assert.Empty(fixture.Prompts);
    }
}
