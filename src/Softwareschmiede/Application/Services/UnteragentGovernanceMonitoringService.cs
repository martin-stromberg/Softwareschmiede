using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Ueberwacht periodisch alle aktiven Unteragenten Autonomer Aufgaben auf Governance-Abbruchbedingungen (Token-/Laufzeitlimit).</summary>
public sealed class UnteragentGovernanceMonitoringService : PeriodicBackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UnteragentGovernanceMonitoringService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt eine neue Instanz des <see cref="UnteragentGovernanceMonitoringService"/>.</summary>
    /// <param name="scopeFactory">Erzeugt DI-Scopes für den scoped Zugriff auf DbContext und Governance-Service je Durchlauf.</param>
    /// <param name="timeProvider">Zeitquelle für Polling-Delay und Abschlusszeitstempel (testbar via <see cref="TimeProvider"/>).</param>
    /// <param name="logger">Logger für Monitoring- und Abbruchmeldungen.</param>
    public UnteragentGovernanceMonitoringService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<UnteragentGovernanceMonitoringService> logger)
        : base(PollingInterval, timeProvider, logger, "Unteragent-Governance-Monitoring ist fehlgeschlagen.")
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Fuehrt einen einzelnen Monitoring-Durchlauf aus: prueft alle aktuell aktiven Unteragenten auf Governance-Abbruchbedingungen.</summary>
    /// <param name="ct">Abbruchtoken für den Durchlauf.</param>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var governance = scope.ServiceProvider.GetRequiredService<UnteragentGovernanceService>();

        var aktiveUnteragenten = await db.UnteragentSpezifikationen
            .Include(u => u.AutonomAufgabe)
            .ThenInclude(k => k.Aufgabe)
            .Where(u =>
                (u.Status == UnteragentStatus.Erzeugt || u.Status == UnteragentStatus.Ausgefuehrt)
                && u.AutonomAufgabe.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.AutonomAufgabe
                && u.AutonomAufgabe.SessionPauseUtc == null)
            .ToListAsync(ct);

        foreach (var unteragent in aktiveUnteragenten)
        {
            await PruefeUnteragentAsync(db, governance, unteragent, ct);
        }
    }

    private async Task PruefeUnteragentAsync(
        SoftwareschmiededDbContext db,
        UnteragentGovernanceService governance,
        UnteragentSpezifikation unteragent,
        CancellationToken ct)
    {
        try
        {
            try
            {
                await governance.ValidiereFehlerBedingungAsync(unteragent, ct);
            }
            catch (UnteragentAbbruchException ex)
            {
                _logger.LogWarning(
                    "Unteragent {AgentId} wird durch Governance abgebrochen: {Grund}",
                    unteragent.ExterneAgentId,
                    ex.Grund);

                unteragent.Status = UnteragentStatus.Fehler;
                unteragent.AbschlussDatum = _timeProvider.GetUtcNow();
                await db.SaveChangesAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Governance-Pruefung für Unteragent {AgentId} ist fehlgeschlagen; wird im naechsten Durchlauf erneut geprueft.",
                unteragent.ExterneAgentId);
        }
    }
}
