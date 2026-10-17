using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Repository-weite Erkennung: aktive Projekt-Repositories werden auch ohne Aufgabe enumeriert.</summary>
public sealed class ScmEventMonitoringServiceTests_RepoWide
{
    /// <summary>Ein aktives Projekt-Repository ohne Aufgabe wird enumeriert; Beobachtungen landen als
    /// nicht zuordenbare Snapshots ohne Inbox-Eintrag.</summary>
    [Fact]
    public async Task Monitor_ActiveRepositoryWithoutTask_EnumeratesAndStoresSnapshotsWithoutInbox()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        var snapshots = await db.ScmContentSnapshots.ToListAsync();
        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, x =>
        {
            Assert.Equal(ScmEventStore.UnassignedDiagnostic, x.Diagnostic);
            Assert.False(x.IdentityResolved);
        });
        var checkpoints = await db.ScmObservationCheckpoints.ToListAsync();
        Assert.Contains(checkpoints, x => x.SourceKey.StartsWith("enumeration/") && x.WatermarkUtc != null && x.Diagnostic == null);
        Assert.Contains(checkpoints, x => x.SourceKey.StartsWith("test-scm/github.com/R_1/Issue/9/")
            && x.Diagnostic == ScmEventStore.UnassignedDiagnostic);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Eine enumerierte Referenz mit zugeordneter Aufgabe durchläuft unverändert Validierung und Inbox.</summary>
    [Fact]
    public async Task Monitor_EnumeratedReferenceWithTask_EntersInboxUnchanged()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var taskId = await AddTaskWithIssueAsync(fixture, 7);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 7),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 42));
        ConfigureObservations(fixture);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
        Assert.Equal(7, ScmEventStore.Observation(item).Reference.Number);
        Assert.Contains(await db.ScmContentSnapshots.Where(x => x.ContentKey.Contains("Issue/42/")).ToListAsync(),
            x => x.Diagnostic == ScmEventStore.UnassignedDiagnostic);
        Assert.DoesNotContain(await db.ScmObservationCheckpoints.ToListAsync(),
            x => x.Diagnostic == ScmEventStore.AmbiguousAssignmentDiagnostic);
    }

    /// <summary>Wiederholte Polls deduplizieren über Snapshot/EventKey und schreiben den Enumerations-Watermark fort.</summary>
    [Fact]
    public async Task Monitor_RepeatedPolls_ProduceNoDuplicates()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var taskId = await AddTaskWithIssueAsync(fixture, 7);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 7));
        ConfigureObservations(fixture);
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();
        await monitor.RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var events = await db.ScmIncomingEvents.ToListAsync();
        Assert.Single(events);
        Assert.Equal(taskId, events[0].AufgabeId);
        var snapshots = await db.ScmContentSnapshots.ToListAsync();
        Assert.Equal(snapshots.Count, snapshots.Select(x => x.ContentKey).Distinct().Count());
    }

    /// <summary>Deaktivierte Repositories und Repositories ohne Ereignis-Plugin werden nicht enumeriert.</summary>
    [Fact]
    public async Task Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, "https://github.com/owner/inactive", aktiv: false);
        await AddRepositoryAsync(fixture, "https://example.org/other/repo", pluginTyp: "Other.Plugin");

        await Monitor(fixture).RunOnceAsync();

        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmObservationCheckpoints.ToListAsync());
    }

    /// <summary>Ein Enumerationsfehler geht in den eigenen Backoff-Checkpoint und blockiert
    /// aufgabenverknüpfte Referenzen desselben Repositories nicht.</summary>
    [Fact]
    public async Task Monitor_EnumerationFailure_UsesBackoffWithoutBlockingLinkedReferences()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var taskId = await AddTaskWithIssueAsync(fixture, 7);
        ConfigureObservations(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Verbindung unterbrochen."));
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            Assert.Equal(taskId, item.AufgabeId);
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("enumeration/")).ToListAsync());
            Assert.Equal("Temporary", checkpoint.ErrorCode);
            Assert.NotNull(checkpoint.NextAttemptUtc);
            Assert.True(checkpoint.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        await monitor.RunOnceAsync();
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture,
        string url = "https://github.com/owner/repo", bool aktiv = true, string? pluginTyp = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Repo-wide" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = url,
            RepositoryName = "repo",
            PluginTyp = pluginTyp ?? fixture.Repository.ProviderPrefix,
            Aktiv = aktiv
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task<Guid> AddTaskWithIssueAsync(ScmDispatchFixture fixture, int number)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Repo-wide" };
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            Titel = "Linked task",
            Status = AufgabeStatus.Neu,
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                Projekt = project,
                RepositoryUrl = "https://github.com/owner/repo",
                RepositoryName = "repo",
                PluginTyp = fixture.Repository.ProviderPrefix
            },
            IssueReferenz = new IssueReferenz { Id = Guid.NewGuid(), IssueNummer = number, Titel = "Issue" }
        };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureObservations(ScmDispatchFixture fixture)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, "U_1", "owner", null, null,
                    "$Softwareschmiede test", "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }
}
