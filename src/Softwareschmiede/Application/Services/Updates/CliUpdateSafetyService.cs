using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Prüft aktive CLI-Aufgaben vor dem Start eines Programmupdates.</summary>
public sealed class CliUpdateSafetyService : ICliUpdateSafetyService
{
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<CliUpdateSafetyService> _logger;

    /// <inheritdoc cref="CliUpdateSafetyService"/>
    public CliUpdateSafetyService(AufgabeService aufgabeService, ILogger<CliUpdateSafetyService> logger)
    {
        _aufgabeService = aufgabeService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CliUpdateSafetyResult> CheckAsync(CancellationToken ct = default)
    {
        var activeTasks = await _aufgabeService.GetAktiveAufgabenAsync(ct);
        var riskyTasks = activeTasks
            .Where(a => a.AktiveRunId is not null && a.LaufStatus == AufgabeLaufStatus.Laeuft)
            .Select(a => $"{a.Titel} ({a.Id})")
            .ToList();

        if (riskyTasks.Count > 0)
        {
            _logger.LogInformation("Update-Sicherheitsprüfung fand {Count} riskante aktive CLI-Aufgaben.", riskyTasks.Count);
        }

        return new CliUpdateSafetyResult(riskyTasks.Count, riskyTasks);
    }
}
