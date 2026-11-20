using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Zähler der fachlich laufenden Automatisierungen: wendet die gemeinsame
/// <see cref="AiExecutionStatusClassification"/> auf die Kandidatenmenge der Seitenleiste
/// (<see cref="IActiveWorkItemsService.GetAktiveAufgabenAsync"/>) an und zählt den Zustand
/// <see cref="AiExecutionState.Running"/>. Laufender gehandelter Prozess und
/// zeitgesteuerter Prompt werden dabei wie in der UI-Klassifikation berücksichtigt.
/// Zusätzlich zählt jedes SCM-Ereignis im Status „Verzögert" als ausstehende
/// Automatisierung, damit der Auto-Shutdown eine geplante Zustellung nicht überholt.
/// </summary>
public sealed class RunningAutomationsCount : IRunningAutomationsCount
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRunningAutomationStatusSource _statusSource;
    private readonly PromptScheduleDispatchService _promptZeitVersandService;

    /// <inheritdoc cref="RunningAutomationsCount"/>
    /// <param name="scopeFactory">Scope-Factory zum Zugriff auf den scoped <see cref="IActiveWorkItemsService"/>.</param>
    /// <param name="statusSource">Laufzeitquelle des Live-Substatus je Aufgabe.</param>
    /// <param name="promptZeitVersandService">Quelle der zeitgesteuerten Prompt-Wartestellung.</param>
    public RunningAutomationsCount(
        IServiceScopeFactory scopeFactory,
        IRunningAutomationStatusSource statusSource,
        PromptScheduleDispatchService promptZeitVersandService)
    {
        _scopeFactory = scopeFactory;
        _statusSource = statusSource;
        _promptZeitVersandService = promptZeitVersandService;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var aktiveAufgabenService = scope.ServiceProvider.GetRequiredService<IActiveWorkItemsService>();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var workItems = await aktiveAufgabenService.GetAktiveAufgabenAsync(ct);

        var count = 0;
        foreach (var workItem in workItems)
        {
            var data = AiExecutionStatusData.VonAufgabe(
                workItem,
                _statusSource.GetLiveRunStatus(workItem.Id),
                _promptZeitVersandService.GetScheduledPromptStatus(workItem.Id) is not null);

            if (AiExecutionStatusClassification.Klassifiziere(data, out _) == AiExecutionState.Running)
            {
                count++;
            }
        }

        count += await db.ScmIncomingEvents.CountAsync(x => x.Status == "Verzögert", ct);

        return count;
    }
}
