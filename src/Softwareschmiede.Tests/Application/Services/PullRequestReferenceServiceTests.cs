using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="PullRequestReferenceService"/>.</summary>
public sealed class PullRequestReferenceServiceTests
{
    /// <summary>SaveCreatedAsync persistiert eine neue PR-Referenz an der Aufgabe.</summary>
    [Fact]
    public async Task SaveCreatedAsync_PersistedPullRequestAnWorkItem()
    {
        using var db = TestDbContextFactory.Create();
        var (project, workItem) = AddWorkItem(db);
        var service = CreateService(db);

        var saved = await service.SaveCreatedAsync(workItem.Id, "owner/repo", new PullRequest(
                42,
                "PR Titel",
                "https://github.com/owner/repo/pull/42",
                "feature/pr",
                PullRequestProvider.GitHub,
                "owner/repo",
                "PR_kw",
                "main",
                "abc123"), TestContext.Current.CancellationToken);

        Assert.Equal(workItem.Id, saved.WorkItemId);
        Assert.Equal(PullRequestProvider.GitHub, saved.Provider);
        Assert.Equal("owner/repo", saved.RepositoryId);
        Assert.Equal(42, saved.PullRequestNumber);
        Assert.Equal("abc123", saved.HeadSha);
        Assert.Equal(PullRequestMonitoringPhase.Created, saved.MonitoringPhase);

        var loaded = await service.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        Assert.Single(loaded);
        Assert.Equal(project.Id, workItem.ProjectId);
    }

    /// <summary>UpdateFromProviderAsync aktualisiert vorhandene Workflow-Runs und fuegt neue hinzu.</summary>
    [Fact]
    public async Task UpdateFromProviderAsync_UpsertedWorkflowRuns()
    {
        using var db = TestDbContextFactory.Create();
        var (_, workItem) = AddWorkItem(db);
        var service = CreateService(db);
        var saved = await service.SaveCreatedAsync(workItem.Id, "owner/repo", new PullRequest(7, "PR", "https://github.com/owner/repo/pull/7", "feature/pr", RepositoryId: "owner/repo"), TestContext.Current.CancellationToken);

        var status = new PullRequestStatusInfo(
            PullRequestProvider.GitHub,
            "owner/repo",
            7,
            null,
            saved.Url,
            saved.Title,
            saved.SourceBranch,
            "main",
            "head",
            null,
            PullRequestStatus.Open,
            PullRequestMergeStatus.Mergeable,
            DateTimeOffset.UtcNow);

        await service.UpdateFromProviderAsync(saved, status, [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ], PullRequestMonitoringPhase.PreMergeSucceeded, TestContext.Current.CancellationToken);

        await service.UpdateFromProviderAsync(saved, status, [
                new PullRequestWorkflowRunInfo("1", "Build aktualisiert", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false),
                new PullRequestWorkflowRunInfo("2", "Test", null, "head", "feature/pr", WorkflowRunStatus.InProgress, WorkflowRunConclusion.Unknown, null, null, false)
            ], PullRequestMonitoringPhase.PreMergeRunning, TestContext.Current.CancellationToken);

        var loaded = (await service.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken)).Single();
        Assert.Equal(2, loaded.WorkflowRuns.Count);
        Assert.Contains(loaded.WorkflowRuns, run => run.ProviderRunId == "1" && run.Name == "Build aktualisiert");
        Assert.Contains(loaded.WorkflowRuns, run => run.ProviderRunId == "2");
    }

    private static PullRequestReferenceService CreateService(SoftwareschmiedeDbContext db)
        => new(db, TimeProvider.System, NullLogger<PullRequestReferenceService>.Instance);

    private static (Project Project, WorkItem WorkItem) AddWorkItem(SoftwareschmiedeDbContext db)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Projekt",
            Description = "Test",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Aufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Projects.Add(project);
        db.WorkItems.Add(workItem);
        db.SaveChanges();
        return (project, workItem);
    }
}
