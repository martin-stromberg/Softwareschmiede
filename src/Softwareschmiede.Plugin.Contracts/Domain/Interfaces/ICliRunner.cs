using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Führt CLI-Prozesse aus.</summary>
public interface ICliRunner
{
    /// <summary>
    /// Führt einen CLI-Befehl aus und gibt das Ergebnis zurück.
    /// stdout und stderr werden parallel gelesen um Deadlocks zu vermeiden.
    /// </summary>
    /// <param name="command">Ausführbares Programm.</param>
    /// <param name="args">Argumente (werden sicher über ArgumentList übergeben).</param>
    /// <param name="workingDirectory">Arbeitsverzeichnis oder null für aktuelles Verzeichnis.</param>
    /// <param name="environmentVariables">Umgebungsvariablen (z.B. Tokens).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<CliResult> RunAsync(
        string command,
        IEnumerable<string> args,
        string? workingDirectory,
        IDictionary<string, string>? environmentVariables,
        CancellationToken ct = default);

    /// <summary>Führt einen CLI-Befehl aus und streamt stdout zeilenweise.</summary>
    /// <param name="command">Ausführbares Programm.</param>
    /// <param name="args">Argumente (werden sicher über ArgumentList übergeben).</param>
    /// <param name="workingDirectory">Arbeitsverzeichnis oder null für aktuelles Verzeichnis.</param>
    /// <param name="environmentVariables">Umgebungsvariablen (z.B. Tokens).</param>
    /// <param name="ct">Cancellation Token.</param>
    IAsyncEnumerable<string> StreamAsync(
        string command,
        IEnumerable<string> args,
        string? workingDirectory,
        IDictionary<string, string>? environmentVariables,
        CancellationToken ct = default);
}
