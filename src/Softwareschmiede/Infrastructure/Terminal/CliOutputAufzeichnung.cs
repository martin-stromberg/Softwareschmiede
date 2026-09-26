namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Vollständige Rohbyte-Aufzeichnung einer Terminal-Session: Header-Metadaten plus die
/// aufgezeichneten <see cref="CliOutputChunkRecord"/>-Chunks in Eingangsreihenfolge.</summary>
public sealed class CliOutputAufzeichnung
{
    /// <summary>ID der Aufgabe, zu der die Session gehörte.</summary>
    public required Guid AufgabeId { get; init; }

    /// <summary>Anzeigename des KI-Plugins, dessen CLI aufgezeichnet wurde.</summary>
    public required string PluginName { get; init; }

    /// <summary>Absoluter Aufzeichnungsbeginn (UTC); Anker für die relativen Chunk-Offsets.</summary>
    public required DateTimeOffset StartUtc { get; init; }

    /// <summary>Initiale Spaltenanzahl der aufgezeichneten Session.</summary>
    public required int Cols { get; init; }

    /// <summary>Initiale Zeilenanzahl der aufgezeichneten Session.</summary>
    public required int Rows { get; init; }

    /// <summary><c>true</c>, wenn die Aufzeichnung bis zum Session-Ende lückenlos lief;
    /// <c>false</c>, wenn das Byte-Budget überschritten wurde (nur das Präfix ist enthalten).</summary>
    public required bool IstVollstaendig { get; init; }

    /// <summary>Zeitpunkt des Aufzeichnungsendes (UTC), oder <c>null</c> solange die Aufzeichnung läuft.</summary>
    public DateTimeOffset? EndeUtc { get; init; }

    /// <summary>Die aufgezeichneten Chunks in Eingangsreihenfolge.</summary>
    public required IReadOnlyList<CliOutputChunkRecord> Chunks { get; init; }
}
