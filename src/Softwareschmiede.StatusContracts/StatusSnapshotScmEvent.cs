namespace Softwareschmiede.StatusContracts;

/// <summary>Projiziertes SCM-Ereignis der App-Warteschlange für die Status-Seite.</summary>
public sealed record StatusSnapshotScmEvent
{
    /// <summary>Verarbeitungsstatus (<c>ScmIncomingEventStatus</c>-Wert, z. B. <c>Wartend</c>, <c>Verarbeitet</c>).</summary>
    public required string Status { get; init; }

    /// <summary>Warte-/Diagnosegrund, sofern vorhanden.</summary>
    public string? Reason { get; init; }

    /// <summary>UTC-Empfangszeitpunkt des Ereignisses.</summary>
    public DateTimeOffset ReceivedUtc { get; init; }

    /// <summary>Anzeigetext der Referenz (z. B. <c>IssueComment #478</c>).</summary>
    public string? ReferenceDisplay { get; init; }

    /// <summary>URL des beobachteten SCM-Objekts beim Provider (Linkziel auf der Seite), sofern bekannt.</summary>
    public string? Url { get; init; }
}
