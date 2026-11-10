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

/// <summary>Aufgabenanlage und -wiederverwendung aus qualifizierten Ereignissen unzugeordneter Referenzen.</summary>
public sealed class ScmEventMonitoringServiceTests_TaskAssignment
{
    /// <summary>Ein qualifizierter Kommentar zu einem neuen Issue legt genau eine Aufgabe mit
    /// Issue-Referenz an; das Ereignis läuft mit dieser Aufgabe in die Inbox.</summary>
    [Fact]
    public async Task Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        var projectId = (await RepositoriesAsync(fixture))[0].ProjektId;

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync());
        Assert.Equal("Issue Titel 9", task.Titel);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal(projectId, task.ProjektId);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.Equal(9, task.IssueReferenz?.IssueNummer);
        Assert.Contains("$Softwareschmiede test", task.AnforderungsBeschreibung);
        Assert.Contains("https://github.com/owner/repo/issues/9", task.AnforderungsBeschreibung);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(task.Id, item.AufgabeId);
    }

    /// <summary>Ein qualifiziertes Ereignis zu einem Pull Request legt eine Aufgabe mit
    /// ReviewSource-Referenz an.</summary>
    [Fact]
    public async Task Monitor_QualifiedPullRequestEvent_CreatesReviewSourceTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.PullRequests).ToListAsync());
        Assert.Equal("PR Titel 5", task.Titel);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        var reference = Assert.Single(task.PullRequests);
        Assert.Equal(PullRequestReferenzRolle.ReviewSource, reference.Rolle);
        Assert.Equal(PullRequestProvider.GitHub, reference.Provider);
        Assert.Equal("owner/repo", reference.RepositoryId);
        Assert.Equal(5, reference.PullRequestNumber);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(task.Id, item.AufgabeId);
    }

    /// <summary>Wiederholte Läufe und Folgebatches derselben Referenz erzeugen keine zweite Aufgabe.</summary>
    [Fact]
    public async Task Monitor_RepeatedPollsAndBatches_CreateSingleTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        // Beschreibung und Kommentar sind beide qualifiziert.
        ConfigureObservations(fixture, descriptionQualified: true);
        ConfigureIssue(fixture, 9);
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();
        await monitor.RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Single(await db.Aufgaben.ToListAsync());
        Assert.Single(await db.IssueReferenzen.Where(x => x.IssueNummer == 9).ToListAsync());
        Assert.Equal(2, await db.ScmIncomingEvents.CountAsync());
        Assert.Single((await db.ScmIncomingEvents.Select(x => x.AufgabeId).Distinct().ToListAsync()));
    }

    /// <summary>Ein Ereignis ohne Trigger erzeugt weder Aufgabe noch Inbox-Eintrag; der
    /// Detailabruf wird gar nicht erst angefragt.</summary>
    [Fact]
    public async Task Monitor_UnqualifiedEvent_KeepsUnassignedWithoutTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture, body: "Kommentar ohne Trigger");

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync());
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        Assert.All(await db.ScmContentSnapshots.ToListAsync(),
            x => Assert.Equal(ScmEventStore.UnassignedDiagnostic, x.Diagnostic));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Eine remote verschwundene Referenz bleibt unzugeordnet, ohne dass eine Aufgabe entsteht.</summary>
    [Fact]
    public async Task Monitor_VanishedReference_StaysUnassigned()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync());
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        Assert.All(await db.ScmContentSnapshots.ToListAsync(),
            x => Assert.Equal(ScmEventStore.UnassignedDiagnostic, x.Diagnostic));
    }

    /// <summary>Ein Fehler beim Detailabruf geht in den Quell-Checkpoint-Backoff; es entsteht keine Aufgabe.</summary>
    [Fact]
    public async Task Monitor_ResolutionFailure_RecordsCheckpointFailure()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Verbindung unterbrochen."));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync());
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.Contains("/IssueComment")).ToListAsync());
        Assert.Equal("Temporary", checkpoint.ErrorCode);
        Assert.NotNull(checkpoint.NextAttemptUtc);
    }

    /// <summary>Eine nachträglich verknüpfte Aufgabe wird wiederverwendet; es entsteht keine zweite.</summary>
    [Fact]
    public async Task Monitor_ExistingTask_IsReusedWithoutSecondCreation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        var monitor = Monitor(fixture);
        await monitor.RunOnceAsync();

        var taskId = (await TasksAsync(fixture)).Single();
        await monitor.RunOnceAsync();

        Assert.Equal(taskId, (await TasksAsync(fixture)).Single());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.All(await db.ScmIncomingEvents.ToListAsync(), x => Assert.Equal(taskId, x.AufgabeId));
    }

    /// <summary>Ein Ereignis auf einem Pull Request, dessen Head-Branch dem Branch einer aktiven
    /// Aufgabe ohne PullRequestReferenz entspricht, wird dieser Aufgabe zugeordnet — ohne Zweitanlage
    /// und ohne implizit persistierte Referenz.</summary>
    [Fact]
    public async Task Monitor_PullRequestOnActiveTaskBranch_AssignsWithoutSecondCreation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.Aufgaben.ToListAsync()).Id);
        Assert.Empty(await db.PullRequestReferenzen.ToListAsync());
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
    }

    /// <summary>Ein nur in der Groß-/Kleinschreibung abweichender PR-Head-Branch trifft die aktive
    /// Aufgabe weiterhin (OrdinalIgnoreCase). Guard gegen die eingeschränkte Kontextabfrage in
    /// <see cref="ScmEventTaskResolver"/>: der Befund war ein Effizienzproblem ohne Fehlverhalten,
    /// daher ist der Test als Semantik-Sicherung grün und schlägt nur fehl, wenn der
    /// Branchvergleich künftig case-sensitiv in die Datenbankabfrage wandert.</summary>
    [Fact]
    public async Task Monitor_PullRequestBranchWithDifferentCasing_AssignsWithoutSecondCreation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, branchName: "Feature/Branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.Aufgaben.ToListAsync()).Id);
        Assert.Empty(await db.PullRequestReferenzen.ToListAsync());
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
    }

    /// <summary>Eine Closing-Direktive im PR-Body ordnet das Ereignis der aktiven Aufgabe mit der
    /// referenzierten Issue-Nummer zu, statt eine neue Aufgabe anzulegen.</summary>
    [Fact]
    public async Task Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, issueNummer: 9);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, body: "PR Beschreibung\n\nCloses #9");

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.Aufgaben.ToListAsync()).Id);
        var events = await db.ScmIncomingEvents.ToListAsync();
        Assert.Contains(events, x => ScmEventStore.Observation(x).Reference.Kind == ScmReferenceKind.PullRequest);
        Assert.All(events, x => Assert.Equal(taskId, x.AufgabeId));
    }

    /// <summary>Der Branch-Match greift nur bei aktiven Aufgaben; trifft der Kontext ausschließlich
    /// eine beendete Aufgabe, entsteht wie bisher eine ReviewSource-Aufgabe.</summary>
    [Fact]
    public async Task Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var endedTaskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Beendet, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var tasks = await db.Aufgaben.Include(x => x.PullRequests).ToListAsync();
        Assert.Equal(2, tasks.Count);
        var created = Assert.Single(tasks, x => x.Id != endedTaskId);
        var reference = Assert.Single(created.PullRequests);
        Assert.Equal(PullRequestReferenzRolle.ReviewSource, reference.Rolle);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(created.Id, item.AufgabeId);
    }

    /// <summary>Zeigen Branch- und Issue-Signal auf unterschiedliche aktive Aufgaben, bleibt das
    /// Ereignis mit Mehrdeutigkeitsdiagnose unzugeordnet; es entsteht weder Aufgabe noch Inbox-Eintrag.</summary>
    [Fact]
    public async Task Monitor_PullRequestConflictingContextSignals_ReportsAmbiguityWithoutTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, branchName: "feature/branch");
        await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, issueNummer: 9);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, body: "PR Beschreibung\n\nCloses #9");

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal(2, await db.Aufgaben.CountAsync());
        var events = await db.ScmIncomingEvents.ToListAsync();
        Assert.DoesNotContain(events, x => ScmEventStore.Observation(x).Reference.Kind == ScmReferenceKind.PullRequest);
        var snapshots = await db.ScmContentSnapshots.Where(x => x.ContentKey.Contains("/PullRequest/")).ToListAsync();
        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, x => Assert.Equal(ScmEventStore.AmbiguousAssignmentDiagnostic, x.Diagnostic));
    }

    /// <summary>Ein Fork-PR mit gleichem Branchnamen ist kein Kontexttreffer; es entsteht eine
    /// ReviewSource-Aufgabe.</summary>
    [Fact]
    public async Task Monitor_ForkedPullRequestWithSameBranch_CreatesReviewSourceTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, sourceRepositoryId: "fork/repo");

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var tasks = await db.Aufgaben.Include(x => x.PullRequests).ToListAsync();
        Assert.Equal(2, tasks.Count);
        var created = Assert.Single(tasks, x => x.Id != taskId);
        var reference = Assert.Single(created.PullRequests);
        Assert.Equal(PullRequestReferenzRolle.ReviewSource, reference.Rolle);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(created.Id, item.AufgabeId);
    }

    /// <summary>Eine pausierte Aufgabe ist ein gültiger Kontexttreffer: Das Ereignis wird zugeordnet
    /// und bleibt mit der Pausier-Begründung wartend, statt eine Doppelaufgabe zu erzeugen.</summary>
    [Fact]
    public async Task Monitor_PullRequestOnPausedTaskBranch_AssignsAndWaits()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, AufgabeStatus.Gestartet,
            branchName: "feature/branch", pausiertBisUtc: DateTimeOffset.UtcNow.AddHours(1));
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.Aufgaben.ToListAsync()).Id);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("pausiert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task<List<Guid>> TasksAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>()
            .Aufgaben.Select(x => x.Id).ToListAsync();
    }

    private static async Task<List<GitRepository>> RepositoriesAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().GitRepositories.ToListAsync();
    }

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "TaskAssignment" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginTyp = fixture.Repository.ProviderPrefix
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task<Guid> AddTaskAsync(ScmDispatchFixture fixture, Guid repositoryId,
        AufgabeStatus status, string? branchName = null, DateTimeOffset? pausiertBisUtc = null, int? issueNummer = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var projektId = await db.GitRepositories.Where(x => x.Id == repositoryId).Select(x => x.ProjektId).SingleAsync();
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            GitRepositoryId = repositoryId,
            Titel = "Kontextaufgabe",
            Status = status,
            BranchName = branchName,
            PausiertBisUtc = pausiertBisUtc,
            IssueReferenz = issueNummer is null ? null : new IssueReferenz
            {
                Id = Guid.NewGuid(),
                IssueNummer = issueNummer,
                Titel = "Issue " + issueNummer
            }
        };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureObservations(ScmDispatchFixture fixture, bool descriptionQualified = false,
        string body = "$Softwareschmiede test")
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind switch
                {
                    ScmContentKind.IssueComment => [Observation(request, "C_" + request.Reference.Number, body, time)],
                    ScmContentKind.Description when descriptionQualified => [Observation(request, "D_" + request.Reference.Number, "$Softwareschmiede Beschreibung", time)],
                    _ => Array.Empty<ScmEventObservation>()
                })));
    }

    private static ScmEventObservation Observation(ScmEventReadRequest request, string nodeId, string body, DateTimeOffset time)
        => new(request.Reference, request.ContentKind, nodeId, "42", time, time, null, "U_1", "owner", null, null,
            body, "https://github.com/owner/repo/issues/" + request.Reference.Number, time);

    private static void ConfigureIssue(ScmDispatchFixture fixture, int number)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.Is<ScmEventReference>(r => r.Kind == ScmReferenceKind.Issue && r.Number == number), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number));

    private static void ConfigurePullRequest(ScmDispatchFixture fixture, int number,
        string branchName = "feature/branch", string body = "PR Beschreibung", string sourceRepositoryId = "owner/repo")
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadPullRequestAsync(
                It.Is<ScmEventReference>(r => r.Kind == ScmReferenceKind.PullRequest && r.Number == number), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new PullRequest(reference.Number,
                "PR Titel " + reference.Number, "https://github.com/owner/repo/pull/" + reference.Number,
                branchName, PullRequestProvider.GitHub, "owner/repo", "PR_node",
                "main", "sha1", sourceRepositoryId, "https://github.com/owner/repo.git", "refs/heads/" + branchName,
                body));
}
