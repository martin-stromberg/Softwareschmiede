using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Reihenfolge-Regel der Ereigniszustellung: Nur ein transient offener (oder unbekannter)
/// Vorgänger hält ein Folgeereignis derselben Aufgabe auf <c>Wartend</c> zurück — final oder terminal
/// ungeklärte Vorgänger geben die Zustellung frei. Unter den Folgeereignissen selbst bleibt die
/// Reihenfolge gewahrt.</summary>
public sealed class ScmEventDispatchTests_Ordering
{
    /// <summary>Ein Folgeereignis hinter einem finalen oder terminal ungeklärten Vorgänger wird
    /// regulär zugestellt statt dauerhaft zu warten.</summary>
    [Theory]
    [InlineData(ScmIncomingEventStatus.VerarbeitungUngeklärt)]
    [InlineData(ScmIncomingEventStatus.ZustellungUngewiss)]
    [InlineData(ScmIncomingEventStatus.Verarbeitet)]
    [InlineData(ScmIncomingEventStatus.Abgebrochen)]
    public async Task Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered(string predecessorStatus)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, predecessorId) = await fixture.AddAsync(configured: true);
        await fixture.SetEventStatusAsync(predecessorId, predecessorStatus);
        var followUpId = await AddEventToTaskAsync(fixture, taskId, "$Softwareschmiede Folgeereignis");

        await fixture.DispatchAsync(followUpId);

        var followUp = await fixture.EventAsync(followUpId);
        Assert.Equal(ScmIncomingEventStatus.Zugestellt, followUp.Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Ein transient offener Vorgänger hält das Folgeereignis weiterhin mit dem
    /// Reihenfolge-Wartegrund zurück — die Reihenfolge-Garantie bleibt bestehen.</summary>
    [Theory]
    [InlineData(ScmIncomingEventStatus.Aufgenommen)]
    [InlineData(ScmIncomingEventStatus.Verzögert)]
    [InlineData(ScmIncomingEventStatus.Wartend)]
    [InlineData(ScmIncomingEventStatus.ZustellungVorbereitet)]
    [InlineData(ScmIncomingEventStatus.Zugestellt)]
    [InlineData(ScmIncomingEventStatus.InBearbeitung)]
    public async Task Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting(string predecessorStatus)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, predecessorId) = await fixture.AddAsync(configured: true);
        await fixture.SetEventStatusAsync(predecessorId, predecessorStatus);
        var followUpId = await AddEventToTaskAsync(fixture, taskId, "$Softwareschmiede Folgeereignis");

        await fixture.DispatchAsync(followUpId);

        var followUp = await fixture.EventAsync(followUpId);
        Assert.Equal(ScmIncomingEventStatus.Wartend, followUp.Status);
        Assert.Equal("Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.", followUp.Reason);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Mehrere Folgeereignisse hinter einem terminalen Vorgänger: nur das älteste wird
    /// freigegeben; sein transienter Zustellstatus hält das jüngere zurück, bis es selbst final ist.</summary>
    [Fact]
    public async Task Dispatch_MultipleFollowUps_KeepRelativeOrder()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, predecessorId) = await fixture.AddAsync(configured: true);
        await fixture.SetEventStatusAsync(predecessorId, ScmIncomingEventStatus.VerarbeitungUngeklärt);
        var olderId = await AddEventToTaskAsync(fixture, taskId, "$Softwareschmiede aelteres Folgeereignis");
        var youngerId = await AddEventToTaskAsync(fixture, taskId, "$Softwareschmiede juengeres Folgeereignis");

        await fixture.DispatchAsync(olderId);
        Assert.Equal(ScmIncomingEventStatus.Zugestellt, (await fixture.EventAsync(olderId)).Status);

        await fixture.DispatchAsync(youngerId);
        var younger = await fixture.EventAsync(youngerId);
        Assert.Equal(ScmIncomingEventStatus.Wartend, younger.Status);
        Assert.Equal("Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.", younger.Reason);
        Assert.Single(fixture.Prompts);

        await fixture.SetEventStatusAsync(olderId, ScmIncomingEventStatus.Verarbeitet);
        // Der Simulator meldet Turn-Ende: Ein neuer Eingabeprompt im Puffer hebt den
        // Beschäftigt-Zustand des Session-Adapters für die nächste Zustellung auf.
        fixture.SetPrompt(taskId, fixture.Prompt);
        await fixture.DispatchAsync(youngerId);

        Assert.Equal(ScmIncomingEventStatus.Zugestellt, (await fixture.EventAsync(youngerId)).Status);
        Assert.Equal(2, fixture.Prompts.Count);
        Assert.Contains("aelteres Folgeereignis", fixture.Prompts[0]);
        Assert.Contains("juengeres Folgeereignis", fixture.Prompts[1]);
    }

    /// <summary>Legt ein weiteres Ereignis auf derselben Aufgabe an — über den regulären
    /// Inbox-Pfad mit eigener Remote-Knoten-ID, damit es eine größere Sequence erhält.</summary>
    private static async Task<Guid> AddEventToTaskAsync(ScmDispatchFixture fixture, Guid taskId, string body)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var time = DateTimeOffset.UtcNow;
        var observation = new ScmEventObservation(
            new(fixture.Repository, ScmReferenceKind.Issue, 7), ScmContentKind.IssueComment,
            Guid.NewGuid().ToString(), "42", time, time, null, "U_1", "owner", null, null,
            body, "https://github.com/owner/repo/issues/7", time);
        await scope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(
            taskId, taskId.ToString(),
            new(new(observation.Reference, observation.ContentKind, null, time), [observation]),
            ct: TestContext.Current.CancellationToken);
        return await db.ScmIncomingEvents.Where(x => x.WorkItemId == taskId)
            .OrderByDescending(x => x.Sequence).Select(x => x.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
    }
}
