using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verarbeitet lokale Annahmen, KI-Aktionen und Abschlüsse unabhängig vom Remote-Pollintervall.</summary>
public sealed class ScmEventExchangeService(IServiceScopeFactory scopes, TimeProvider time, ScmEventStartupGate startup,
    ILogger<ScmEventExchangeService> logger)
    : PeriodicBackgroundService(TimeSpan.FromSeconds(2), time, logger, "SCM-Antwortkanal konnte nicht geprüft werden.")
{
    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        // Derselbe Laufzeit-Schalter wie für die Überwachung: Bei deaktivierter
        // SCM-Ereignisüberwachung wird auch nicht automatisch zugestellt oder abgeglichen.
        // Explizite Benutzeraktionen in der Ereignisübersicht bleiben davon unberührt.
        if (!await scope.ServiceProvider.GetRequiredService<AppEinstellungService>().GetScmMonitoringEnabledAsync(ct))
        {
            logger.LogDebug("SCM-Ereignisüberwachung ist deaktiviert; Antwortkanal-Prüfung wird übersprungen.");
            return;
        }
        var ids = await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmIncomingEvents.AsNoTracking()
            .Where(x => x.DeliveryAttemptId != null && x.Status != "Verarbeitet" && x.Status != "Abgebrochen").Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            using var itemScope = scopes.CreateScope();
            await itemScope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(id, ct: ct);
        }
    }
}
