using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Echte SQLite-Persistenz mit Wiederanlauf, Versionsdifferenzen und konkurrierenden Claims.</summary>
public sealed class ScmEventPersistenceTests
{
    /// <summary>Altbestand bleibt bei Migration erhalten; Versionen werden über Neustarts genau einmal aufgenommen.</summary>
    [Fact]
    public async Task MigrationAndReplayPreserveOldTasksAndObserveVersions()
    {
        var path = Path.Combine(Path.GetTempPath(), "scm-db-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            var taskId = Guid.NewGuid();
            await using (var db = new SoftwareschmiededDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20260916094502_AddAufgabePausiertBisUtc", TestContext.Current.CancellationToken);
                var project = new Projekt { Id = Guid.NewGuid(), Name = "Altbestand" };
                db.Projekte.Add(project);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                // Der Altbestand wird roh eingefügt: Das aktuelle Modell enthält neuere
                // Aufgaben-Spalten, die es zu diesem Migrationsstand noch nicht gibt.
                await db.Database.ExecuteSqlAsync($"INSERT INTO Aufgaben (Id, ProjektId, Titel, Status, AusfuehrungsStatus, ErstellungsDatum, RecoveryVersion) VALUES ({taskId}, {project.Id}, 'Bestehende Aufgabe', 0, 0, {DateTimeOffset.UtcNow}, 0)", cancellationToken: TestContext.Current.CancellationToken);
                await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
                var observation = Observe();
                var store = new ScmEventStore(db, new(), TimeProvider.System);
                await store.AcceptBatchAsync(taskId, "source", Batch(observation), ct: TestContext.Current.CancellationToken);
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { UpdatedAt = observation.UpdatedAt.AddHours(1) }), ct: TestContext.Current.CancellationToken);
                Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal("Bestehende Aufgabe", (await db.Aufgaben.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken))!.Titel);
            }
            await using (var db = new SoftwareschmiededDbContext(options))
            {
                var store = new ScmEventStore(db, new(), TimeProvider.System);
                var observation = Observe();
                await store.AcceptBatchAsync(taskId, "source", Batch(observation), ct: TestContext.Current.CancellationToken);
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { Body = "$Softwareschmiede B", LastEditedAt = observation.CreatedAt.AddSeconds(1), EditorId = "42" }), ct: TestContext.Current.CancellationToken);
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { LastEditedAt = observation.CreatedAt.AddSeconds(2), EditorId = "42" }), ct: TestContext.Current.CancellationToken);
                Assert.Equal(3, await db.ScmIncomingEvents.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal(1, await db.ScmContentSnapshots.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>Ein Altbestand-Ereignis ohne die Reaktionseigenschaft deserialisiert zu
    /// <see langword="null"/> und durchläuft die Eingangsprüfung exakt wie vor der Umstellung.</summary>
    [Fact]
    public void ObservationJson_WithoutViewerReactions_Deserializes()
    {
        var observation = Observe();
        var node = JsonNode.Parse(JsonSerializer.Serialize(observation, ScmEventStore.JsonOptions))!.AsObject();
        Assert.True(node.Remove("viewerReactions"));
        var item = new ScmIncomingEvent { EventKey = observation.EventKey, ObservationJson = node.ToJsonString() };
        var restored = ScmEventStore.Observation(item);
        Assert.Equal(observation.EventKey, restored.EventKey);
        Assert.Null(restored.ViewerReactions);
        Assert.False(restored.ViewerHasReacted);
        Assert.Null(ScmEventValidationService.GetRejection(restored));
    }

    /// <summary>Fehlende Identitäten bleiben auflösbar; fremde Bearbeitungen führen zu keinem Auftrag.</summary>
    [Fact]
    public async Task MissingEditorCanResolveLaterWithoutAcceptingForeignEditor()
    {
        await using var db = new SoftwareschmiededDbContext(new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var observation = Observe() with { LastEditedAt = Observe().CreatedAt.AddSeconds(1) };
        var store = new ScmEventStore(db, new(), TimeProvider.System);
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation), ct: TestContext.Current.CancellationToken);
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation with { EditorId = "42" }), ct: TestContext.Current.CancellationToken);
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation with { Body = "$Softwareschmiede fremd", EditorId = "other" }), ct: TestContext.Current.CancellationToken);
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Nur ein Consumer hält die Aufgabenlease; nach Freigabe kann ein anderer übernehmen.</summary>
    [Fact]
    public async Task AtomicLeaseExcludesOtherConsumers()
    {
        await using var db = new SoftwareschmiededDbContext(new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var store = new ScmEventStore(db, new(), TimeProvider.System);
        var taskId = Guid.NewGuid();
        Assert.True(await store.ClaimTaskAsync(taskId, "first", TestContext.Current.CancellationToken));
        Assert.False(await store.ClaimTaskAsync(taskId, "second", TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.ReleaseTaskAsync(taskId, "second", TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.ReleaseTaskAsync(taskId, "first", TestContext.Current.CancellationToken));
        Assert.True(await store.ClaimTaskAsync(taskId, "second", TestContext.Current.CancellationToken));
    }

    private static ScmEventBatch Batch(ScmEventObservation observation) => new(new(observation.Reference, observation.ContentKind, null, observation.ObservedAt), [observation]);
    private static ScmEventObservation Observe()
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return new(new(new("fake", "host", "repo", "owner", "repository", "42", "42", "owner", true), ScmReferenceKind.Issue, 7),
            ScmContentKind.IssueComment, "node", "comment", date, date, null, "42", "owner", null, null,
            "$Softwareschmiede A", "https://host/owner/repository/issues/7", date);
    }
}
