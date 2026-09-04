using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Persistiert und aktualisiert Pull-Request-Referenzen zu Aufgaben.</summary>
public sealed class PullRequestReferenzService
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(5);
    private readonly SoftwareschmiededDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PullRequestReferenzService> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="PullRequestReferenzService"/>.</summary>
    public PullRequestReferenzService(
        SoftwareschmiededDbContext db,
        TimeProvider timeProvider,
        ILogger<PullRequestReferenzService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Speichert oder aktualisiert einen erstellten Pull Request fuer eine Aufgabe.</summary>
    public async Task<PullRequestReferenz> SaveCreatedAsync(
        Guid aufgabeId,
        string repositoryId,
        PullRequest pullRequest,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var provider = pullRequest.Provider;
        var normalizedRepositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.RepositoryId ?? repositoryId);
        var sourceRepositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.SourceRepositoryId ?? normalizedRepositoryId);

        var entity = await _db.PullRequestReferenzen
            .Include(p => p.WorkflowRuns)
            .FirstOrDefaultAsync(
                p => p.Provider == provider
                     && p.RepositoryId == normalizedRepositoryId
                     && p.PullRequestNumber == pullRequest.Nummer,
                ct);

        if (entity is null)
        {
            entity = new PullRequestReferenz
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                Provider = provider,
                Rolle = PullRequestReferenzRolle.CreatedByTask,
                RepositoryId = normalizedRepositoryId,
                PullRequestNumber = pullRequest.Nummer,
                CreatedUtc = now,
                MonitoringPhase = PullRequestMonitoringPolicy.GetInitialPhase(provider),
                NextCheckUtc = PullRequestMonitoringPolicy.GetInitialPhase(provider) == PullRequestMonitoringPhase.NotMonitored ? null : now
            };
            _db.PullRequestReferenzen.Add(entity);
        }
        else if (entity.AufgabeId != aufgabeId || entity.Rolle != PullRequestReferenzRolle.CreatedByTask)
        {
            throw new InvalidOperationException("Dieser Pull Request ist bereits einer Aufgabe zugeordnet.");
        }

        entity.ProviderPullRequestId = pullRequest.ProviderPullRequestId;
        entity.Url = pullRequest.Url;
        entity.Titel = pullRequest.Titel;
        entity.SourceBranch = pullRequest.BranchName;
        entity.SourceRepositoryId = sourceRepositoryId;
        entity.SourceRepositoryUrl = pullRequest.SourceRepositoryUrl;
        entity.SourceRef = pullRequest.SourceRef;
        entity.TargetBranch = pullRequest.TargetBranch ?? string.Empty;
        entity.HeadSha = pullRequest.HeadSha;
        entity.Status = PullRequestStatus.Open;
        entity.MergeStatus = PullRequestMergeStatus.Unknown;
        entity.LastError = null;

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Liefert Pull Requests einer Aufgabe inklusive Workflow-Runs.</summary>
    public async Task<IReadOnlyList<PullRequestReferenz>> GetByAufgabeAsync(Guid aufgabeId, CancellationToken ct = default)
        => await _db.PullRequestReferenzen
            .AsNoTracking()
            .Include(p => p.WorkflowRuns.OrderBy(w => w.IsPostMerge).ThenBy(w => w.Name))
            .Where(p => p.AufgabeId == aufgabeId)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(ct);

    /// <summary>Liefert PRs, die durch das Monitoring geprueft werden sollen.</summary>
    public async Task<IReadOnlyList<PullRequestReferenz>> GetDueForMonitoringAsync(DateTimeOffset now, int take, CancellationToken ct = default)
        => await _db.PullRequestReferenzen
            .Include(p => p.WorkflowRuns)
            .Where(p => p.MonitoringPhase != PullRequestMonitoringPhase.PostMergeSucceeded
                        && p.MonitoringPhase != PullRequestMonitoringPhase.PostMergeFailed
                        && p.MonitoringPhase != PullRequestMonitoringPhase.Completed
                        && p.MonitoringPhase != PullRequestMonitoringPhase.Failed
                        && p.MonitoringPhase != PullRequestMonitoringPhase.NotMonitored
                        && p.Provider == PullRequestProvider.GitHub
                        && (p.NextCheckUtc == null || p.NextCheckUtc <= now))
            .OrderBy(p => p.LastCheckedUtc ?? DateTimeOffset.MinValue)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>Liefert alle PRs einer Aufgabe fuer einen expliziten Refresh.</summary>
    public async Task<IReadOnlyList<PullRequestReferenz>> GetRefreshableByAufgabeAsync(Guid aufgabeId, CancellationToken ct = default)
        => await _db.PullRequestReferenzen
            .Include(p => p.WorkflowRuns)
            .Where(p => p.AufgabeId == aufgabeId
                        && p.MonitoringPhase != PullRequestMonitoringPhase.NotMonitored
                        && p.Provider == PullRequestProvider.GitHub)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(ct);

    /// <summary>Aktualisiert Statusdaten und Workflow-Runs eines Pull Requests.</summary>
    public async Task UpdateFromProviderAsync(
        PullRequestReferenz entity,
        PullRequestStatusInfo status,
        IReadOnlyList<PullRequestWorkflowRunInfo> workflowRuns,
        PullRequestMonitoringPhase phase,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.ProviderPullRequestId = status.ProviderPullRequestId ?? entity.ProviderPullRequestId;
        entity.Url = status.Url;
        entity.Titel = status.Titel;
        entity.SourceBranch = status.SourceBranch;
        entity.TargetBranch = status.TargetBranch;
        entity.HeadSha = status.HeadSha ?? entity.HeadSha;
        entity.MergeCommitSha = status.MergeCommitSha ?? entity.MergeCommitSha;
        entity.Status = status.Status;
        entity.MergeStatus = status.MergeStatus;
        entity.MonitoringPhase = phase;
        entity.LastCheckedUtc = now;
        entity.NextCheckUtc = now.Add(DefaultRetryDelay);
        entity.LastError = null;

        UpsertWorkflowRuns(entity, workflowRuns, now);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Speichert eine Provider-Unsicherheit ohne die Ueberwachung terminal zu beenden.</summary>
    public async Task SetProviderUncertaintyAsync(
        PullRequestReferenz entity,
        PullRequestMonitoringPhase phase,
        string message,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.MonitoringPhase = phase;
        entity.LastError = message;
        entity.LastCheckedUtc = now;
        entity.NextCheckUtc = now.Add(DefaultRetryDelay);

        _logger.LogWarning(
            "Pull Request {RepositoryId}#{Number}: {Phase}: {Message}",
            entity.RepositoryId,
            entity.PullRequestNumber,
            phase,
            message);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Speichert einen retryfaehigen Abruffehler ohne die bisherige fachliche Phase zu verlieren.</summary>
    public async Task SetRetryableErrorAsync(
        PullRequestReferenz entity,
        string message,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.LastError = message;
        entity.LastCheckedUtc = now;
        entity.NextCheckUtc = now.Add(DefaultRetryDelay);

        _logger.LogWarning(
            "Pull Request {RepositoryId}#{Number}: retryfaehiger Monitoring-Fehler: {Message}",
            entity.RepositoryId,
            entity.PullRequestNumber,
            message);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Speichert einen blockierten oder fehlgeschlagenen Zustand.</summary>
    public async Task SetProblemAsync(
        PullRequestReferenz entity,
        PullRequestMonitoringPhase phase,
        string message,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.MonitoringPhase = phase;
        entity.LastError = message;
        entity.LastCheckedUtc = now;
        entity.NextCheckUtc = phase == PullRequestMonitoringPhase.Blocked
            ? now.Add(TimeSpan.FromMinutes(30))
            : now.Add(DefaultRetryDelay);

        _logger.LogWarning(
            "Pull Request {RepositoryId}#{Number}: {Phase}: {Message}",
            entity.RepositoryId,
            entity.PullRequestNumber,
            phase,
            message);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Speichert eine Monitoring-Phase ohne Fehlerzustand.</summary>
    public async Task SetPhaseAsync(
        PullRequestReferenz entity,
        PullRequestMonitoringPhase phase,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.MonitoringPhase = phase;
        entity.LastError = null;
        entity.LastCheckedUtc = now;
        entity.NextCheckUtc = now.Add(DefaultRetryDelay);
        await _db.SaveChangesAsync(ct);
    }

    private void UpsertWorkflowRuns(
        PullRequestReferenz entity,
        IReadOnlyList<PullRequestWorkflowRunInfo> workflowRuns,
        DateTimeOffset now)
    {
        foreach (var run in workflowRuns.Where(r => !string.IsNullOrWhiteSpace(r.ProviderRunId)))
        {
            var existing = entity.WorkflowRuns.FirstOrDefault(
                w => string.Equals(w.ProviderRunId, run.ProviderRunId, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new PullRequestWorkflowRun
                {
                    Id = Guid.NewGuid(),
                    PullRequestReferenzId = entity.Id,
                    ProviderRunId = run.ProviderRunId
                };
                entity.WorkflowRuns.Add(existing);
                _db.PullRequestWorkflowRuns.Add(existing);
            }

            existing.Name = run.Name;
            existing.Url = run.Url;
            existing.HeadSha = run.HeadSha;
            existing.BranchName = run.BranchName;
            existing.Status = run.Status;
            existing.Conclusion = run.Conclusion;
            existing.StartedAtUtc = run.StartedAtUtc;
            existing.CompletedAtUtc = run.CompletedAtUtc;
            existing.IsPostMerge = run.IsPostMerge;
            existing.UpdatedUtc = now;
        }
    }
}
