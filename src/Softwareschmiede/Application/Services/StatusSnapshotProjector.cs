using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Read-only-Projektion der DB-Bestände (<see cref="SoftwareschmiedeDbContext"/>) plus
/// Laufzeit-Singletons (<see cref="IRunningAutomationStatusSource"/>,
/// <see cref="IRunningAutomationsCount"/>, <see cref="IApplicationVersionProvider"/>) in das
/// geteilte <see cref="StatusSnapshot"/>-DTO. Enthält ausschließlich Anzeigefelder — keine
/// Geheimnisse oder Tokens.
/// </summary>
public sealed class StatusSnapshotProjector
{
    /// <summary>Maximale Anzahl projizierter SCM-Ereignisse pro Snapshot.</summary>
    public const int MaxScmEvents = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRunningAutomationStatusSource _statusSource;
    private readonly IRunningAutomationsCount _automationsCount;
    private readonly IApplicationVersionProvider _versionProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StatusSnapshotProjector> _logger;

    /// <summary>Erstellt den Projektor.</summary>
    /// <param name="scopeFactory">Die Scope-Factory für den DB-Zugriff.</param>
    /// <param name="statusSource">Die Live-Laufstatusquelle.</param>
    /// <param name="automationsCount">Der Zähler fachlich laufender Automatisierungen.</param>
    /// <param name="versionProvider">Der Versionsanbieter der Installation.</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    /// <param name="logger">Der Logger.</param>
    public StatusSnapshotProjector(
        IServiceScopeFactory scopeFactory,
        IRunningAutomationStatusSource statusSource,
        IRunningAutomationsCount automationsCount,
        IApplicationVersionProvider versionProvider,
        TimeProvider timeProvider,
        ILogger<StatusSnapshotProjector> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(statusSource);
        ArgumentNullException.ThrowIfNull(automationsCount);
        ArgumentNullException.ThrowIfNull(versionProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _statusSource = statusSource;
        _automationsCount = automationsCount;
        _versionProvider = versionProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Projiziert den aktuellen App-Zustand in einen <see cref="StatusSnapshot"/>.</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Der projizierte Snapshot.</returns>
    public async Task<StatusSnapshot> CreateAsync(CancellationToken ct = default)
    {
        var nowUtc = _timeProvider.GetUtcNow();
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var appSettings = scope.ServiceProvider.GetRequiredService<AppSettingService>();

        var instanceId = await appSettings.GetOrCreateRemoteStatusInstanceIdAsync(ct).ConfigureAwait(false);
        var instanceName = await appSettings.GetRemoteStatusInstanceNameAsync(ct).ConfigureAwait(false);

        var workItems = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.IssueReference)
            .Include(w => w.PullRequests)
            .Include(w => w.GitRepository)
            .Where(w => WorkItemStatusExtensions.ActiveOrWaitingStatuses.Contains(w.Status))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var projectedItems = workItems
            .Select(w => ProjectWorkItem(w, nowUtc))
            .ToList();

        var scmEvents = await ProjectScmEventsAsync(db, ct).ConfigureAwait(false);
        var runningCount = await _automationsCount.CountAsync(ct).ConfigureAwait(false);
        var version = await _versionProvider.GetInstalledVersionAsync(ct).ConfigureAwait(false);

        return new StatusSnapshot
        {
            InstanceId = instanceId,
            InstanceName = instanceName,
            CreatedUtc = nowUtc,
            ProtocolVersion = RelayProtocolVersion.Current,
            AppVersion = version?.TagName ?? version?.Version,
            RunningAutomationsCount = runningCount,
            WorkItems = projectedItems,
            ScmEvents = scmEvents,
        };
    }

    private StatusSnapshotWorkItem ProjectWorkItem(WorkItem workItem, DateTimeOffset nowUtc)
    {
        var liveRunStatus = _statusSource.GetLiveRunStatus(workItem.Id) ?? workItem.RunStatus;
        var hasActiveRun = WorkItemRunActivity.IsActive(
            workItem.ActiveRunId, workItem.LastHeartbeatUtc, nowUtc);

        return new StatusSnapshotWorkItem
        {
            Title = workItem.Title,
            Status = workItem.Status.ToString(),
            RunStatus = liveRunStatus?.ToString(),
            Kind = workItem.Kind == WorkItemKind.Unspecified ? null : workItem.Kind.ToString(),
            Purpose = workItem.Purpose,
            PausedUntilUtc = workItem.PausedUntilUtc,
            LastHeartbeatUtc = workItem.LastHeartbeatUtc,
            HasActiveRun = hasActiveRun,
            RepositoryName = workItem.GitRepository?.RepositoryName,
            IssueNumber = workItem.IssueReference?.IssueNumber,
            IssueUrl = workItem.IssueReference?.IssueUrl,
            PullRequests = workItem.PullRequests
                .Select(p => new StatusSnapshotPullRequest(
                    p.PullRequestNumber,
                    p.Title,
                    p.Url,
                    p.Status == PullRequestStatus.Unknown ? null : p.Status.ToString(),
                    string.IsNullOrEmpty(p.RepositoryId) ? null : p.RepositoryId))
                .ToList(),
        };
    }

    private async Task<IReadOnlyList<StatusSnapshotScmEvent>> ProjectScmEventsAsync(
        SoftwareschmiedeDbContext db, CancellationToken ct)
    {
        // Blockierende Ereignisse (Warteschlange) plus die jüngsten Einträge für die Übersicht.
        var recent = await db.ScmIncomingEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ReceivedUtc)
            .Take(MaxScmEvents)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var recentKeys = recent.Select(e => e.EventKey).ToHashSet(StringComparer.Ordinal);
        var blocking = await db.ScmIncomingEvents
            .AsNoTracking()
            .Where(e => !ScmIncomingEventStatus.NonBlocking.Contains(e.Status))
            .OrderByDescending(e => e.ReceivedUtc)
            .Take(MaxScmEvents)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var merged = recent
            .Concat(blocking.Where(e => !recentKeys.Contains(e.EventKey)))
            .OrderByDescending(e => e.ReceivedUtc)
            .Take(MaxScmEvents);

        var projected = new List<StatusSnapshotScmEvent>();
        foreach (var item in merged)
        {
            string? referenceDisplay = null;
            string? url = null;
            try
            {
                // Deserialize kann auch null liefern (JSON-Literal „null“) — dann bleiben
                // Referenzanzeige und URL leer statt die Snapshot-Erzeugung abzureißen.
                var observation = ScmEventStore.Observation(item);
                if (observation is not null)
                {
                    referenceDisplay = $"{observation.ContentKind} #{observation.Reference.Number}";
                    url = observation.Url;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "ObservationJson des SCM-Ereignisses {EventId} nicht lesbar.", item.Id);
            }

            projected.Add(new StatusSnapshotScmEvent
            {
                Status = item.Status,
                Reason = item.Reason,
                ReceivedUtc = DateTimeOffset.FromUnixTimeMilliseconds(item.ReceivedUtc),
                ReferenceDisplay = referenceDisplay,
                Url = url,
            });
        }

        return projected;
    }
}
