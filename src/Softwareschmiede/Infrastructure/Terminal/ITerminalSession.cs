using System.Diagnostics;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Gemeinsame Abstraktion einer interaktiven Terminal-Session (PTY- oder Pipe-Backend):
/// kapselt Prozess, Ein-/Ausgabe-Streams, den gerenderten <see cref="TerminalBuffer"/> und den
/// Session-Lebenszyklus inklusive Exit-/Fehler-Ereignissen.</summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>Schreibbarer Stream für Tastatureingaben an den Prozess.</summary>
    Stream InputStream { get; }

    /// <summary>Lesbarer Stream für die Prozessausgabe.</summary>
    Stream OutputStream { get; }

    /// <summary>Der verwaltete Prozess der Sitzung.</summary>
    Process Process { get; }

    /// <summary>Der Terminal-Buffer dieser Sitzung (wird von der Leseschleife befüllt).</summary>
    TerminalBuffer Buffer { get; }

    /// <summary>Laufzeitstatus der aktiven CLI innerhalb der Sitzung.</summary>
    CliRuntimeStatus RuntimeStatus { get; }

    /// <summary>Gibt an, ob die Session über ein echtes Pseudo-Terminal (ConPTY) läuft.</summary>
    bool IsPseudoTerminal { get; }

    /// <summary>Der Exit-Code des Prozesses nach dessen Beendigung, oder <c>null</c>.</summary>
    int? ExitCode { get; }

    /// <summary>Der erste fatale Laufzeitfehler der Session (z. B. Leseschleifen-/Schreibfehler), oder
    /// <c>null</c>, solange kein Fehler aufgetreten ist. Wird gesetzt, <em>bevor</em> <see cref="Failed"/>
    /// ausgelöst wird, damit ein Fehler, der vor der Registrierung eines <see cref="Failed"/>-Handlers
    /// auftrat, nachträglich erkannt werden kann.</summary>
    TerminalSessionFailedEventArgs? Failure { get; }

    /// <summary>Schreibt bereits kodierte Eingabebytes serialisiert in den Input-Stream der Sitzung.</summary>
    Task WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default);

    /// <summary>Schreibt einen Prompt inkl. abschließendem Absenden (CR) in den Input-Stream.</summary>
    Task WritePromptAsync(string prompt, CancellationToken ct);

    /// <summary>Ändert die Größe des Terminals/der Pseudo Console. Identische Aufrufe werden dedupliziert.</summary>
    bool Resize(int cols, int rows);

    /// <summary>Meldet eine Benutzereingabe an die Status-Erkennung.</summary>
    void MarkInputActivity();

    /// <summary>Meldet gelesene Ausgabe der CLI an die Status-Erkennung.</summary>
    void MarkOutputActivity();

    /// <summary>Wartet begrenzt darauf, dass die Leseschleife den Output-Stream bis zum Ende verarbeitet hat.</summary>
    Task<bool> DrainOutputAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>Baut den <see cref="Buffer"/> synchron aus den gespeicherten Replay-Chunks neu auf
    /// (für die Neuanbindung eines Terminal-Controls an eine laufende Session).</summary>
    void RebuildBufferFromReplay();

    /// <summary>Wird pro gelesenem Roh-Chunk ausgelöst (vor dem Parsen).</summary>
    event EventHandler<TerminalOutputChunkEventArgs>? OutputChunk;

    /// <summary>Wird bei Beendigung des Prozesses ausgelöst; trägt den Exit-Code, soweit ermittelbar.</summary>
    event EventHandler<TerminalSessionExitedEventArgs>? Exited;

    /// <summary>Wird bei einem fatalen Laufzeitfehler der Session ausgelöst (z. B. Leseschleifen-/Schreibfehler).</summary>
    event EventHandler<TerminalSessionFailedEventArgs>? Failed;

    /// <summary>Wird nach jeder erfolgreichen Verarbeitung eines Ausgabe-Chunks durch die Leseschleife ausgelöst.</summary>
    event EventHandler? BufferChanged;

    /// <summary>Wird ausgelöst, wenn sich der Laufzeitstatus der CLI ändert.</summary>
    event EventHandler<CliRuntimeStatusChangedEventArgs>? RuntimeStatusChanged;
}
