namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>EventArgs für einen gelesenen Roh-Output-Chunk einer <see cref="ITerminalSession"/>.</summary>
public sealed class TerminalOutputChunkEventArgs : EventArgs
{
    /// <summary>Erstellt neue Ereignisargumente.</summary>
    /// <param name="data">Der unveränderte Roh-Chunk der Terminal-Ausgabe.</param>
    public TerminalOutputChunkEventArgs(ReadOnlyMemory<byte> data)
    {
        Data = data;
    }

    /// <summary>Die rohen Ausgabebytes (unverändert, vor der Parser-Verarbeitung).</summary>
    public ReadOnlyMemory<byte> Data { get; }
}
