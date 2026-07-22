using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Startet den ConPTY-Kindprozess über die native Windows-Pseudo-Console-API.</summary>
public sealed class Win32PseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
{
    private readonly ILogger<Win32PseudoConsoleProcessLauncher> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Erstellt eine neue Instanz von <see cref="Win32PseudoConsoleProcessLauncher"/>.</summary>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen des <see cref="PseudoConsoleSession"/>-Loggers.</param>
    public Win32PseudoConsoleProcessLauncher(ILogger<Win32PseudoConsoleProcessLauncher> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle) Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand, ITerminalOutputSink? outputSink = null)
    {
        var workingDir = !string.IsNullOrEmpty(effectiveWorkingDirectory) && Directory.Exists(effectiveWorkingDirectory)
            ? effectiveWorkingDirectory
            : Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        _logger.LogInformation("CLI-Prozess (ConPTY, cmd.exe → {Command}) für Aufgabe {AufgabeId} starten.", pluginCommand, aufgabeId);

        var pseudoConsole = PseudoConsole.Create(220, 50);

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
            // Das native Win32-Handle aus CreateProcess bleibt bewusst offen (siehe
            // CliProcessHandle.NativeProcessHandle) - es wird erst beim endgültigen Aufräumen in
            // KiAusfuehrungsService.CancelAndDisposeConPtyResources geschlossen, nicht hier.
        }
        catch
        {
            PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
            pseudoConsole.Dispose();
            throw;
        }

        var session = CreatePseudoConsoleSession(aufgabeId, pseudoConsole, process, outputSink);

        return (process, session, startResult.ProcessHandle);
    }

    private PseudoConsoleSession CreatePseudoConsoleSession(Guid aufgabeId, PseudoConsole pseudoConsole, Process process, ITerminalOutputSink? outputSink)
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

            return new PseudoConsoleSession(pseudoConsole, process, inputStream, outputStream, _loggerFactory.CreateLogger<PseudoConsoleSession>(), outputSink);
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
