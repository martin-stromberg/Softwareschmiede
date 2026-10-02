namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>EventArgs für einen fatalen Laufzeitfehler einer <see cref="ITerminalSession"/>.</summary>
public sealed class TerminalSessionFailedEventArgs : EventArgs
{
    /// <summary>Erstellt neue Ereignisargumente.</summary>
    /// <param name="error">Die aufgetretene Exception.</param>
    /// <param name="phase">Die Session-Phase, in der der Fehler auftrat (z. B. <c>ReadLoop</c>, <c>Write</c>).</param>
    public TerminalSessionFailedEventArgs(Exception error, string phase)
    {
        Error = error;
        Phase = phase;
    }

    /// <summary>Die aufgetretene Exception.</summary>
    public Exception Error { get; }

    /// <summary>Die Session-Phase, in der der Fehler auftrat (z. B. <c>ReadLoop</c>, <c>Write</c>).</summary>
    public string Phase { get; }
}
