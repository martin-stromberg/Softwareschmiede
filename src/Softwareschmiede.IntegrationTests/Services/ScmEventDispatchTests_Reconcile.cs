using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Kanalabgleich mit entkoppelter Pull-Request-Registrierung: Eine im Abschluss gemeldete
/// <c>pullRequestNumber</c> wird als <see cref="PullRequestReference"/> verknüpft — auch ohne
/// Annahmemeldung, bei ungeklärtem Ereignisstatus und bei verspätet eintreffendem
/// <c>complete.json</c>; wiederholte Abgleiche erzeugen weder Referenz- noch Protokollduplikate.</summary>
public sealed class ScmEventDispatchTests_Reconcile
{
    /// <summary>Ein <c>complete.json</c> mit <c>pullRequestNumber</c> ohne <c>accepted.json</c>
    /// verknüpft den Pull Request und protokolliert ihn genau einmal; das Ereignis selbst
    /// endet ungeklärt, ohne dass die Verknüpfung verloren geht.</summary>
    [Fact]
    public async Task Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference()
    {
        var clock = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        var delivered = await SeedEventWithExchangeAsync(fixture, eventId);
        clock.Now = clock.Now.AddMinutes(3);
        await WriteCompleteAsync(delivered, pullRequestNumber: 7);

        // Zwei Abgleiche wie beim Sekundentakt des Antwortkanal-Dienstes: Der erste
        // beobachtet die neu abgelegte Datei nur (Stabilitätsprüfung des Protokolls)
        // und lässt das Ereignis ungeklärt enden; der zweite liest den Abschluss.
        await fixture.DispatchAsync(eventId);
        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);
        await fixture.DispatchAsync(eventId);

        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var reference = Assert.Single(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, reference.WorkItemId);
        Assert.Equal(7, reference.PullRequestNumber);
        Assert.Equal("owner/repo", reference.RepositoryId);
        Assert.Equal(PullRequestReferenceRole.CreatedByTask, reference.Role);
        Assert.Equal("https://github.com/owner/repo/pull/7", reference.Url);
        Assert.Single(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("Pull Request #7"));
    }

    /// <summary>Der Kanalabgleich treibt nicht abgeschlossene Ereignisse im Sekundentakt erneut
    /// durch denselben Pfad — weder Referenz noch Protokolleintrag dürfen sich wiederholen.</summary>
    [Fact]
    public async Task Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce()
    {
        var clock = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var delivered = await SeedEventWithExchangeAsync(fixture, eventId);
        clock.Now = clock.Now.AddMinutes(3);
        await WriteCompleteAsync(delivered, pullRequestNumber: 7);
        // Erster Abgleich stabilisiert nur die Dateibeobachtung; die beiden folgenden
        // lesen den Abschluss und müssen sich dedupliziert verhalten.
        await fixture.DispatchAsync(eventId);
        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Single(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("Pull Request #7"));
    }

    /// <summary>Eine bereits einer anderen Aufgabe zugeordnete Referenz wird nicht übernommen:
    /// die Verknüpfung bleibt beim Fremdbesitzer, der Hinweis wird genau einmal protokolliert
    /// und wiederholte Abgleiche erzeugen kein Duplikat.</summary>
    [Fact]
    public async Task Reconcile_ReportedPullRequestAssignedToOtherTask_KeepsForeignAssignment()
    {
        var clock = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        var (otherTaskId, _) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.PullRequestReferences.Add(new PullRequestReference
            {
                Id = Guid.NewGuid(),
                WorkItemId = otherTaskId,
                Provider = PullRequestProvider.GitHub,
                Role = PullRequestReferenceRole.CreatedByTask,
                RepositoryId = "owner/repo",
                PullRequestNumber = 7,
                CreatedUtc = DateTimeOffset.UtcNow,
                MonitoringPhase = PullRequestMonitoringPhase.Created
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var delivered = await SeedEventWithExchangeAsync(fixture, eventId);
        clock.Now = clock.Now.AddMinutes(3);
        await WriteCompleteAsync(delivered, pullRequestNumber: 7);

        // Erster Abgleich stabilisiert die Dateibeobachtung; die beiden folgenden lesen den
        // Abschluss — der Fremdzuordnungs-Hinweis darf dabei nur einmal protokolliert werden.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        using var checkScope = fixture.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var reference = Assert.Single(await checkDb.PullRequestReferences
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(otherTaskId, reference.WorkItemId);
        var entry = Assert.Single(await checkDb.LogEntries
            .Where(x => x.Type == LogEntryType.GitAction)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("bereits einer anderen Aufgabe zugeordnet"));
        Assert.Equal(taskId, entry.WorkItemId);
    }

    /// <summary>Ein verspätet lesbares <c>complete.json</c> holt der laufende Abgleich nach:
    /// die gemeldete Nummer wird auch nach dem Übergang in den ungeklärten Status registriert.</summary>
    [Fact]
    public async Task Reconcile_LateCompleteAfterUnresolved_RegistersReference()
    {
        var clock = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var delivered = await SeedEventWithExchangeAsync(fixture, eventId);
        clock.Now = clock.Now.AddMinutes(3);
        await fixture.DispatchAsync(eventId);
        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);

        await WriteCompleteAsync(delivered, pullRequestNumber: 7);
        // Erster Folgeabgleich stabilisiert die Dateibeobachtung, zweiter verarbeitet sie.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var reference = Assert.Single(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(7, reference.PullRequestNumber);
        Assert.Single(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("Pull Request #7"));
    }

    /// <summary>Auch wenn die Abschluss-Mengenprüfung scheitert und das Ereignis ungeklärt endet,
    /// wird die gemeldete <c>pullRequestNumber</c> registriert.</summary>
    [Fact]
    public async Task Reconcile_FailedCompletenessCheck_StillRegistersReference()
    {
        var clock = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: clock);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        var delivered = await SeedEventWithExchangeAsync(fixture, eventId);
        await WriteAcceptedAsync(delivered);
        // Die Mengenprüfung scheitert: complete.json meldet eine Aktionskennung, die nie als
        // Kanalaktion ankam — trotzdem bleibt die Pull-Request-Verknüpfung erhalten.
        await WriteCompleteAsync(delivered, pullRequestNumber: 7, actionIds: [Guid.NewGuid()]);

        // Erster Abgleich stabilisiert nur die Dateibeobachtung, zweiter verarbeitet sie.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);
        Assert.Equal(ScmIncomingEventStatus.InBearbeitung, (await fixture.EventAsync(eventId)).Status);

        clock.Now = clock.Now.AddMinutes(31);
        await fixture.DispatchAsync(eventId);

        Assert.Equal(ScmIncomingEventStatus.VerarbeitungUngeklärt, (await fixture.EventAsync(eventId)).Status);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var reference = Assert.Single(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, reference.WorkItemId);
        Assert.Equal(7, reference.PullRequestNumber);
        Assert.Single(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("Pull Request #7"));
    }

    /// <summary>Liefert ein zugestelltes Ereignis mit gesetztem Versuchskanal
    /// (<see cref="ScmIncomingEvent.DeliveryAttemptId"/>, <see cref="ScmIncomingEvent.CliRunId"/>,
    /// <see cref="ScmIncomingEvent.ExchangePath"/>); Kanaldateien schreiben die Aufrufer je Szenario.</summary>
    private static async Task<ScmIncomingEvent> SeedEventWithExchangeAsync(ScmDispatchFixture fixture, Guid eventId)
    {
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal(ScmIncomingEventStatus.Zugestellt, delivered.Status);
        return delivered;
    }

    private static Task WriteAcceptedAsync(ScmIncomingEvent delivered)
        => File.WriteAllTextAsync(Path.Combine(delivered.ExchangePath!, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, delivered.Id,
                delivered.DeliveryAttemptId!.Value, Guid.Parse(delivered.CliRunId!)), TestJson.Options));

    private static Task WriteCompleteAsync(ScmIncomingEvent delivered, int? pullRequestNumber, Guid[]? actionIds = null)
        => File.WriteAllTextAsync(Path.Combine(delivered.ExchangePath!, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, delivered.Id,
                delivered.DeliveryAttemptId!.Value, Guid.Parse(delivered.CliRunId!),
                actionIds ?? [], false, Report: "PR angelegt.", PullRequestNumber: pullRequestNumber), TestJson.Options));
}
