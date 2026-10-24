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

    private static void ConfigurePullRequest(ScmDispatchFixture fixture, int number)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadPullRequestAsync(
                It.Is<ScmEventReference>(r => r.Kind == ScmReferenceKind.PullRequest && r.Number == number), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new PullRequest(reference.Number,
                "PR Titel " + reference.Number, "https://github.com/owner/repo/pull/" + reference.Number,
                "feature/branch", PullRequestProvider.GitHub, "owner/repo", "PR_node",
                "main", "sha1", "owner/repo", "https://github.com/owner/repo.git", "refs/heads/feature/branch",
                "PR Beschreibung"));
}
