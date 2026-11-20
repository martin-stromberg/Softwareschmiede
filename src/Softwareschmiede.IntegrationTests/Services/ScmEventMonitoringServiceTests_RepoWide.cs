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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshots = await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, x =>
        {
            Assert.Equal(ScmEventStore.UnassignedDiagnostic, x.Diagnostic);
            Assert.False(x.IdentityResolved);
        });
        var checkpoints = await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, item.WorkItemId);
        Assert.Equal(7, ScmEventStore.Observation(item).Reference.Number);
        Assert.Contains(await db.ScmContentSnapshots.Where(x => x.ContentKey.Contains("Issue/42/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Diagnostic == ScmEventStore.UnassignedDiagnostic);
        Assert.DoesNotContain(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Diagnostic == ScmEventStore.AmbiguousAssignmentDiagnostic);
    }

    /// <summary>Ein Kommentar mit Kontoreaktion auf einer aufgabenverknüpften Referenz erzeugt kein
    /// Inbox-Ereignis und keine Zustellung; die Ablehnung bleibt als Snapshot-Diagnose sichtbar,
    /// während ein unberührter Kommentar denselben Zyklus regulär durchläuft.</summary>
    [Fact]
    public async Task Monitor_ReactedComment_StoresDiagnosticWithoutInbox()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddTaskWithIssueAsync(fixture, 7);
        var taskId8 = await AddTaskWithIssueAsync(fixture, 8);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 7),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureObservations(fixture, viewerReactedNumber: 7);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId8, item.WorkItemId);
        var reacted = Assert.Single(await db.ScmContentSnapshots.Where(x => x.ContentKey.EndsWith("C_7")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AlreadyReactedRejection, reacted.Diagnostic);
        Assert.True(reacted.IdentityResolved);
        Assert.Contains(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.SourceKey.StartsWith("test-scm/github.com/R_1/Issue/7/IssueComment") && x.WatermarkUtc != null);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        // Nur das unberührte Ereignis hinterlässt Hostaktionen (Empfangsreaktion + Wartehinweis).
        Assert.Equal(2, fixture.Responses.Count);
    }

    /// <summary>Ein reagierter Kommentar bleibt auch im Folgezyklus ohne Ereignis: Die
    /// Snapshot-Deduplikation überspringt die bekannte, abgelehnte Textversion.</summary>
    [Fact]
    public async Task Monitor_ReactedComment_NextPoll_StaysQuiet()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddTaskWithIssueAsync(fixture, 7);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 7));
        ConfigureObservations(fixture, viewerReactedNumber: 7);
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var reacted = Assert.Single(await db.ScmContentSnapshots.Where(x => x.ContentKey.EndsWith("C_7")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AlreadyReactedRejection, reacted.Diagnostic);
        Assert.Empty(fixture.Responses);
        Assert.Empty(fixture.Prompts);
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
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var events = await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(events);
        Assert.Equal(taskId, events[0].WorkItemId);
        var snapshots = await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(snapshots.Count, snapshots.Select(x => x.ContentKey).Distinct().Count());
    }

    /// <summary>Deaktivierte Repositories und Repositories ohne Ereignis-Plugin werden nicht enumeriert.</summary>
    [Fact]
    public async Task Monitor_InactiveOrNonGitHubRepositories_AreNotEnumerated()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, "https://github.com/owner/inactive", active: false);
        await AddRepositoryAsync(fixture, "https://example.org/other/repo", pluginTyp: "Other.Plugin");

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
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
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(taskId, item.WorkItemId);
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("enumeration/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("Temporary", checkpoint.ErrorCode);
            Assert.NotNull(checkpoint.NextAttemptUtc);
            Assert.True(checkpoint.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Eine Beobachtung des vertrauenswürdigen github-actions-Kontos mit frei stehendem
    /// Keyword löst auf einer unzugeordneten Referenz Aufgabenanlage und Inbox-Ereignis aus.</summary>
    [Fact]
    public async Task Monitor_GitHubActionsAuthoredTrigger_CreatesTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture, authorId: "U_gha", authorLogin: "github-actions[bot]");
        fixture.ConfigureIssue(9);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(9, task.IssueReference!.IssueNumber);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        Assert.Equal("github-actions[bot]", ScmEventStore.Observation(item).AuthorLogin);
        Assert.DoesNotContain(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Diagnostic == "Textversion stammt nicht vom Repository-Owner");
    }

    /// <summary>Ein Fremd-Bot-Login steht nicht auf der Vertrauensliste: Der Kommentar mit gültigem
    /// Trigger wird mit der Owner-Diagnose verworfen, ohne Ereignis und ohne neue Aufgabe.</summary>
    [Fact]
    public async Task Monitor_ForeignBotAuthoredTrigger_StillRejected()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddTaskWithIssueAsync(fixture, 7);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 7));
        ConfigureObservations(fixture, authorId: "U_dep", authorLogin: "dependabot[bot]");

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshot = Assert.Single(await db.ScmContentSnapshots.Where(x => x.ContentKey.EndsWith("C_7")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Textversion stammt nicht vom Repository-Owner", snapshot.Diagnostic);
        Assert.True(snapshot.IdentityResolved);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
    }

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture,
        string url = "https://github.com/owner/repo", bool active = true, string? pluginTyp = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Repo-wide" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Project = project,
            RepositoryUrl = url,
            RepositoryName = "repo",
            PluginType = pluginTyp ?? fixture.Repository.ProviderPrefix,
            Active = active
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task<Guid> AddTaskWithIssueAsync(ScmDispatchFixture fixture, int number)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Repo-wide" };
        var task = new WorkItem
        {
            Id = Guid.NewGuid(),
            Project = project,
            Title = "Linked task",
            Status = WorkItemStatus.New,
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                Project = project,
                RepositoryUrl = "https://github.com/owner/repo",
                RepositoryName = "repo",
                PluginType = fixture.Repository.ProviderPrefix
            },
            IssueReference = new IssueReference { Id = Guid.NewGuid(), IssueNumber = number, Title = "Issue" }
        };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureObservations(ScmDispatchFixture fixture, int? viewerReactedNumber = null,
        string? authorId = "U_1", string? authorLogin = "owner")
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, authorId, authorLogin, null, null,
                    "$Softwareschmiede test", "https://github.com/owner/repo/issues/" + request.Reference.Number, time,
                    request.Reference.Number == viewerReactedNumber ? new[] { "EYES" } : null)] : [])));
    }
}
