namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistenter SCM-Ereignisdatensatz: ScmObservationCheckpoint.</summary>
public sealed class ScmObservationCheckpoint
{
    /// <summary>SourceKey des Ereignisprozesses.</summary>
    public string SourceKey { get; set; } = "";

    /// <summary>WatermarkUtc des Ereignisprozesses.</summary>
    public long? WatermarkUtc { get; set; }

    /// <summary>Failures des Ereignisprozesses.</summary>
    public int Failures { get; set; }

    /// <summary>NextAttemptUtc des Ereignisprozesses.</summary>
    public long? NextAttemptUtc { get; set; }

    /// <summary>ErrorCode des Ereignisprozesses.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Diagnostic des Ereignisprozesses.</summary>
    public string? Diagnostic { get; set; }
}
