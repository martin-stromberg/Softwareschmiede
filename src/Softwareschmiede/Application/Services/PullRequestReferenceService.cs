using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Persistiert und aktualisiert Pull-Request-Referenzen zu Aufgaben.</summary>
public sealed class PullRequestReferenceService
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(5);
    private readonly SoftwareschmiedeDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PullRequestReferenceService> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="PullRequestReferenceService"/>.</summary>
    public PullRequestReferenceService(
        SoftwareschmiedeDbContext db,
        TimeProvider timeProvider,
        ILogger<PullRequestReferenceService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Speichert oder aktualisiert einen erstellten Pull Request fuer eine Aufgabe.</summary>
    public async Task<PullRequestReference> SaveCreatedAsync(
        Guid workItemId,
        string repositoryId,
        PullRequest pullRequest,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var provider = pullRequest.Provider;
        var normalizedRepositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.RepositoryId ?? repositoryId);
        var sourceRepositoryId = PullRequestRepositoryId.Normalize(provider, pullRequest.SourceRepositoryId ?? normalizedRepositoryId);

        var entity = await _db.PullRequestReferences
            .Include(p => p.WorkflowRuns)
            .FirstOrDefaultAsync(
                p => p.Provider == provider
                     && p.RepositoryId == normalizedRepositoryId
                     && p.PullRequestNumber == pullRequest.Number,
                ct);

        if (entity is null)
        {
            entity = new PullRequestReference
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
                Provider = provider,
                Role = PullRequestReferenceRole.CreatedByTask,
                RepositoryId = normalizedRepositoryId,
                PullRequestNumber = pullRequest.Number,
                CreatedUtc = now,
                MonitoringPhase = PullRequestMonitoringPolicy.GetInitialPhase(provider),
                NextCheckUtc = PullRequestMonitoringPolicy.GetInitialPhase(provider) == PullRequestMonitoringPhase.NotMonitored ? null : now
            };
            _db.PullRequestReferences.Add(entity);
        }
        else if (entity.WorkItemId != workItemId || entity.Role != PullRequestReferenceRole.CreatedByTask)
        {
            throw new InvalidOperationException("Dieser Pull Request ist bereits einer Aufgabe zugeordnet.");
        }

        entity.ProviderPullRequestId = pullRequest.ProviderPullRequestId;
        entity.Url = pullRequest.Url;
        entity.Title = pullRequest.Title;
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

    /// <summary>Sucht eine Referenz über ihren fachlichen Schlüssel (Provider + normalisierte
    /// Repository-Id + Nummer). Provider-Ableitung aus dem Plugin-Prefix und Normalisierung laufen
    /// intern über dieselbe Auflösung wie <see cref="SaveReportedAsync"/>.</summary>
    /// <returns>Die gefundene Referenz oder <see langword="null"/>.</returns>
    public async Task<PullRequestReference?> FindByKeyAsync(
        string? pluginPrefix,
        string repositoryId,
        int pullRequestNumber,
        CancellationToken ct = default)
        => await FindByKeyAsync(ResolveKey(pluginPrefix, repositoryId), pullRequestNumber, ct);

    /// <summary>Speichert oder aktualisiert einen Pull Request, den der KI-Agent selbst angelegt und im
    /// Abschluss gemeldet hat (<c>pullRequestNumber</c> in <c>complete.json</c>). Idempotent: Eine bereits
    /// derselben Aufgabe zugeordnete Referenz wird ergänzt; eine fremd zugeordnete Referenz wird nicht
    /// übernommen und mit <see langword="null"/> quittiert.</summary>
    /// <returns>Die verknüpfte Referenz oder <see langword="null"/>, wenn der Pull Request bereits einer
    /// anderen Aufgabe zugeordnet ist.</returns>
    public async Task<PullRequestReference?> SaveReportedAsync(
        Guid workItemId,
        string? pluginPrefix,
        string repositoryId,
        int pullRequestNumber,
        string? url,
        CancellationToken ct = default)
    {
        if (pullRequestNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber), "Die Pull-Request-Nummer muss positiv sein.");

        var now = _timeProvider.GetUtcNow();
        // Ereignisquellen liefert derzeit nur das GitHub-Plugin; unbekannte Prefixe werden
        // bewusst als GitHub-Referenz gespeichert, damit die Abschlussüberwachung sie prüfen kann.
        var key = ResolveKey(pluginPrefix, repositoryId);

        var entity = await FindByKeyAsync(key, pullRequestNumber, ct);

        if (entity is null)
        {
            entity = new PullRequestReference
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
                Provider = key.Provider,
                Role = PullRequestReferenceRole.CreatedByTask,
                RepositoryId = key.RepositoryId,
                PullRequestNumber = pullRequestNumber,
                CreatedUtc = now,
                MonitoringPhase = PullRequestMonitoringPolicy.GetInitialPhase(key.Provider),
                NextCheckUtc = PullRequestMonitoringPolicy.GetInitialPhase(key.Provider) == PullRequestMonitoringPhase.NotMonitored ? null : now
            };
            _db.PullRequestReferences.Add(entity);
        }
        else if (entity.WorkItemId != workItemId)
        {
            _logger.LogWarning(
                "Pull Request {RepositoryId}#{Number} wurde gemeldet, ist aber bereits Aufgabe {AufgabeId} zugeordnet; Meldung an Aufgabe {MeldendeAufgabeId} wird ignoriert.",
                key.RepositoryId, pullRequestNumber, entity.WorkItemId, workItemId);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(url))
            entity.Url = url;
        if (entity.Status == PullRequestStatus.Unknown)
            entity.Status = PullRequestStatus.Open;

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Liefert Pull Requests einer Aufgabe inklusive Workflow-Runs.</summary>
    public async Task<IReadOnlyList<PullRequestReference>> GetByWorkItemAsync(Guid workItemId, CancellationToken ct = default)
        => await _db.PullRequestReferences
            .AsNoTracking()
            .Include(p => p.WorkflowRuns.OrderBy(w => w.IsPostMerge).ThenBy(w => w.Name))
            .Where(p => p.WorkItemId == workItemId)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(ct);

    /// <summary>Liefert PRs, die durch das Monitoring geprueft werden sollen.</summary>
    public async Task<IReadOnlyList<PullRequestReference>> GetDueForMonitoringAsync(DateTimeOffset now, int take, CancellationToken ct = default)
        => await _db.PullRequestReferences
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
    public async Task<IReadOnlyList<PullRequestReference>> GetRefreshableByWorkItemAsync(Guid workItemId, CancellationToken ct = default)
        => await _db.PullRequestReferences
            .Include(p => p.WorkflowRuns)
            .Where(p => p.WorkItemId == workItemId
                        && p.MonitoringPhase != PullRequestMonitoringPhase.NotMonitored
                        && p.Provider == PullRequestProvider.GitHub)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(ct);

    /// <summary>Aktualisiert Statusdaten und Workflow-Runs eines Pull Requests.</summary>
    public async Task UpdateFromProviderAsync(
        PullRequestReference entity,
        PullRequestStatusInfo status,
        IReadOnlyList<PullRequestWorkflowRunInfo> workflowRuns,
        PullRequestMonitoringPhase phase,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        entity.ProviderPullRequestId = status.ProviderPullRequestId ?? entity.ProviderPullRequestId;
        entity.Url = status.Url;
        entity.Title = status.Title;
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
        PullRequestReference entity,
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
        PullRequestReference entity,
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
        PullRequestReference entity,
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
        PullRequestReference entity,
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

    /// <summary>Leitet Provider und normalisierte Repository-Id aus Plugin-Prefix und Rohwert ab;
    /// unbekannte Prefixe gelten als GitHub-Referenz.</summary>
    private static PullRequestKey ResolveKey(string? pluginPrefix, string repositoryId)
    {
        var provider = PullRequestProviderDescriptor.TryGetProviderForPluginPrefix(pluginPrefix)
            ?? PullRequestProvider.GitHub;
        return new(provider, PullRequestRepositoryId.Normalize(provider, repositoryId));
    }

    private async Task<PullRequestReference?> FindByKeyAsync(PullRequestKey key, int pullRequestNumber, CancellationToken ct)
        => await _db.PullRequestReferences
            .FirstOrDefaultAsync(
                p => p.Provider == key.Provider
                     && p.RepositoryId == key.RepositoryId
                     && p.PullRequestNumber == pullRequestNumber,
                ct);

    /// <summary>Fachlicher Schlüssel einer Pull-Request-Referenz (abgeleiteter Provider +
    /// normalisierte Repository-Id); die Pull-Request-Nummer bleibt ein separater Parameter
    /// der <c>FindByKeyAsync</c>-Überladung.</summary>
    private sealed record PullRequestKey(PullRequestProvider Provider, string RepositoryId);

    private void UpsertWorkflowRuns(
        PullRequestReference entity,
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
                    PullRequestReferenceId = entity.Id,
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
