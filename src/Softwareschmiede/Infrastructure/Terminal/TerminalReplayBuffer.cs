namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Begrenzter Ringpuffer für rohe Terminal-Ausgabe-Chunks. Dient der Neuanbindung eines
/// Terminal-Controls an eine laufende Session (<see cref="ITerminalSession.RebuildBufferFromReplay"/>)
/// und ist bewusst vom dauerhaften Sitzungsprotokoll (<c>CliOutputProtokollWriter</c>) getrennt.</summary>
public sealed class TerminalReplayBuffer
{
    private readonly object _lock = new();
    private readonly Queue<byte[]> _chunks = new();
    private readonly int _byteBudget;
    private int _bufferedBytes;

    /// <summary>Erstellt einen neuen Replay-Puffer.</summary>
    /// <param name="byteBudget">Maximale Gesamtgröße der gehaltenen Rohbytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="byteBudget"/> ist kleiner oder gleich 0.</exception>
    public TerminalReplayBuffer(int byteBudget)
    {
        if (byteBudget <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteBudget), "Das Byte-Budget des Replay-Puffers muss größer als 0 sein.");

        _byteBudget = byteBudget;
    }

    /// <summary>Hängt einen Roh-Chunk an den Puffer an; bei Budget-Überschreitung werden die ältesten Chunks verworfen.</summary>
    /// <param name="chunk">Der anzuhängende Roh-Chunk (wird kopiert).</param>
    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
            return;

        var copy = chunk.Length > _byteBudget ? chunk[^_byteBudget..].ToArray() : chunk.ToArray();
        lock (_lock)
        {
            _chunks.Enqueue(copy);
            _bufferedBytes += copy.Length;
            while (_bufferedBytes > _byteBudget && _chunks.Count > 0)
                _bufferedBytes -= _chunks.Dequeue().Length;
        }
    }

    /// <summary>Liefert die gehaltenen Chunks in der Reihenfolge ihres Eingangs.</summary>
    /// <returns>Eine Momentaufnahme der gepufferten Chunks.</returns>
    public IReadOnlyList<byte[]> GetChunks()
    {
        lock (_lock)
            return _chunks.ToArray();
    }
}
