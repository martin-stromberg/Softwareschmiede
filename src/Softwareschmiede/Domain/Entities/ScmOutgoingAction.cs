namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistenter SCM-Ereignisdatensatz: ScmOutgoingAction.</summary>
public sealed class ScmOutgoingAction
{
    /// <summary>Id des Ereignisprozesses.</summary>
    public Guid Id { get; set; }

    /// <summary>EventId des Ereignisprozesses.</summary>
    public Guid EventId { get; set; }

    /// <summary>AttemptId des Ereignisprozesses.</summary>
    public Guid? AttemptId { get; set; }

    /// <summary>Purpose des Ereignisprozesses.</summary>
    public string Purpose { get; set; } = "";

    /// <summary>RequestJson des Ereignisprozesses.</summary>
    public string RequestJson { get; set; } = "";

    /// <summary>Status des Ereignisprozesses.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>RemoteId des Ereignisprozesses.</summary>
    public string? RemoteId { get; set; }

    /// <summary>Attempts des Ereignisprozesses.</summary>
    public int Attempts { get; set; }

    /// <summary>NextAttemptUtc des Ereignisprozesses.</summary>
    public long? NextAttemptUtc { get; set; }

    /// <summary>ErrorCode des Ereignisprozesses.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Error des Ereignisprozesses.</summary>
    public string? Error { get; set; }
}
