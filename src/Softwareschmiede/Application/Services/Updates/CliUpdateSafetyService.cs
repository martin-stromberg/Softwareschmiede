using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Prüft aktive CLI-Aufgaben vor dem Start eines Programmupdates.</summary>
public sealed class CliUpdateSafetyService : ICliUpdateSafetyService
{
    private readonly AufgabeService _aufgabeService;
    private readonly KiPluginLimitService _kiPluginLimitService;
    private readonly ILogger<CliUpdateSafetyService> _logger;

    /// <inheritdoc cref="CliUpdateSafetyService"/>
    public CliUpdateSafetyService(
        AufgabeService aufgabeService,
        KiPluginLimitService kiPluginLimitService,
        ILogger<CliUpdateSafetyService> logger)
    {
        _aufgabeService = aufgabeService;
        _kiPluginLimitService = kiPluginLimitService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CliUpdateSafetyResult> CheckAsync(CancellationToken ct = default)
    {
        var activeTasks = await _aufgabeService.GetAktiveAufgabenAsync(ct);

        var prefixes = activeTasks
            .Select(a => a.KiPluginPrefix)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Persistierte, noch zukünftige Plugin-Session-Limits in einer Abfrage laden.
        var sessionLimits = await _kiPluginLimitService.GetAktiveSessionLimitsAsync(prefixes, ct);

        var now = DateTimeOffset.UtcNow;
        var riskyTasks = activeTasks
            .Where(a =>
            {
                // Ein bekannt limitiertes Plugin kann die Ausführung bis zum Reset ohnehin nicht
                // fortsetzen — die Aufgabe ist kein Update-Risiko, auch bei frischem Heartbeat
                // (Heartbeat-Toleranz entfällt komplett).
                if (!string.IsNullOrWhiteSpace(a.KiPluginPrefix)
                    && sessionLimits.TryGetValue(a.KiPluginPrefix, out _))
                {
                    return false;
                }

                return AufgabeLaufAktivitaet.IstAktiv(a.AktiveRunId, a.LastHeartbeatUtc, now);
            })
            .Select(a => $"{a.Titel} ({a.Id})")
            .ToList();

        if (riskyTasks.Count > 0)
        {
            _logger.LogInformation("Update-Sicherheitsprüfung fand {Count} riskante aktive CLI-Aufgaben.", riskyTasks.Count);
        }

        return new CliUpdateSafetyResult(riskyTasks.Count, riskyTasks);
    }
}
