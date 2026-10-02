using System.Diagnostics;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Ergebnis eines Terminal-Session-Starts über <see cref="IPseudoConsoleProcessLauncher"/>.
/// Ein etwaiges natives Win32-Prozess-Handle liegt ausschließlich in der Ownership der
/// <see cref="ITerminalSession"/> (für die PID-Wiederverwendungs-sichere Exit-Code-Ermittlung) und
/// wird dort in <c>Dispose</c> geschlossen — es wird nicht zusätzlich über das Ergebnis exponiert.</summary>
/// <param name="Process">Der gestartete CLI-Prozess.</param>
/// <param name="Session">Die zugehörige Terminal-Session.</param>
/// <param name="IsPseudoTerminal">Gibt an, ob die Session über ein echtes Pseudo-Terminal läuft.</param>
public sealed record TerminalSessionStartResult(
    Process Process,
    ITerminalSession Session,
    bool IsPseudoTerminal);
