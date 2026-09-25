using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Gebündelte, überwiegend optionale Parameter einer <see cref="PseudoConsoleSession"/> —
/// hält die Konstruktorliste schmal (Diagnose, Test-Hooks und Laufzeitkonfiguration).</summary>
internal sealed record PseudoConsoleSessionContext
{
    /// <summary>Logger für Fehler- und Diagnosemeldungen der Leseschleife (optional).</summary>
    public ILogger? Logger { get; init; }

    /// <summary>Optionale Senke für gelesene Terminal-Ausgabe.</summary>
    public ITerminalOutputSink? OutputSink { get; init; }

    /// <summary>Natives Win32-Prozess-Handle für die PID-Wiederverwendungs-sichere Exit-Code-Ermittlung
    /// (ConPTY-Pfad); <see cref="IntPtr.Zero"/> beim Pipe-Backend.</summary>
    public IntPtr NativeProcessHandle { get; init; }

    /// <summary>Terminal-Laufzeitparameter (Replay-Budget, initiale Buffer-Größe); <c>null</c> verwendet die Defaults.</summary>
    public TerminalSessionOptions? Options { get; init; }

    /// <summary>Gibt an, ob die Session über ein echtes Pseudo-Terminal läuft.</summary>
    public bool IsPseudoTerminal { get; init; }

    /// <summary>Zeitquelle für die Runtime-Status-Erkennung (Test-Hook).</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Dauer ohne I/O-Aktivität, ab der der Runtime-Status <see cref="CliRuntimeStatus.WartetAufEingabe"/> angenommen wird.</summary>
    public TimeSpan WaitingThreshold { get; init; } = TimeSpan.FromSeconds(4);
}
