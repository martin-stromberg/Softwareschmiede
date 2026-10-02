using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Zentrale Erzeugung von Terminal-Sessions: löst die Spec auf, führt die Preflight-Diagnose
/// aus, wählt das Backend (PTY oder Pipe) und startet den CLI-Prozess.</summary>
public interface ITerminalSessionFactory
{
    /// <summary>Startet eine Terminal-Session aus der gelieferten Spec.</summary>
    /// <param name="aufgabeId">ID der Aufgabe (für Logging/Protokoll).</param>
    /// <param name="spec">Die vom Plugin gelieferte Startbeschreibung.</param>
    /// <param name="outputSink">Optionale Senke für Terminal-Ausgabe (Protokoll-Pfad).</param>
    /// <param name="healthCheck">Optionale Plugin-Health-Probe (<c>IKiPlugin.CheckHealthAsync</c> als Delegate).</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Das <see cref="TerminalSessionStartResult"/> der gestarteten Session.</returns>
    Task<TerminalSessionStartResult> StartAsync(
        Guid aufgabeId,
        TerminalSessionStartSpec spec,
        ITerminalOutputSink? outputSink,
        Func<CancellationToken, Task<bool>>? healthCheck,
        CancellationToken ct);
}
