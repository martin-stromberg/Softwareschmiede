using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Persistiert auditierbare Benachrichtigungsentscheidungen.</summary>
public sealed class BenachrichtigungsAuditService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<BenachrichtigungsAuditService> _logger;

    /// <inheritdoc cref="BenachrichtigungsAuditService"/>
    public BenachrichtigungsAuditService(
        SoftwareschmiededDbContext db,
        ILogger<BenachrichtigungsAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid ereignisId,
        Guid aufgabeId,
        string benutzerId,
        BenachrichtigungsKanal kanal,
        BenachrichtigungsModus modus,
        BenachrichtigungsEntscheidung entscheidung,
        string grund,
        CancellationToken ct = default)
    {
        var eintrag = new BenachrichtigungsDispatchLog
        {
            Id = Guid.NewGuid(),
            EreignisId = ereignisId,
            AufgabeId = aufgabeId,
            BenutzerId = benutzerId,
            Kanal = kanal,
            Modus = modus,
            Entscheidung = entscheidung,
            Grund = grund,
            ErstelltAm = DateTimeOffset.UtcNow
        };

        _db.BenachrichtigungsDispatchLogs.Add(eintrag);
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
