namespace Softwareschmiede.Domain.Enums;

/// <summary>Status eines Provider-Workflow-Runs.</summary>
public enum WorkflowRunStatus
{
    /// <summary>Status ist unbekannt.</summary>
    Unknown,

    /// <summary>Workflow wurde angefragt oder steht in der Warteschlange.</summary>
    Queued,

    /// <summary>Workflow laeuft.</summary>
    InProgress,

    /// <summary>Workflow wurde abgeschlossen.</summary>
    Completed
}
