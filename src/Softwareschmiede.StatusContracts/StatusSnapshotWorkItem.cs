namespace Softwareschmiede.StatusContracts;

/// <summary>Projizierte Aufgabe einer App-Instanz für die Status-Seite.</summary>
public sealed record StatusSnapshotWorkItem
{
    /// <summary>Titel der Aufgabe.</summary>
    public required string Title { get; init; }

    /// <summary>Lebenszyklus-Status als Anzeigetext (z. B. „Gestartet", „Wartend").</summary>
    public required string Status { get; init; }

    /// <summary>Laufzeit-Substatus als Enum-Name (<c>Running</c>/<c>WaitingForInput</c>),
    /// bevorzugt aus der Live-Quelle; <see langword="null"/> ohne aktiven Lauf.</summary>
    public string? RunStatus { get; init; }

    /// <summary>Fachlicher Ursprung der Aufgabe als Enum-Name (z. B. <c>Instruction</c>, <c>PullRequest</c>).</summary>
    public string? Kind { get; init; }

    /// <summary>Zweck-Markierung der Aufgabe (z. B. <c>IssueAufbereitung</c>); <see langword="null"/> bei regulären Aufgaben.</summary>
    public string? Purpose { get; init; }

    /// <summary>UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist; <see langword="null"/> oder Vergangenheit = nicht pausiert.</summary>
    public DateTimeOffset? PausedUntilUtc { get; init; }

    /// <summary>Letzter Heartbeat einer laufenden Ausführung (UTC).</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; init; }

    /// <summary><see langword="true"/>, wenn zum Projektionszeitpunkt ein aktiver Lauf besteht
    /// (aktive Lauf-ID und frischer Heartbeat).</summary>
    public bool HasActiveRun { get; init; }

    /// <summary>Name des verknüpften Git-Repositories, sofern vorhanden.</summary>
    public string? RepositoryName { get; init; }

    /// <summary>Nummer des verknüpften Issues beim Provider, sofern vorhanden.</summary>
    public int? IssueNumber { get; init; }

    /// <summary>URL des verknüpften Issues beim Provider, sofern vorhanden.</summary>
    public string? IssueUrl { get; init; }

    /// <summary>Verknüpfte Pull Requests der Aufgabe.</summary>
    public IReadOnlyList<StatusSnapshotPullRequest> PullRequests { get; init; } = [];
}

/// <summary>Verknüpfte Pull-Request-Referenz einer Aufgabe für die Status-Seite.</summary>
/// <param name="Number">Die Pull-Request-Nummer beim Provider.</param>
/// <param name="Title">Der Titel des Pull Requests.</param>
/// <param name="Url">Die URL des Pull Requests beim Provider.</param>
/// <param name="Status">Der PR-Status als Enum-Name (z. B. <c>Open</c>, <c>Merged</c>).</param>
/// <param name="RepositoryId">Provider-Repository-Identifier (z. B. <c>owner/repo</c>).</param>
public sealed record StatusSnapshotPullRequest(
    int Number,
    string Title,
    string Url,
    string? Status,
    string? RepositoryId);
