namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Ein aufgezeichneter Roh-Chunk der Terminal-Ausgabe mit Zeitstempel relativ zum
/// Aufzeichnungsbeginn (<see cref="CliOutputAufzeichnung.StartUtc"/>).</summary>
/// <param name="Offset">Zeitlicher Abstand des Chunks zum Aufzeichnungsbeginn.</param>
/// <param name="Data">Die unveränderten Rohbytes des Chunks.</param>
public sealed record CliOutputChunkRecord(TimeSpan Offset, byte[] Data);
