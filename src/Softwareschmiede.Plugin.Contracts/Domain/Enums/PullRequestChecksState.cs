namespace Softwareschmiede.Domain.Enums;

/// <summary>Zusammengefasster Zustand der Statuschecks auf dem Head-Commit eines Pull Requests.</summary>
public enum PullRequestChecksState
{
    /// <summary>Check-Zustand ist nicht bekannt oder wird vom Plugin nicht geliefert.</summary>
    Unknown,

    /// <summary>Keine Checks vorhanden.</summary>
    None,

    /// <summary>Mindestens ein Check läuft noch oder sein Ergebnis fehlt.</summary>
    Pending,

    /// <summary>Alle Checks sind erfolgreich.</summary>
    Success,

    /// <summary>Mindestens ein Check ist fehlgeschlagen.</summary>
    Failure
}
