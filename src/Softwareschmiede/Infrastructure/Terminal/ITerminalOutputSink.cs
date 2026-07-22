namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Optionale Senke fuer rohe Terminal-Ausgabe einer <see cref="PseudoConsoleSession"/>.</summary>
public interface ITerminalOutputSink
{
    /// <summary>Meldet einen gelesenen Ausgabe-Chunk. Implementierungen muessen benoetigte Bytes sofort kopieren.</summary>
    /// <param name="bytes">Der gelesene Ausgabe-Chunk.</param>
    void OnOutputChunk(ReadOnlySpan<byte> bytes);

    /// <summary>Schliesst die Ausgabe idempotent ab und flusht ausstehende Restdaten.</summary>
    void Complete();

    /// <summary>Schliesst die Ausgabe ab und wartet begrenzt auf die Persistenz bereits angenommener Daten.</summary>
    /// <param name="timeout">Maximale Wartezeit auf den Abschluss nach dem Schliessen.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Eine Aufgabe, die endet, sobald der Drain abgeschlossen ist oder der Timeout greift.</returns>
    Task CompleteAsync(TimeSpan timeout, CancellationToken ct = default);
}
