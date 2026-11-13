using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Zähler der fachlich laufenden Automatisierungen: wendet die gemeinsame
/// <see cref="KiAusfuehrungsStatusKlassifikation"/> auf die Kandidatenmenge der Seitenleiste
/// (<see cref="IAktiveAufgabenService.GetAktiveAufgabenAsync"/>) an und zählt ausschließlich
/// den Zustand <see cref="KiAusfuehrungsZustand.Laeuft"/>. Laufender gehandelter Prozess und
/// zeitgesteuerter Prompt werden dabei wie in der UI-Klassifikation berücksichtigt.
/// </summary>
public sealed class LaufendeAutomatisierungenZaehlung : ILaufendeAutomatisierungenZaehlung
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRunningAutomationStatusSource _statusSource;
    private readonly PromptZeitVersandService _promptZeitVersandService;

    /// <inheritdoc cref="LaufendeAutomatisierungenZaehlung"/>
    /// <param name="scopeFactory">Scope-Factory zum Zugriff auf den scoped <see cref="IAktiveAufgabenService"/>.</param>
    /// <param name="statusSource">Laufzeitquelle des Live-Substatus je Aufgabe.</param>
    /// <param name="promptZeitVersandService">Quelle der zeitgesteuerten Prompt-Wartestellung.</param>
    public LaufendeAutomatisierungenZaehlung(
        IServiceScopeFactory scopeFactory,
        IRunningAutomationStatusSource statusSource,
        PromptZeitVersandService promptZeitVersandService)
    {
        _scopeFactory = scopeFactory;
        _statusSource = statusSource;
        _promptZeitVersandService = promptZeitVersandService;
    }

    /// <inheritdoc/>
    public async Task<int> ZaehleAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var aktiveAufgabenService = scope.ServiceProvider.GetRequiredService<IAktiveAufgabenService>();
        var aufgaben = await aktiveAufgabenService.GetAktiveAufgabenAsync(ct);

        var anzahl = 0;
        foreach (var aufgabe in aufgaben)
        {
            var daten = KiAusfuehrungsStatusDaten.VonAufgabe(
                aufgabe,
                _statusSource.GetLiveLaufStatus(aufgabe.Id),
                _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id) is not null);

            if (KiAusfuehrungsStatusKlassifikation.Klassifiziere(daten, out _) == KiAusfuehrungsZustand.Laeuft)
            {
                anzahl++;
            }
        }

        return anzahl;
    }
}
