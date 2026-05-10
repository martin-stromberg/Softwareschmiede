namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis eines CLI-Prozessaufrufs.</summary>
/// <param name="ExitCode">Exit-Code des Prozesses.</param>
/// <param name="StdOut">Standardausgabe des Prozesses.</param>
/// <param name="StdErr">Fehlerausgabe des Prozesses.</param>
public sealed record CliResult(
    int ExitCode,
    string StdOut,
    string StdErr
)
{
    /// <summary>Gibt an, ob der Prozess erfolgreich war (ExitCode == 0).</summary>
    public bool IsSuccess => ExitCode == 0;
}
