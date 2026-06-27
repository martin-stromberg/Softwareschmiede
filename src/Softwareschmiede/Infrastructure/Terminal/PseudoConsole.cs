using static Softwareschmiede.Infrastructure.Terminal.PseudoConsoleNativeMethods;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Kapselt einen Windows Pseudo Console (HPCON) Handle und die zugehörigen Pipe-Handles.</summary>
internal sealed class PseudoConsole : IDisposable
{
    private bool _disposed;

    /// <summary>Der HPCON-Handle der Pseudo Console.</summary>
    internal IntPtr Handle { get; }

    /// <summary>Der schreibbare Eingabe-Pipe-Endpunkt (Anwendungsseite).</summary>
    internal IntPtr InputWritePipe { get; }

    /// <summary>Der lesbare Ausgabe-Pipe-Endpunkt (Anwendungsseite).</summary>
    internal IntPtr OutputReadPipe { get; }

    private PseudoConsole(IntPtr handle, IntPtr inputWritePipe, IntPtr outputReadPipe)
    {
        Handle = handle;
        InputWritePipe = inputWritePipe;
        OutputReadPipe = outputReadPipe;
    }

    /// <summary>Erstellt eine neue Pseudo Console mit den angegebenen Abmessungen.</summary>
    /// <param name="cols">Spaltenanzahl der Pseudo Console.</param>
    /// <param name="rows">Zeilenanzahl der Pseudo Console.</param>
    /// <returns>Die erstellte <see cref="PseudoConsole"/>-Instanz.</returns>
    internal static PseudoConsole Create(short cols, short rows)
    {
        if (!CreatePipe(out var inputReadPipe, out var inputWritePipe, IntPtr.Zero, 0))
            throw new InvalidOperationException($"CreatePipe (Input) fehlgeschlagen: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");

        if (!CreatePipe(out var outputReadPipe, out var outputWritePipe, IntPtr.Zero, 0))
        {
            CloseHandle(inputReadPipe);
            CloseHandle(inputWritePipe);
            throw new InvalidOperationException($"CreatePipe (Output) fehlgeschlagen: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
        }

        var size = new COORD { X = cols, Y = rows };
        var hr = CreatePseudoConsole(size, inputReadPipe, outputWritePipe, 0, out var hpcon);

        CloseHandle(inputReadPipe);
        CloseHandle(outputWritePipe);

        if (hr != 0)
        {
            CloseHandle(inputWritePipe);
            CloseHandle(outputReadPipe);
            throw new InvalidOperationException($"CreatePseudoConsole fehlgeschlagen (HRESULT: 0x{hr:X8}).");
        }

        return new PseudoConsole(hpcon, inputWritePipe, outputReadPipe);
    }

    /// <summary>Ändert die Größe der Pseudo Console.</summary>
    /// <param name="cols">Neue Spaltenanzahl.</param>
    /// <param name="rows">Neue Zeilenanzahl.</param>
    internal void Resize(short cols, short rows)
    {
        var size = new COORD { X = cols, Y = rows };
        var hr = ResizePseudoConsole(Handle, size);
        if (hr != 0)
            throw new InvalidOperationException($"ResizePseudoConsole fehlgeschlagen (HRESULT: 0x{hr:X8}).");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        ClosePseudoConsole(Handle);
        CloseHandle(InputWritePipe);
        CloseHandle(OutputReadPipe);
    }
}
