using System.Diagnostics;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Koordiniert eine laufende Pseudo-Console-Sitzung bestehend aus <see cref="PseudoConsole"/>,
/// <see cref="Process"/>, Eingabe- und Ausgabe-Stream.</summary>
public sealed class PseudoConsoleSession : IDisposable
{
    private readonly PseudoConsole _pseudoConsole;
    private readonly Process _process;
    private bool _disposed;

    /// <summary>Schreibbarer Stream für Tastatureingaben an den Prozess.</summary>
    public Stream InputStream { get; }

    /// <summary>Lesbarer Stream für die Prozessausgabe.</summary>
    public Stream OutputStream { get; }

    /// <summary>Der verwaltete Prozess der Sitzung.</summary>
    public Process Process => _process;

    /// <summary>Der Terminal-Buffer dieser Sitzung. Wird von <c>TerminalControl</c> gesetzt und
    /// bei erneuter Anzeige wiederverwendet, damit der Bildschirminhalt erhalten bleibt.</summary>
    public TerminalBuffer? Buffer { get; set; }

    /// <summary>Erstellt eine neue <see cref="PseudoConsoleSession"/>.</summary>
    /// <param name="pseudoConsole">Die zugehörige Pseudo Console.</param>
    /// <param name="process">Der gestartete Prozess.</param>
    /// <param name="inputStream">Schreibbarer Stream für Eingaben an den Prozess.</param>
    /// <param name="outputStream">Lesbarer Stream für die Prozessausgabe.</param>
    internal PseudoConsoleSession(PseudoConsole pseudoConsole, Process process, Stream inputStream, Stream outputStream)
    {
        _pseudoConsole = pseudoConsole;
        _process = process;
        InputStream = inputStream;
        OutputStream = outputStream;
    }

    /// <summary>Ändert die Größe der Pseudo Console.</summary>
    /// <param name="cols">Neue Spaltenanzahl.</param>
    /// <param name="rows">Neue Zeilenanzahl.</param>
    /// <returns><c>true</c>, wenn die Größenänderung erfolgreich war; andernfalls <c>false</c>.</returns>
    public bool Resize(int cols, int rows)
        => _pseudoConsole.Resize((short)cols, (short)rows);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { InputStream.Dispose(); } catch { }
        try { OutputStream.Dispose(); } catch { }
        try { _pseudoConsole.Dispose(); } catch { }
        try { _process.Dispose(); } catch { }
    }
}
