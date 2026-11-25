namespace Softwareschmiede.StatusContracts;

/// <summary>
/// Zeitpunktbezogener, read-only projizierter Zustand einer App-Instanz.
/// Wird periodisch über <c>PushSnapshot</c> an den Status-Server geliefert;
/// enthält ausschließlich Anzeigefelder — keine Geheimnisse oder Tokens.
/// </summary>
public sealed record StatusSnapshot
{
    /// <summary>Die persistente Instanz-ID der sendenden Installation.</summary>
    public required string InstanceId { get; init; }

    /// <summary>Optionaler Anzeigename der Instanz.</summary>
    public string? InstanceName { get; init; }

    /// <summary>UTC-Erzeugungszeitpunkt des Snapshots in der App.</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Die vom Client verwendete Protokollversion (<see cref="RelayProtocolVersion.Current"/>).</summary>
    public int ProtocolVersion { get; init; }

    /// <summary>Installierte App-Version (Anzeigewert, z. B. Tag-Name oder Version).</summary>
    public string? AppVersion { get; init; }

    /// <summary>Anzahl der fachlich laufenden Automatisierungen (inkl. verzögerter SCM-Ereignisse).</summary>
    public int RunningAutomationsCount { get; init; }

    /// <summary>Aktive bzw. wartende Aufgaben der Instanz.</summary>
    public IReadOnlyList<StatusSnapshotWorkItem> WorkItems { get; init; } = [];

    /// <summary>Jüngste SCM-Ereignisse der Warteschlange (Top-N).</summary>
    public IReadOnlyList<StatusSnapshotScmEvent> ScmEvents { get; init; } = [];
}
