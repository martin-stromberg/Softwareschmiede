namespace Softwareschmiede.Domain.Enums;

/// <summary>Persistierter Lebenszyklusstatus der KI-Ausfuehrung einer Aufgabe.</summary>
public enum WorkItemExecutionStatus
{
    /// <summary>Die KI-Ausfuehrung wurde noch nicht gestartet.</summary>
    NotStarted,

    /// <summary>Die KI-Ausfuehrung ist aktiv oder soll nach einem App-Neustart wiederhergestellt werden.</summary>
    Active,

    /// <summary>Die KI-Ausfuehrung wurde beendet; ein erneuter Start muss explizit ausgeloest werden.</summary>
    Completed
}
