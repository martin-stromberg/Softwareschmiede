using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für die manuelle Wiederherstellung festhängender Aufgaben.</summary>
public sealed class AufgabeRecoveryService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly IRunningAutomationStatusSource _runningStatusSource;
    private readonly ILogger<AufgabeRecoveryService> _logger;

    /// <summary>Schwelle in Minuten, ab der ein Heartbeat als abgelaufen gilt (Recovery-Kandidat bzw. KI-Status "nicht mehr aktiv").</summary>
    public const int HeartbeatTimeoutMinutes = 5;

    /// <summary>Erstellt eine neue Instanz des <see cref="AufgabeRecoveryService"/>.</summary>
    public AufgabeRecoveryService(
        SoftwareschmiedeDbContext db,
        IRunningAutomationStatusSource runningStatusSource,
        ILogger<AufgabeRecoveryService> logger)
    {
        _db = db;
        _runningStatusSource = runningStatusSource;
        _logger = logger;
    }

    /// <summary>
    /// Scannt alle Aufgaben nach Recovery-Kandidaten.
    /// Kandidaten: aktiver Aufgabenstatus, persistierter Ausfuehrungsstatus Aktiv,
    /// Heartbeat älter als 5 Minuten, kein laufender CLI-Prozess.
    /// </summary>
    public async Task<IEnumerable<Guid>> ScanForRecoveryCandidatesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMinutes(-HeartbeatTimeoutMinutes);

        // Pausierte Aufgaben (PausiertBisUtc in der Zukunft) sind keine „festhängenden" Kandidaten:
        // sie wurden bewusst bis zu einem bekannten Zeitpunkt angehalten (z. B. Session-Limit des
        // KI-Plugins) und tauchen nach Pausenende bei weiterhin abgelaufenem Heartbeat wieder auf.
        var kandidaten = await _db.WorkItems
            .AsNoTracking()
            .Where(a => WorkItemStatusExtensions.ActiveOrWaitingStatuses.Contains(a.Status)
                && a.ExecutionStatus == WorkItemExecutionStatus.Active
                && a.LastHeartbeatUtc != null
                && a.LastHeartbeatUtc < cutoff
                && (a.PausedUntilUtc == null || a.PausedUntilUtc <= now))
            .Select(a => a.Id)
            .ToListAsync(ct);

        return kandidaten.Where(id => !_runningStatusSource.IsRunning(id));
    }

    /// <summary>
    /// Führt eine manuelle Recovery durch.
    /// Erlaubt nur Recovery aus <see cref="WorkItemStatus.Started"/> oder <see cref="WorkItemStatus.Waiting"/>.
    /// </summary>
    public async Task RecoverManuellAsync(Guid workItemId, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _logger.LogInformation(
            "TaskRecoveryRequested CorrelationId={CorrelationId} TaskId={TaskId} Trigger=Manual",
            correlationId,
            workItemId);

        var workItem = await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == workItemId, ct);

        if (workItem is null)
        {
            LogRejected(correlationId, workItemId, "NotFound");
            throw new InvalidOperationException("Aufgabe wurde nicht gefunden.");
        }

        if (!IstRecoveryStatus(workItem.Status, workItem.ExecutionStatus))
        {
            LogRejected(correlationId, workItemId, "InvalidState");
            throw new InvalidOperationException("Wiederherstellung für aktuellen Status nicht verfügbar.");
        }

        if (workItem.PausedUntilUtc is { } pausiertBis && workItem.PausedUntilUtc.IstAktuellPausiert(DateTimeOffset.UtcNow))
        {
            LogRejected(correlationId, workItemId, "Paused");
            throw new InvalidOperationException(
                $"Wiederherstellung nicht möglich, die Aufgabe ist bis {pausiertBis.ToLocalTime():dd.MM.yyyy HH:mm:ss} pausiert.");
        }

        bool isRunning;
        try
        {
            isRunning = _runningStatusSource.IsRunning(workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TaskRecoveryRejected CorrelationId={CorrelationId} TaskId={TaskId} ReasonCode=RunningStatusUnavailable",
                correlationId,
                workItemId);
            throw new InvalidOperationException("Prüfung der Laufzeit war nicht möglich.");
        }

        _logger.LogInformation(
            "TaskRecoveryEligibilityChecked CorrelationId={CorrelationId} TaskId={TaskId} IsRunning={IsRunning} CurrentStatus={CurrentStatus} CurrentAusfuehrungsStatus={CurrentAusfuehrungsStatus}",
            correlationId,
            workItemId,
            isRunning,
            workItem.Status,
            workItem.ExecutionStatus);

        if (isRunning)
        {
            LogRejected(correlationId, workItemId, "StillRunning");
            throw new InvalidOperationException("Wiederherstellung nicht möglich, Verarbeitung läuft noch.");
        }

        IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            int rowCount;
            if (_db.Database.IsRelational())
            {
                rowCount = await _db.WorkItems
                    .Where(a => a.Id == workItemId
                        && a.Status == workItem.Status
                        && a.ExecutionStatus == WorkItemExecutionStatus.Active
                        && a.RecoveryVersion == workItem.RecoveryVersion)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(a => a.Status, WorkItemStatus.Started)
                        .SetProperty(a => a.RecoveryVersion, a => a.RecoveryVersion + 1), ct);
            }
            else
            {
                var tracked = await _db.WorkItems.FirstOrDefaultAsync(a => a.Id == workItemId, ct);
                if (tracked is null
                    || tracked.Status != workItem.Status
                    || tracked.ExecutionStatus != WorkItemExecutionStatus.Active
                    || tracked.RecoveryVersion != workItem.RecoveryVersion)
                {
                    rowCount = 0;
                }
                else
                {
                    tracked.Status = WorkItemStatus.Started;
                    tracked.RecoveryVersion++;
                    rowCount = 1;
                }
            }

            if (rowCount == 0)
            {
                _logger.LogInformation(
                    "TaskRecoveryConcurrencyConflict CorrelationId={CorrelationId} TaskId={TaskId}",
                    correlationId,
                    workItemId);
                throw new InvalidOperationException("Status wurde bereits geändert. Ansicht wurde aktualisiert.");
            }

            var auditEintrag = new LogEntry
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
                Type = LogEntryType.StatusTransition,
                Content =
                    $"Manuelle Wiederherstellung: {workItem.Status} → {WorkItemStatus.Started}\n" +
                    $"ReasonCode: RecoveryManual\n" +
                    $"CorrelationId: {correlationId}",
                Timestamp = DateTimeOffset.UtcNow
            };
            _db.LogEntries.Add(auditEintrag);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            _logger.LogInformation(
                "TaskRecoverySucceeded CorrelationId={CorrelationId} TaskId={TaskId} FromStatus={FromStatus} ToStatus={ToStatus}",
                correlationId,
                workItemId,
                workItem.Status,
                WorkItemStatus.Started);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private void LogRejected(string correlationId, Guid workItemId, string reasonCode)
        => _logger.LogInformation(
            "TaskRecoveryRejected CorrelationId={CorrelationId} TaskId={TaskId} ReasonCode={ReasonCode}",
            correlationId,
            workItemId,
            reasonCode);

    /// <summary>
    /// Prüft, ob eine Aufgabe für die generische Crash-Recovery in Frage kommt.
    /// </summary>
    /// <param name="status">Der Aufgabenstatus.</param>
    /// <param name="executionStatus">Der persistierte KI-Ausführungsstatus.</param>
    /// <returns><c>true</c>, wenn die Aufgabe ein Recovery-Kandidat ist; andernfalls <c>false</c>.</returns>
    internal static bool IstRecoveryStatus(WorkItemStatus status, WorkItemExecutionStatus executionStatus)
        => status.IsActiveOrWaiting()
            && executionStatus == WorkItemExecutionStatus.Active;
}
