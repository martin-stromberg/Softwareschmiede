using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Deterministische Priorisierung der Dispatch-Reihenfolge eingehender SCM-Ereignisse bei
/// begrenzten Ausführungsplätzen (und allgemein je Polling-Batch). Die Klassenordnung ist:
/// (1) Fortsetzungsereignisse für aktive Aufgaben, deren letzter Lauf pausiert oder beendet ist;
/// (2) Verzögerungs-Ereignisse (<c>#verzögert</c>/<see cref="ScmDelayDirective"/>), aufsteigend
/// nach Zielzeitpunkt <see cref="ScmIncomingEvent.NextAttemptUtc"/>;
/// (3) alle übrigen Ereignisse in Eingangsreihenfolge (<see cref="ScmIncomingEvent.Sequence"/>).
/// Die eigentliche Platzvergabe bleibt der Startfreigabe des Orchestrierers vorbehalten.
/// </summary>
public static class ScmDispatchPrioritaet
{
    /// <summary>Leichtgewichtige Ereignis-Auswahlzeile für die Priorisierung.</summary>
    /// <param name="Id">Ereignis-ID.</param>
    /// <param name="WorkItemId">Zugehörige Aufgabe.</param>
    /// <param name="Sequence">Eingangsreihenfolge.</param>
    /// <param name="Status">Persistierter Ereignisstatus (<c>„Verzögert"</c> markiert Klasse 2).</param>
    /// <param name="NextAttemptUtc">Zielzeitpunkt für die Klasse-2-Sortierung (Unix-ms).</param>
    public sealed record EreignisAuswahl(Guid Id, Guid WorkItemId, long Sequence, string? Status, long? NextAttemptUtc);

    /// <summary>Aufgabenzustand, der für die Priorisierung benötigt wird.</summary>
    /// <param name="Status">Fachlicher Lebenszyklus der Aufgabe.</param>
    /// <param name="ExecutionStatus">Lauf-Lebenszyklus (letzter Lauf).</param>
    /// <param name="PausedUntilUtc">Pausenziel; gesetzt (auch abgelaufen) markiert eine Fortsetzung.</param>
    public sealed record AufgabenZustand(WorkItemStatus Status, WorkItemExecutionStatus ExecutionStatus,
        DateTimeOffset? PausedUntilUtc);

    /// <summary>Prioritätsklasse eines Ereignisses: 1 = Fortsetzung, 2 = Verzögerung, 3 = übrige.</summary>
    /// <param name="item">Ereignis-Auswahlzeile.</param>
    /// <param name="task">Zustand der zugehörigen Aufgabe oder <c>null</c>, wenn unbekannt.</param>
    /// <returns>Prioritätsklasse (kleiner = früher).</returns>
    public static int PrioritaetsKlasse(EreignisAuswahl item, AufgabenZustand? task)
    {
        if (task is not null && task.Status is not (WorkItemStatus.Completed or WorkItemStatus.Archived)
            && (task.PausedUntilUtc is not null
                || (task.ExecutionStatus == WorkItemExecutionStatus.Completed
                    && task.Status is WorkItemStatus.New or WorkItemStatus.Started or WorkItemStatus.Waiting)))
            return 1;
        if (item.Status == "Verzögert")
            return 2;
        return 3;
    }

    /// <summary>Ordnet einen Batch von Ereignissen in die Dispatch-Reihenfolge.</summary>
    /// <param name="items">Kandidaten-Ereignisse des Batches.</param>
    /// <param name="aufgabenZustaende">Zustand je Aufgaben-ID (fehlende Einträge werden wie unbekannt behandelt).</param>
    /// <returns>Priorisiert geordnete Liste (stabil nach <see cref="EreignisAuswahl.Sequence"/>).</returns>
    public static IReadOnlyList<EreignisAuswahl> OrderForDispatch(IReadOnlyList<EreignisAuswahl> items,
        IReadOnlyDictionary<Guid, AufgabenZustand> aufgabenZustaende)
        => items.Select(x => (Item: x, Klasse: PrioritaetsKlasse(x,
                    aufgabenZustaende.TryGetValue(x.WorkItemId, out var z) ? z : null)))
            .OrderBy(t => t.Klasse)
            .ThenBy(t => t.Klasse == 2 ? t.Item.NextAttemptUtc ?? long.MaxValue : 0)
            .ThenBy(t => t.Item.Sequence)
            .Select(t => t.Item)
            .ToList();
}
