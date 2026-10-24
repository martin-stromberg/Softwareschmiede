namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistenter SCM-Ereignisdatensatz: ScmIncomingEvent.</summary>
public sealed class ScmIncomingEvent
{
    /// <summary>Sequence des Ereignisprozesses.</summary>
    public long Sequence { get; set; }

    /// <summary>Id des Ereignisprozesses.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>EventKey des Ereignisprozesses.</summary>
    public string EventKey { get; set; } = "";

    /// <summary>AufgabeId des Ereignisprozesses.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>ObservationJson des Ereignisprozesses.</summary>
    public string ObservationJson { get; set; } = "";

    /// <summary>Status des Ereignisprozesses.</summary>
    public string Status { get; set; } = "Aufgenommen";

    /// <summary>Reason des Ereignisprozesses.</summary>
    public string? Reason { get; set; }

    /// <summary>ReceivedUtc des Ereignisprozesses.</summary>
    public long ReceivedUtc { get; set; }

    /// <summary>Attempts des Ereignisprozesses.</summary>
    public int Attempts { get; set; }

    /// <summary>NextAttemptUtc des Ereignisprozesses.</summary>
    public long? NextAttemptUtc { get; set; }

    /// <summary>ErrorCode des Ereignisprozesses.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>LeaseOwner des Ereignisprozesses.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>LeaseUntilUtc des Ereignisprozesses.</summary>
    public long? LeaseUntilUtc { get; set; }

    /// <summary>DeliveryAttemptId des Ereignisprozesses.</summary>
    public Guid? DeliveryAttemptId { get; set; }

    /// <summary>CliRunId des Ereignisprozesses.</summary>
    public string? CliRunId { get; set; }

    /// <summary>ExchangePath des Ereignisprozesses.</summary>
    public string? ExchangePath { get; set; }

    /// <summary>Nachgewiesene beschränkende Prozessidentitäten des Antwortkanals, ohne allgemeine Benutzerfreigaben.</summary>
    public string? ExchangeRestrictedWriteSidsJson { get; set; }

    /// <summary>DeliveredUtc des Ereignisprozesses.</summary>
    public long? DeliveredUtc { get; set; }

    /// <summary>AcceptedUtc des Ereignisprozesses.</summary>
    public long? AcceptedUtc { get; set; }

    /// <summary>ReadyConfirmedRunId des Ereignisprozesses.</summary>
    public string? ReadyConfirmedRunId { get; set; }
}
