using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Persistiert auditierbare Benachrichtigungsentscheidungen.</summary>
public sealed class NotificationAuditService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<NotificationAuditService> _logger;

    /// <inheritdoc cref="NotificationAuditService"/>
    public NotificationAuditService(
        SoftwareschmiedeDbContext db,
        ILogger<NotificationAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Persistiert eine Benachrichtigungsentscheidung als Audit-Eintrag.</summary>
    public async Task LogAsync(
        Guid eventId,
        Guid workItemId,
        string benutzerId,
        NotificationChannel kanal,
        NotificationMode mode,
        NotificationDecision entscheidung,
        string grund,
        CancellationToken ct = default)
    {
        var entry = new NotificationDispatchLog
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            WorkItemId = workItemId,
            UserId = benutzerId,
            Channel = kanal,
            Mode = mode,
            Decision = entscheidung,
            Reason = grund,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.NotificationDispatchLogs.Add(entry);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogDebug(ex, "Dispatch-Audit bereits vorhanden für Ereignis {EreignisId}, Kanal {Kanal}.", eventId, kanal);
        }
    }
}
