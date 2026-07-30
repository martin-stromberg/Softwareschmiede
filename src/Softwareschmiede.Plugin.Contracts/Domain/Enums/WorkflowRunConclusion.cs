namespace Softwareschmiede.Domain.Enums;

/// <summary>Abschlussbewertung eines Provider-Workflow-Runs.</summary>
public enum WorkflowRunConclusion
{
    /// <summary>Bewertung ist nicht bekannt oder noch nicht verfuegbar.</summary>
    Unknown,

    /// <summary>Workflow war erfolgreich.</summary>
    Success,

    /// <summary>Workflow ist fehlgeschlagen.</summary>
    Failure,

    /// <summary>Workflow wurde abgebrochen.</summary>
    Cancelled,

    /// <summary>Workflow wurde uebersprungen.</summary>
    Skipped,

    /// <summary>Workflow wurde wegen Zeitueberschreitung beendet.</summary>
    TimedOut,

    /// <summary>Workflow benoetigt eine manuelle Aktion.</summary>
    ActionRequired
}
