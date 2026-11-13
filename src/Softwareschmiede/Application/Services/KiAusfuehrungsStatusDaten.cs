using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>Parameterbündel der <see cref="KiAusfuehrungsStatusKlassifikation"/>.</summary>
/// <param name="Status">Aktueller Aufgabenstatus.</param>
/// <param name="AusfuehrungsStatus">Persistierter Lebenszyklusstatus der KI-Ausführung.</param>
/// <param name="AktiveRunId">Aktive Lauf-ID der KI-Ausführung.</param>
/// <param name="LastHeartbeatUtc">Zeitstempel des letzten Heartbeats einer aktiven Ausführung.</param>
/// <param name="LaufStatus">Persistierter Laufzeit-Substatus der aktiven CLI-Ausführung.</param>
/// <param name="LiveLaufStatus">Live-Substatus aus der Laufzeitquelle (<c>null</c> = kein laufender gehandelter Prozess).</param>
/// <param name="HasScheduledPrompt">Gibt an, ob ein zeitgesteuerter Prompt in der Warteschlange steht.</param>
/// <param name="PausiertBisUtc">UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist (null oder Vergangenheit = nicht pausiert).</param>
public sealed record KiAusfuehrungsStatusDaten(
    AufgabeStatus Status,
    AufgabeAusfuehrungsStatus AusfuehrungsStatus,
    string? AktiveRunId,
    DateTimeOffset? LastHeartbeatUtc,
    AufgabeLaufStatus? LaufStatus,
    AufgabeLaufStatus? LiveLaufStatus,
    bool HasScheduledPrompt,
    DateTimeOffset? PausiertBisUtc)
{
    /// <summary>
    /// Erstellt die Klassifikations-Eingabe aus einer <see cref="Aufgabe"/>-Entität —
    /// gemeinsame Fabrik für alle Aufrufstellen, die die persistierten Aufgabenfelder
    /// auf die positionsabhängige Parameterliste mappen.
    /// </summary>
    /// <param name="aufgabe">Quell-Entität.</param>
    /// <param name="liveLaufStatus">Live-Substatus aus der Laufzeitquelle (<c>null</c> = kein laufender gehandelter Prozess).</param>
    /// <param name="hasScheduledPrompt">Gibt an, ob ein zeitgesteuerter Prompt in der Warteschlange steht.</param>
    public static KiAusfuehrungsStatusDaten VonAufgabe(
        Aufgabe aufgabe,
        AufgabeLaufStatus? liveLaufStatus,
        bool hasScheduledPrompt)
        => new(
            aufgabe.Status,
            aufgabe.AusfuehrungsStatus,
            aufgabe.AktiveRunId,
            aufgabe.LastHeartbeatUtc,
            aufgabe.LaufStatus,
            liveLaufStatus,
            hasScheduledPrompt,
            aufgabe.PausiertBisUtc);
}
