using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Interne Backend-Naht für den eigentlichen Terminal-Prozessstart in <see cref="TerminalSessionService"/>.</summary>
public interface IPseudoConsoleProcessLauncher
{
    /// <summary>Gibt an, ob dieser Launcher eine echte Pseudo Console (ConPTY) bereitstellt.</summary>
    bool IsPseudoTerminal { get; }

    /// <summary>Startet den in <paramref name="spec"/> beschriebenen CLI-Prozess direkt (die Spec ist bereits
    /// durch <c>TerminalExecutableResolver</c> normalisiert — der Launcher enthält keine eigene Auflösungslogik).</summary>
    /// <param name="aufgabeId">ID der Aufgabe (für Logging).</param>
    /// <param name="spec">Die normalisierte Startbeschreibung der Anbieter-CLI.</param>
    /// <param name="outputSink">Optionale Senke für Terminal-Ausgabe.</param>
    /// <returns>Ein <see cref="TerminalSessionStartResult"/> mit Prozess, Session und
    /// Backend-Kennzeichnung.</returns>
    TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null);
}
