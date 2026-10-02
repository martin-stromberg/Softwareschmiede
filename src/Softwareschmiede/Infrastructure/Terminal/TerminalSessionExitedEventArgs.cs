namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>EventArgs für die Beendigung des Prozesses einer <see cref="ITerminalSession"/>.</summary>
public sealed class TerminalSessionExitedEventArgs : EventArgs
{
    /// <summary>Erstellt neue Ereignisargumente.</summary>
    /// <param name="exitCode">Der ermittelte Exit-Code des Prozesses, oder <c>null</c>.</param>
    public TerminalSessionExitedEventArgs(int? exitCode)
    {
        ExitCode = exitCode;
    }

    /// <summary>Der Exit-Code des Prozesses, oder <c>null</c> wenn nicht ermittelbar.</summary>
    public int? ExitCode { get; }
}
