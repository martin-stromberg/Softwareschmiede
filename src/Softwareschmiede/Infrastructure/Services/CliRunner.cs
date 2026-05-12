using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>
/// Führt CLI-Prozesse aus. stdout und stderr werden IMMER parallel und asynchron gelesen,
/// um Deadlocks durch volle Puffer zu vermeiden. Tokens werden als Umgebungsvariablen übergeben,
/// niemals als CLI-Argumente. Prozess-Cleanup via Kill(entireProcessTree: true).
/// </summary>
public sealed class CliRunner : ICliRunner
{
    private readonly ILogger<CliRunner> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="CliRunner"/>.</summary>
    public CliRunner(ILogger<CliRunner> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task<CliResult> RunAsync(
        string command,
        IEnumerable<string> args,
        string? workingDirectory,
        IDictionary<string, string>? environmentVariables,
        CancellationToken ct = default)
    {
        var argList = args.ToList();
        _logger.LogInformation(
            "Führe CLI-Befehl aus: {Command} mit {ArgCount} Argumenten in {WorkingDir}",
            command, argList.Count, workingDirectory);

        using var process = CreateProcess(command, argList, workingDirectory, environmentVariables);
        process.Start();

        // Schließe StandardInput sofort, damit CLI-Tools nicht versuchen, interaktive Eingaben zu lesen
        process.StandardInput.Close();

        // stdout und stderr PARALLEL lesen – verhindert Deadlock durch volle Puffer
        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdOutTask, stdErrTask);
        await process.WaitForExitAsync(ct);

        var result = new CliResult(process.ExitCode, stdOutTask.Result, stdErrTask.Result);

        if (!result.IsSuccess)
            _logger.LogWarning(
                "CLI-Befehl {Command} beendet mit Exit-Code {ExitCode}. StdErr: {StdErr}",
                command, result.ExitCode, result.StdErr);
        else
            _logger.LogInformation("CLI-Befehl {Command} erfolgreich abgeschlossen.", command);

        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        string command,
        IEnumerable<string> args,
        string? workingDirectory,
        IDictionary<string, string>? environmentVariables,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starte CLI-Streaming: {Command}", command);

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleWriter = true });
        Process? process = null;
        var outputCompletionSource = new TaskCompletionSource();

        try
        {
            process = CreateProcess(command, args, workingDirectory, environmentVariables);
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    channel.Writer.TryWrite(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    _logger.LogDebug("CLI-StdErr: {Line}", e.Data);
                    // stderr ebenfalls an den Aufrufer weiterleiten, damit Fehlermeldungen
                    // z.B. im Aufgaben-Protokoll erscheinen
                    channel.Writer.TryWrite($"[Fehler] {e.Data}");
                }
            };

            process.Exited += (_, _) =>
            {
                // Signalisiere, dass der Prozess beendet ist
                outputCompletionSource.SetResult();
            };

            process.Start();
            
            // Schließe StandardInput sofort, damit CLI-Tools nicht versuchen, interaktive Eingaben zu lesen
            process.StandardInput.Close();
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Starte das Warten auf den Prozess im Hintergrund
            var exitTask = process.WaitForExitAsync(ct);
            var outputTask = outputCompletionSource.Task;

            // Yield alle verfügbaren Zeilen, bis der Prozess beendet ist
            await foreach (var line in channel.Reader.ReadAllAsync(ct))
            {
                yield return line;
                
                // Prüfe, ob der Prozess beendet ist und alle Ausgabe gepuffert wurde
                if (outputTask.IsCompleted)
                {
                    // Gebe dem Output-Reader eine kurze Zeit, um verbleibende Daten zu verarbeiten
                    await Task.Delay(10, ct);
                }
            }

            if (process.ExitCode != 0)
                _logger.LogWarning(
                    "CLI-Streaming-Prozess {Command} beendet mit Exit-Code {ExitCode}",
                    command, process.ExitCode);
        }
        finally
        {
            // Stelle sicher, dass der Channel geschlossen ist
            channel.Writer.Complete();
            
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        _logger.LogInformation("Beende CLI-Prozess {Command} (entireProcessTree).", command);
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Prozess wurde nicht gestartet oder ist bereits beendet
                    _logger.LogDebug(ex, "Prozess konnte nicht beendet werden (wahrscheinlich nicht gestartet).");
                }
                process.Dispose();
            }
        }
    }

    private static Process CreateProcess(
        string command,
        IEnumerable<string> args,
        string? workingDirectory,
        IDictionary<string, string>? environmentVariables)
    {
        var resolvedCommand = ResolveExecutablePath(command);
        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedCommand,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // SICHERHEIT: ArgumentList statt Arguments verwenden (verhindert Command Injection)
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        // SICHERHEIT: Umgebungsvariablen setzen (Tokens werden NIE als CLI-Argumente übergeben)
        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
                startInfo.Environment[key] = value;
        }

        return new Process { StartInfo = startInfo };
    }

    /// <summary>
    /// Löst den vollständigen Pfad eines Executables auf. Zuerst wird die PATH-Umgebungsvariable
    /// durchsucht, dann bekannte Installationspfade als Fallback. Auf Windows werden zusätzlich
    /// .exe, .cmd und .bat-Erweiterungen geprüft. Falls das Executable nicht gefunden wird,
    /// wird der originale Befehlsname zurückgegeben.
    /// </summary>
    private static string ResolveExecutablePath(string command)
    {
        // Wenn bereits ein absoluter Pfad angegeben ist
        if (Path.IsPathRooted(command))
            return command;

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        // 1. PATH-Umgebungsvariable durchsuchen (sowohl Prozess- als auch System-PATH)
        var processPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var allPaths = $"{processPath}{Path.PathSeparator}{machinePath}{Path.PathSeparator}{userPath}";
        var pathDirs = allPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in pathDirs)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir.Trim(), command + ext);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        // 2. Bekannte Installationspfade als Fallback (z.B. GitHub CLI via winget/Installer)
        if (OperatingSystem.IsWindows())
        {
            var knownDirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GitHub CLI"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GitHub CLI"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages", "GitHub.cli_Microsoft.Winget.Source_8wekyb3d8bbwe", "cli-2*", "bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
                @"C:\tools\gh\bin",
            };

            foreach (var dir in knownDirs)
            {
                // Wildcard-Unterstützung für versionsabhängige Pfade
                if (dir.Contains('*'))
                {
                    var parent = Path.GetDirectoryName(dir.Split('*')[0].TrimEnd('\\', '/')) ?? string.Empty;
                    if (Directory.Exists(parent))
                    {
                        var pattern = Path.GetFileName(dir.Split('*')[0]) + "*";
                        foreach (var subDir in Directory.GetDirectories(parent, pattern))
                        {
                            var binDir = Path.Combine(subDir, "bin");
                            foreach (var ext in extensions)
                            {
                                var fullPath = Path.Combine(binDir, command + ext);
                                if (File.Exists(fullPath))
                                    return fullPath;
                            }
                        }
                    }
                    continue;
                }

                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(dir, command + ext);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
        }

        // Fallback: originalen Befehlsnamen zurückgeben
        return command;
    }
}
