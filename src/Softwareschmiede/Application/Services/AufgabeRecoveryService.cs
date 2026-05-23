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
    private readonly SoftwareschmiededDbContext _db;
    private readonly IRunningAutomationStatusSource _runningStatusSource;
    private readonly ILogger<AufgabeRecoveryService> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="AufgabeRecoveryService"/>.</summary>
    public AufgabeRecoveryService(
        SoftwareschmiededDbContext db,
        IRunningAutomationStatusSource runningStatusSource,
        ILogger<AufgabeRecoveryService> logger)
    {
        _db = db;
        _runningStatusSource = runningStatusSource;
        _logger = logger;
    }

    /// <summary>
    /// Führt eine manuelle Recovery auf <see cref="AufgabeStatus.InBearbeitung"/> aus.
    /// Erlaubt nur Recovery aus <see cref="AufgabeStatus.KiAktiv"/> oder <see cref="AufgabeStatus.TestsLaufen"/>.
    /// </summary>
    public async Task RecoverManuellAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _logger.LogInformation(
            "TaskRecoveryRequested CorrelationId={CorrelationId} TaskId={TaskId} Trigger=Manual",
            correlationId,
            aufgabeId);

        var aufgabe = await _db.Aufgaben
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == aufgabeId, ct);

        if (aufgabe is null)
        {
            LogRejected(correlationId, aufgabeId, "NotFound");
            throw new InvalidOperationException("Aufgabe wurde nicht gefunden.");
        }

        if (!IstRecoveryStatus(aufgabe.Status))
        {
            LogRejected(correlationId, aufgabeId, "InvalidState");
            throw new InvalidOperationException("Wiederherstellung für aktuellen Status nicht verfügbar.");
        }

        bool isRunning;
        try
        {
            isRunning = _runningStatusSource.IsRunning(aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "TaskRecoveryRejected CorrelationId={CorrelationId} TaskId={TaskId} ReasonCode=RunningStatusUnavailable",
                correlationId,
                aufgabeId);
            throw new InvalidOperationException("Prüfung der Laufzeit war nicht möglich.");
        }

        _logger.LogInformation(
            "TaskRecoveryEligibilityChecked CorrelationId={CorrelationId} TaskId={TaskId} IsRunning={IsRunning} CurrentStatus={CurrentStatus}",
            correlationId,
            aufgabeId,
            isRunning,
            aufgabe.Status);

        if (isRunning)
        {
            LogRejected(correlationId, aufgabeId, "StillRunning");
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
                rowCount = await _db.Aufgaben
                    .Where(a => a.Id == aufgabeId
                        && a.Status == aufgabe.Status
                        && a.RecoveryVersion == aufgabe.RecoveryVersion)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(a => a.Status, AufgabeStatus.InBearbeitung)
                        .SetProperty(a => a.RecoveryVersion, a => a.RecoveryVersion + 1), ct);
            }
            else
            {
                var tracked = await _db.Aufgaben.FirstOrDefaultAsync(a => a.Id == aufgabeId, ct);
                if (tracked is null
                    || tracked.Status != aufgabe.Status
                    || tracked.RecoveryVersion != aufgabe.RecoveryVersion)
                {
                    rowCount = 0;
                }
                else
                {
                    tracked.Status = AufgabeStatus.InBearbeitung;
                    tracked.RecoveryVersion++;
                    rowCount = 1;
                }
            }

            if (rowCount == 0)
            {
                _logger.LogInformation(
                    "TaskRecoveryConcurrencyConflict CorrelationId={CorrelationId} TaskId={TaskId}",
                    correlationId,
                    aufgabeId);
                throw new InvalidOperationException("Status wurde bereits geändert. Ansicht wurde aktualisiert.");
            }

            var auditEintrag = new Protokolleintrag
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                Typ = ProtokollTyp.StatusUebergang,
                Inhalt =
                    $"Manuelle Wiederherstellung: {aufgabe.Status} → {AufgabeStatus.InBearbeitung}\n" +
                    $"ReasonCode: RecoveryManual\n" +
                    $"CorrelationId: {correlationId}",
                Zeitstempel = DateTimeOffset.UtcNow
            };
            _db.Protokolleintraege.Add(auditEintrag);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            _logger.LogInformation(
                "TaskRecoverySucceeded CorrelationId={CorrelationId} TaskId={TaskId} FromStatus={FromStatus} ToStatus={ToStatus} AuditEntryId={AuditEntryId}",
                correlationId,
                aufgabeId,
                aufgabe.Status,
                AufgabeStatus.InBearbeitung,
                auditEintrag.Id);
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

    private void LogRejected(string correlationId, Guid aufgabeId, string reasonCode)
        => _logger.LogInformation(
            "TaskRecoveryRejected CorrelationId={CorrelationId} TaskId={TaskId} ReasonCode={ReasonCode}",
            correlationId,
            aufgabeId,
            reasonCode);

    internal static bool IstRecoveryStatus(AufgabeStatus status)
        => status is AufgabeStatus.KiAktiv or AufgabeStatus.TestsLaufen;
}
