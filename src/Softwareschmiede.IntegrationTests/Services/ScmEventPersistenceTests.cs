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
                await db.GetService<IMigrator>().MigrateAsync("20260916094502_AddAufgabePausiertBisUtc");
                var project = new Projekt { Id = Guid.NewGuid(), Name = "Altbestand" };
                db.Projekte.Add(project);
                db.Aufgaben.Add(new Aufgabe { Id = taskId, ProjektId = project.Id, Titel = "Bestehende Aufgabe" });
                await db.SaveChangesAsync();
                await db.Database.MigrateAsync();
                var observation = Observe();
                var store = new ScmEventStore(db, new(), TimeProvider.System);
                await store.AcceptBatchAsync(taskId, "source", Batch(observation));
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { UpdatedAt = observation.UpdatedAt.AddHours(1) }));
                Assert.Single(await db.ScmIncomingEvents.ToListAsync());
                Assert.Equal("Bestehende Aufgabe", (await db.Aufgaben.FindAsync(taskId))!.Titel);
            }
            await using (var db = new SoftwareschmiededDbContext(options))
            {
                var store = new ScmEventStore(db, new(), TimeProvider.System);
                var observation = Observe();
                await store.AcceptBatchAsync(taskId, "source", Batch(observation));
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { Body = "$Softwareschmiede B", LastEditedAt = observation.CreatedAt.AddSeconds(1), EditorId = "42" }));
                await store.AcceptBatchAsync(taskId, "source", Batch(observation with { LastEditedAt = observation.CreatedAt.AddSeconds(2), EditorId = "42" }));
                Assert.Equal(3, await db.ScmIncomingEvents.CountAsync());
                Assert.Equal(1, await db.ScmContentSnapshots.CountAsync());
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>Fehlende Identitäten bleiben auflösbar; fremde Bearbeitungen führen zu keinem Auftrag.</summary>
    [Fact]
    public async Task MissingEditorCanResolveLaterWithoutAcceptingForeignEditor()
    {
        await using var db = new SoftwareschmiededDbContext(new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var observation = Observe() with { LastEditedAt = Observe().CreatedAt.AddSeconds(1) };
        var store = new ScmEventStore(db, new(), TimeProvider.System);
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation with { EditorId = "42" }));
        Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        await store.AcceptBatchAsync(Guid.NewGuid(), "source", Batch(observation with { Body = "$Softwareschmiede fremd", EditorId = "other" }));
        Assert.Single(await db.ScmIncomingEvents.ToListAsync());
    }

    /// <summary>Nur ein Consumer hält die Aufgabenlease; nach Freigabe kann ein anderer übernehmen.</summary>
    [Fact]
    public async Task AtomicLeaseExcludesOtherConsumers()
    {
        await using var db = new SoftwareschmiededDbContext(new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        var store = new ScmEventStore(db, new(), TimeProvider.System);
        var taskId = Guid.NewGuid();
        Assert.True(await store.ClaimTaskAsync(taskId, "first", default));
        Assert.False(await store.ClaimTaskAsync(taskId, "second", default));
        Assert.Equal(0, await store.ReleaseTaskAsync(taskId, "second", default));
        Assert.Equal(1, await store.ReleaseTaskAsync(taskId, "first", default));
        Assert.True(await store.ClaimTaskAsync(taskId, "second", default));
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
