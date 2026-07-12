using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>
/// Startet den CLI-Kindprozess ohne echtes ConPTY über gewöhnliche STDIN/STDOUT-Umleitung.
/// Wird im E2E-Testmodus anstelle von <see cref="Win32PseudoConsoleProcessLauncher"/> verwendet, da
/// ein über die Windows-Pseudo-Console-API angehängter Kindprozess unter <c>dotnet test</c>/
/// <c>vstest.console.exe</c> unmittelbar nach dem Start beendet wird (siehe
/// docs/features/e2e-korrektur/requirement.md).
/// </summary>
public sealed class SimulatedPseudoConsoleProcessLauncher : IPseudoConsoleProcessLauncher
{
    private readonly ILogger<SimulatedPseudoConsoleProcessLauncher> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Erstellt eine neue Instanz von <see cref="SimulatedPseudoConsoleProcessLauncher"/>.</summary>
    /// <param name="logger">Logger für Diagnosemeldungen.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen des <see cref="PseudoConsoleSession"/>-Loggers.</param>
    public SimulatedPseudoConsoleProcessLauncher(ILogger<SimulatedPseudoConsoleProcessLauncher> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle) Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand)
    {
        var workingDir = !string.IsNullOrEmpty(effectiveWorkingDirectory) && Directory.Exists(effectiveWorkingDirectory)
            ? effectiveWorkingDirectory
            : Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        _logger.LogInformation("CLI-Prozess (simuliert, cmd.exe → {Command}) für Aufgabe {AufgabeId} starten.", pluginCommand, aufgabeId);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Simulierter CLI-Prozess konnte nicht gestartet werden.");
        process.BeginErrorReadLine();

        var session = new PseudoConsoleSession(
            NullPseudoConsoleHandle.Instance,
            process,
            process.StandardInput.BaseStream,
            process.StandardOutput.BaseStream,
            _loggerFactory.CreateLogger<PseudoConsoleSession>());

        return (process, session, IntPtr.Zero);
    }
}
