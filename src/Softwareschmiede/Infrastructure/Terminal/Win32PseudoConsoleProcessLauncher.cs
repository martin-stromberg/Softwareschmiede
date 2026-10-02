using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Startet den ConPTY-Kindprozess über die native Windows-Pseudo-Console-API direkt aus
/// einer normalisierten <see cref="TerminalSessionStartSpec"/> (ohne cmd.exe-Hülle).</summary>
public sealed class Win32PseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
{
    private readonly ILogger<Win32PseudoConsoleProcessLauncher> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<TerminalSessionOptions> _options;

    /// <summary>Erstellt eine neue Instanz von <see cref="Win32PseudoConsoleProcessLauncher"/>.</summary>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen des <see cref="PseudoConsoleSession"/>-Loggers.</param>
    /// <param name="options">Terminal-Laufzeitparameter (initiale Größe, Replay-Budget).</param>
    public Win32PseudoConsoleProcessLauncher(ILogger<Win32PseudoConsoleProcessLauncher> logger, ILoggerFactory loggerFactory, IOptions<TerminalSessionOptions> options)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    /// <inheritdoc/>
    public bool IsPseudoTerminal => true;

    /// <inheritdoc/>
    public TerminalSessionStartResult Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink = null)
    {
        var workingDir = !string.IsNullOrEmpty(spec.WorkingDirectory) && Directory.Exists(spec.WorkingDirectory)
            ? spec.WorkingDirectory
            : Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        foreach (var entry in spec.EnvironmentVariables)
            psi.EnvironmentVariables[entry.Key] = entry.Value;

        _logger.LogInformation("CLI-Prozess (ConPTY, {FileName} {Arguments}) für Aufgabe {AufgabeId} starten.", spec.FileName, spec.Arguments, aufgabeId);

        var pseudoConsole = PseudoConsole.Create((short)_options.Value.DefaultCols, (short)_options.Value.DefaultRows);

        ProcessStartResult startResult;
        try
        {
            startResult = PseudoConsoleProcessStarter.Start(psi, pseudoConsole);
        }
        catch
        {
            pseudoConsole.Dispose();
            throw;
        }

        Process process;
        try
        {
            process = Process.GetProcessById(startResult.Pid);
            // Das native Win32-Handle aus CreateProcess bleibt bewusst offen — die PseudoConsoleSession
            // nutzt es für die PID-Wiederverwendungs-sichere Exit-Code-Ermittlung und schließt es in Dispose().
        }
        catch
        {
            PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
            pseudoConsole.Dispose();
            throw;
        }

        PseudoConsoleSession session;
        try
        {
            session = CreatePseudoConsoleSession(aufgabeId, pseudoConsole, process, startResult.ProcessHandle, outputSink);
        }
        catch
        {
            // Der ConPTY-Kindprozess läuft bereits — ohne Session müssen Prozess, natives
            // Prozess-Handle und Pseudo Console aufgeräumt werden, sonst läuft ein verwaister
            // Prozess ohne zugehörige Sitzung weiter.
            try { process.Kill(entireProcessTree: true); } catch { /* Best Effort. */ }
            try { process.Dispose(); } catch { /* Best Effort. */ }
            PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
            pseudoConsole.Dispose();
            throw;
        }

        return new TerminalSessionStartResult(process, session, IsPseudoTerminal: true);
    }

    private PseudoConsoleSession CreatePseudoConsoleSession(Guid aufgabeId, PseudoConsole pseudoConsole, Process process, IntPtr nativeProcessHandle, ITerminalOutputSink? outputSink)
    {
        FileStream? inputStream = null;
        FileStream? outputStream = null;
        try
        {
            inputStream = new FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.InputWritePipe, ownsHandle: false),
                FileAccess.Write,
                bufferSize: 1,
                isAsync: false);

            outputStream = new FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.OutputReadPipe, ownsHandle: false),
                FileAccess.Read,
                bufferSize: 4096,
                isAsync: false);

            return new PseudoConsoleSession(
                pseudoConsole,
                process,
                inputStream,
                outputStream,
                new PseudoConsoleSessionContext
                {
                    Logger = _loggerFactory.CreateLogger<PseudoConsoleSession>(),
                    OutputSink = outputSink,
                    NativeProcessHandle = nativeProcessHandle,
                    Options = _options.Value,
                    IsPseudoTerminal = true,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Anlegen der PseudoConsoleSession für Aufgabe {AufgabeId}.", aufgabeId);
            inputStream?.Dispose();
            outputStream?.Dispose();
            throw;
        }
    }
}
