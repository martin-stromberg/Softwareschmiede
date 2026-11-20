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
        fixture.ConfigureIssue(9);
        var projectId = (await RepositoriesAsync(fixture))[0].ProjectId;

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Issue Titel 9", task.Title);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal(WorkItemStatus.Started, task.Status);
        Assert.Equal(9, task.IssueReference?.IssueNumber);
        Assert.Contains("$Softwareschmiede test", task.RequirementDescription);
        Assert.Contains("https://github.com/owner/repo/issues/9", task.RequirementDescription);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.PullRequests).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("PR Titel 5", task.Title);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        var reference = Assert.Single(task.PullRequests);
        Assert.Equal(PullRequestReferenceRole.ReviewSource, reference.Role);
        Assert.Equal(PullRequestProvider.GitHub, reference.Provider);
        Assert.Equal("owner/repo", reference.RepositoryId);
        Assert.Equal(5, reference.PullRequestNumber);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
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
        fixture.ConfigureIssue(9);
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await db.IssueReferences.Where(x => x.IssueNumber == 9).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.ScmIncomingEvents.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single((await db.ScmIncomingEvents.Select(x => x.WorkItemId).Distinct().ToListAsync(cancellationToken: TestContext.Current.CancellationToken)));
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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => Assert.Equal(ScmEventStore.UnassignedDiagnostic, x.Diagnostic));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ein reagierter Trigger-Kommentar auf einer unzugeordneten Referenz löst keine
    /// Aufgabenauflösung aus: Der Ablehnungsgrund verhindert die Trigger-Auswahl, die
    /// Detailabfrage wird gar nicht erst angefragt.</summary>
    [Fact]
    public async Task Monitor_ReactedCommentOnUnassignedReference_CreatesNoTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture, viewerReactions: ["EYES"]);
        fixture.ConfigureIssue(9);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
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

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.Contains("/IssueComment")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
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
        fixture.ConfigureIssue(9);
        var monitor = fixture.Monitor;
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var taskId = (await TasksAsync(fixture)).Single();
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(taskId, (await TasksAsync(fixture)).Single());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.All(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => Assert.Equal(taskId, x.WorkItemId));
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
        var taskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Id);
        Assert.Empty(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, item.WorkItemId);
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
        var taskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, branchName: "Feature/Branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Id);
        Assert.Empty(await db.PullRequestReferences.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, item.WorkItemId);
    }

    /// <summary>Eine Closing-Direktive im PR-Body ordnet das Ereignis der aktiven Aufgabe mit der
    /// referenzierten Issue-Nummer zu, statt eine neue Aufgabe anzulegen.</summary>
    [Fact]
    public async Task Monitor_PullRequestClosingActiveTaskIssue_AssignsWithoutSecondCreation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, issueNumber: 9);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, body: "PR Beschreibung\n\nCloses #9");

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Id);
        var events = await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(events, x => ScmEventStore.Observation(x).Reference.Kind == ScmReferenceKind.PullRequest);
        Assert.All(events, x => Assert.Equal(taskId, x.WorkItemId));
    }

    /// <summary>Der Branch-Match greift nur bei aktiven Aufgaben; trifft der Kontext ausschließlich
    /// eine beendete Aufgabe, entsteht wie bisher eine ReviewSource-Aufgabe.</summary>
    [Fact]
    public async Task Monitor_PullRequestBranchOfEndedTask_CreatesReviewSourceTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var endedTaskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Completed, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var tasks = await db.WorkItems.Include(x => x.PullRequests).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, tasks.Count);
        var created = Assert.Single(tasks, x => x.Id != endedTaskId);
        var reference = Assert.Single(created.PullRequests);
        Assert.Equal(PullRequestReferenceRole.ReviewSource, reference.Role);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(created.Id, item.WorkItemId);
    }

    /// <summary>Zeigen Branch- und Issue-Signal auf unterschiedliche aktive Aufgaben, bleibt das
    /// Ereignis mit Mehrdeutigkeitsdiagnose unzugeordnet; es entsteht weder Aufgabe noch Inbox-Eintrag.</summary>
    [Fact]
    public async Task Monitor_PullRequestConflictingContextSignals_ReportsAmbiguityWithoutTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, branchName: "feature/branch");
        await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, issueNumber: 9);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, body: "PR Beschreibung\n\nCloses #9");

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(2, await db.WorkItems.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        var events = await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(events, x => ScmEventStore.Observation(x).Reference.Kind == ScmReferenceKind.PullRequest);
        var snapshots = await db.ScmContentSnapshots.Where(x => x.ContentKey.Contains("/PullRequest/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        var taskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started, branchName: "feature/branch");
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5, sourceRepositoryId: "fork/repo");

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var tasks = await db.WorkItems.Include(x => x.PullRequests).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, tasks.Count);
        var created = Assert.Single(tasks, x => x.Id != taskId);
        var reference = Assert.Single(created.PullRequests);
        Assert.Equal(PullRequestReferenceRole.ReviewSource, reference.Role);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(created.Id, item.WorkItemId);
    }

    /// <summary>Eine pausierte Aufgabe ist ein gültiger Kontexttreffer: Das Ereignis wird zugeordnet
    /// und bleibt mit der Pausier-Begründung wartend, statt eine Doppelaufgabe zu erzeugen.</summary>
    [Fact]
    public async Task Monitor_PullRequestOnPausedTaskBranch_AssignsAndWaits()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        var taskId = await AddTaskAsync(fixture, repositoryId, WorkItemStatus.Started,
            branchName: "feature/branch", pausedUntilUtc: DateTimeOffset.UtcNow.AddHours(1));
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        ConfigureObservations(fixture);
        ConfigurePullRequest(fixture, 5);

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(taskId, Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Id);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(taskId, item.WorkItemId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("pausiert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    private static async Task<List<Guid>> TasksAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>()
            .WorkItems.Select(x => x.Id).ToListAsync();
    }

    private static async Task<List<GitRepository>> RepositoriesAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().GitRepositories.ToListAsync();
    }

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "TaskAssignment" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Project = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginType = fixture.Repository.ProviderPrefix
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task<Guid> AddTaskAsync(ScmDispatchFixture fixture, Guid repositoryId,
        WorkItemStatus status, string? branchName = null, DateTimeOffset? pausedUntilUtc = null, int? issueNumber = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var projectId = await db.GitRepositories.Where(x => x.Id == repositoryId).Select(x => x.ProjectId).SingleAsync();
        var task = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitRepositoryId = repositoryId,
            Title = "Kontextaufgabe",
            Status = status,
            BranchName = branchName,
            PausedUntilUtc = pausedUntilUtc,
            IssueReference = issueNumber is null ? null : new IssueReference
            {
                Id = Guid.NewGuid(),
                IssueNumber = issueNumber,
                Title = "Issue " + issueNumber
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
        string body = "$Softwareschmiede test", IReadOnlyList<string>? viewerReactions = null)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind switch
                {
                    ScmContentKind.IssueComment => [Observation(request, "C_" + request.Reference.Number, body, time, viewerReactions)],
                    ScmContentKind.Description when descriptionQualified => [Observation(request, "D_" + request.Reference.Number, "$Softwareschmiede Beschreibung", time)],
                    _ => Array.Empty<ScmEventObservation>()
                })));
    }

    private static ScmEventObservation Observation(ScmEventReadRequest request, string nodeId, string body,
        DateTimeOffset time, IReadOnlyList<string>? viewerReactions = null)
        => new(request.Reference, request.ContentKind, nodeId, "42", time, time, null, "U_1", "owner", null, null,
            body, "https://github.com/owner/repo/issues/" + request.Reference.Number, time, viewerReactions);

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
