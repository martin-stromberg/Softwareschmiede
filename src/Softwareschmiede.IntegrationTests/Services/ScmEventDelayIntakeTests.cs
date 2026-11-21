using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Verzögerungs-Direktiven bei der Ereignisaufnahme: zurückgestellte Zustellung,
/// Aufgabenpause, Empfangskommentar per Outbox und Verwerfen ungültiger Angaben.</summary>
public sealed class ScmEventDelayIntakeTests
{
    /// <summary>Eine gültige Direktive stellt die Zustellung zurück, pausiert die Aufgabe
    /// und legt den Empfangskommentar als wartende Hostaktion an.</summary>
    [Fact]
    public async Task DelayDirective_DefersEventAndPauseTask()
    {
        await using var db = await CreateDbAsync();
        var taskId = await SeedTaskAsync(db);
        var before = DateTimeOffset.UtcNow;
        var store = new ScmEventStore(db, new(), TimeProvider.System);

        await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede bitte #verzögert 2h")), ct: TestContext.Current.CancellationToken);

        var item = await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Verzögert", item.Status);
        Assert.NotNull(item.NextAttemptUtc);
        Assert.InRange(item.NextAttemptUtc.Value,
            before.AddHours(2).ToUnixTimeMilliseconds(),
            DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds());
        Assert.Contains("verzögert", item.Reason, StringComparison.OrdinalIgnoreCase);

        var action = await db.ScmOutgoingActions.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("waiting", action.Purpose);
        Assert.Equal(item.Id, action.EventId);
        Assert.Equal(ScmEventStore.ActionId(item.Id, "waiting"), action.Id);
        var request = System.Text.Json.JsonSerializer.Deserialize<ScmResponseRequest>(action.RequestJson, ScmEventStore.JsonOptions)!;
        Assert.Contains("verzögert", request.Body, StringComparison.OrdinalIgnoreCase);

        var task = await db.WorkItems.SingleAsync(x => x.Id == taskId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(task.PausedUntilUtc);
        Assert.Equal(item.NextAttemptUtc.Value, task.PausedUntilUtc.Value.ToUnixTimeMilliseconds());
        Assert.Contains(await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("verzögerte SCM-Ausführung"));
    }

    /// <summary>Eine ungültige Angabe verzögert nichts; die Verwerfung wird remote quittiert.</summary>
    [Fact]
    public async Task InvalidDelayDirective_RunsNormallyWithFeedbackComment()
    {
        await using var db = await CreateDbAsync();
        var taskId = await SeedTaskAsync(db);
        var store = new ScmEventStore(db, new(), TimeProvider.System);

        await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede #verzögert 99t")), ct: TestContext.Current.CancellationToken);

        var item = await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Aufgenommen", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Null((await db.WorkItems.SingleAsync(x => x.Id == taskId, cancellationToken: TestContext.Current.CancellationToken)).PausedUntilUtc);
        var action = await db.ScmOutgoingActions.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        var request = System.Text.Json.JsonSerializer.Deserialize<ScmResponseRequest>(action.RequestJson, ScmEventStore.JsonOptions)!;
        Assert.Contains("ignoriert", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("32 Tagen", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ohne Direktive bleiben Ereignis, Outbox und Aufgabe unverändert.</summary>
    [Fact]
    public async Task NoDirective_KeepsRegularIntake()
    {
        await using var db = await CreateDbAsync();
        var taskId = await SeedTaskAsync(db);
        var store = new ScmEventStore(db, new(), TimeProvider.System);

        await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede bitte übernehmen")), ct: TestContext.Current.CancellationToken);

        var item = await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Aufgenommen", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Empty(await db.ScmOutgoingActions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null((await db.WorkItems.SingleAsync(x => x.Id == taskId, cancellationToken: TestContext.Current.CancellationToken)).PausedUntilUtc);
    }

    /// <summary>Ein konfiguriertes Keyword ersetzt den Standard bei der Aufnahme.</summary>
    [Fact]
    public async Task ConfiguredDelayKeyword_IsUsedAtIntake()
    {
        await using var db = await CreateDbAsync();
        var taskId = await SeedTaskAsync(db);
        var store = new ScmEventStore(db, new(), TimeProvider.System);

        await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede #verzögert 2h")), delayKeyword: "#warte", ct: TestContext.Current.CancellationToken);
        Assert.Equal("Aufgenommen", (await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).Status);

        await store.AcceptBatchAsync(taskId, "source2", Batch(Observe("$Softwareschmiede #warte 30min", node: "node2")), delayKeyword: "#warte", ct: TestContext.Current.CancellationToken);
        Assert.Equal("Verzögert", (await db.ScmIncomingEvents.SingleAsync(x => x.EventKey.Contains("node2"), cancellationToken: TestContext.Current.CancellationToken)).Status);
    }

    /// <summary>Eine bestehende längere Pause wird nicht verkürzt.</summary>
    [Fact]
    public async Task DelayDirective_DoesNotShortenExistingPause()
    {
        await using var db = await CreateDbAsync();
        var taskId = await SeedTaskAsync(db);
        var existing = DateTimeOffset.UtcNow.AddDays(10);
        await db.WorkItems.Where(x => x.Id == taskId).ExecuteUpdateAsync(x => x.SetProperty(p => p.PausedUntilUtc, existing), cancellationToken: TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var store = new ScmEventStore(db, new(), TimeProvider.System);

        await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede #verzögert 2h")), ct: TestContext.Current.CancellationToken);

        Assert.Equal("Verzögert", (await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).Status);
        Assert.Equal(existing.ToUnixTimeMilliseconds(),
            (await db.WorkItems.SingleAsync(x => x.Id == taskId, cancellationToken: TestContext.Current.CancellationToken)).PausedUntilUtc!.Value.ToUnixTimeMilliseconds());
    }

    /// <summary>Die Zurückstellung übersteht einen Neustart, weil Zielzeitpunkt und
    /// Aufgabenpause persistierte Felder sind.</summary>
    [Fact]
    public async Task DelayedEvent_SurvivesContextReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scm-delay-{Guid.NewGuid():N}.db");
        try
        {
            Guid taskId;
            await using (var db = await CreateFileDbAsync(path))
                taskId = await SeedTaskAsync(db);
            DateTimeOffset pause;
            long nextAttempt;
            await using (var db = await CreateFileDbAsync(path))
            {
                var store = new ScmEventStore(db, new(), TimeProvider.System);
                await store.AcceptBatchAsync(taskId, "source", Batch(Observe("$Softwareschmiede #verzögert 2h")), ct: TestContext.Current.CancellationToken);
            }
            await using (var db = await CreateFileDbAsync(path))
            {
                var item = await db.ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal("Verzögert", item.Status);
                nextAttempt = item.NextAttemptUtc!.Value;
                pause = (await db.WorkItems.SingleAsync(x => x.Id == taskId, cancellationToken: TestContext.Current.CancellationToken)).PausedUntilUtc!.Value;
            }
            Assert.True(nextAttempt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.True(pause > DateTimeOffset.UtcNow);
        }
        finally { File.Delete(path); }
    }

    private static async Task<SoftwareschmiedeDbContext> CreateFileDbAsync(string path)
    {
        var db = new SoftwareschmiedeDbContext(new DbContextOptionsBuilder<SoftwareschmiedeDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<SoftwareschmiedeDbContext> CreateDbAsync()
    {
        var db = new SoftwareschmiedeDbContext(new DbContextOptionsBuilder<SoftwareschmiedeDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> SeedTaskAsync(SoftwareschmiedeDbContext db)
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "SCM" };
        var task = new WorkItem { Id = Guid.NewGuid(), Title = "Aufgabe", ProjectId = project.Id, Status = WorkItemStatus.Started };
        db.AddRange(project, task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static ScmEventBatch Batch(ScmEventObservation observation) => new(new(observation.Reference, observation.ContentKind, null, observation.ObservedAt), [observation]);

    private static ScmEventObservation Observe(string body, string node = "node")
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return new(new(new("fake", "host", "repo", "owner", "repository", "42", "42", "owner", true), ScmReferenceKind.Issue, 7),
            ScmContentKind.IssueComment, node, "comment", date, date, null, "42", "owner", null, null,
            body, "https://host/owner/repository/issues/7", date);
    }
}
