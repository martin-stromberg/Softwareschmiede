using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Persistiert auditierbare Benachrichtigungsentscheidungen.</summary>
public sealed class BenachrichtigungsAuditService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<BenachrichtigungsAuditService> _logger;

    /// <inheritdoc cref="BenachrichtigungsAuditService"/>
    public BenachrichtigungsAuditService(
        SoftwareschmiedeDbContext db,
        ILogger<BenachrichtigungsAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Persistiert eine Benachrichtigungsentscheidung als Audit-Eintrag.</summary>
    public async Task LogAsync(
        Guid ereignisId,
        Guid workItemId,
        string benutzerId,
        NotificationChannel kanal,
        NotificationMode modus,
        NotificationDecision entscheidung,
        string grund,
        CancellationToken ct = default)
    {
        var eintrag = new NotificationDispatchLog
        {
            Id = Guid.NewGuid(),
            EventId = ereignisId,
            WorkItemId = workItemId,
            UserId = benutzerId,
            Channel = kanal,
            Mode = modus,
            Decision = entscheidung,
            Reason = grund,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.NotificationDispatchLogs.Add(eintrag);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex, "Dispatch-Audit bereits vorhanden für Ereignis {EreignisId}, Kanal {Kanal}.", ereignisId, kanal);
        }
    }
}
