using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="PullRequestReferenzService"/>.</summary>
public sealed class PullRequestReferenzServiceTests
{
    /// <summary>SaveCreatedAsync persistiert eine neue PR-Referenz an der Aufgabe.</summary>
    [Fact]
    public async Task SaveCreatedAsync_PersistiertPullRequestAnAufgabe()
    {
        using var db = TestDbContextFactory.Create();
        var (projekt, aufgabe) = AddAufgabe(db);
        var service = CreateService(db);

        var saved = await service.SaveCreatedAsync(
            aufgabe.Id,
            "owner/repo",
            new PullRequest(
                42,
                "PR Titel",
                "https://github.com/owner/repo/pull/42",
                "feature/pr",
                PullRequestProvider.GitHub,
                "owner/repo",
                "PR_kw",
                "main",
                "abc123"));

        Assert.Equal(aufgabe.Id, saved.AufgabeId);
        Assert.Equal(PullRequestProvider.GitHub, saved.Provider);
        Assert.Equal("owner/repo", saved.RepositoryId);
        Assert.Equal(42, saved.PullRequestNumber);
        Assert.Equal("abc123", saved.HeadSha);
        Assert.Equal(PullRequestMonitoringPhase.Created, saved.MonitoringPhase);

        var loaded = await service.GetByAufgabeAsync(aufgabe.Id);
        Assert.Single(loaded);
        Assert.Equal(projekt.Id, aufgabe.ProjektId);
    }

    /// <summary>UpdateFromProviderAsync aktualisiert vorhandene Workflow-Runs und fuegt neue hinzu.</summary>
    [Fact]
    public async Task UpdateFromProviderAsync_UpsertedWorkflowRuns()
    {
        using var db = TestDbContextFactory.Create();
        var (_, aufgabe) = AddAufgabe(db);
        var service = CreateService(db);
        var saved = await service.SaveCreatedAsync(
            aufgabe.Id,
            "owner/repo",
            new PullRequest(7, "PR", "https://github.com/owner/repo/pull/7", "feature/pr", RepositoryId: "owner/repo"));

        var status = new PullRequestStatusInfo(
            PullRequestProvider.GitHub,
            "owner/repo",
            7,
            null,
            saved.Url,
            saved.Titel,
            saved.SourceBranch,
            "main",
            "head",
            null,
            PullRequestStatus.Open,
            PullRequestMergeStatus.Mergeable,
            DateTimeOffset.UtcNow);

        await service.UpdateFromProviderAsync(
            saved,
            status,
            [
                new PullRequestWorkflowRunInfo("1", "Build", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false)
            ],
            PullRequestMonitoringPhase.PreMergeSucceeded);

        await service.UpdateFromProviderAsync(
            saved,
            status,
            [
                new PullRequestWorkflowRunInfo("1", "Build aktualisiert", null, "head", "feature/pr", WorkflowRunStatus.Completed, WorkflowRunConclusion.Success, null, null, false),
                new PullRequestWorkflowRunInfo("2", "Test", null, "head", "feature/pr", WorkflowRunStatus.InProgress, WorkflowRunConclusion.Unknown, null, null, false)
            ],
            PullRequestMonitoringPhase.PreMergeRunning);

        var loaded = (await service.GetByAufgabeAsync(aufgabe.Id)).Single();
        Assert.Equal(2, loaded.WorkflowRuns.Count);
        Assert.Contains(loaded.WorkflowRuns, run => run.ProviderRunId == "1" && run.Name == "Build aktualisiert");
        Assert.Contains(loaded.WorkflowRuns, run => run.ProviderRunId == "2");
    }

    private static PullRequestReferenzService CreateService(SoftwareschmiededDbContext db)
        => new(db, TimeProvider.System, NullLogger<PullRequestReferenzService>.Instance);

    private static (Projekt Projekt, Aufgabe Aufgabe) AddAufgabe(SoftwareschmiededDbContext db)
    {
        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "Projekt",
            Beschreibung = "Test",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "Aufgabe",
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        db.SaveChanges();
        return (projekt, aufgabe);
    }
}
