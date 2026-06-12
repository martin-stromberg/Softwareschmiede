using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Verwaltet eine CLI-Session mit einem externen Prozess.</summary>
public sealed class CliSessionService : ICliSessionService
{
    private readonly ILogger<CliSessionService> _logger;

    private Process? _process;
    private StreamWriter? _stdin;
    private Func<string, Task>? _onOutput;

    /// <inheritdoc cref="CliSessionService"/>
    public CliSessionService(ILogger<CliSessionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRunning => _process != null && !_process.HasExited;

    /// <inheritdoc/>
    public async Task StartAsync(string cliName, string workingDir, Func<string, Task> onOutput)
    {
        if (IsRunning)
            return;

        _onOutput = onOutput;

        var psi = new ProcessStartInfo
        {
            FileName = cliName,
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"CLI-Prozess '{cliName}' konnte nicht gestartet werden.");
        _stdin = _process.StandardInput;

        _ = Task.Run(ReadOutputLoop);
        _ = Task.Run(DrainStderrLoop);

        _logger.LogInformation("CLI-Prozess '{CliName}' gestartet (PID: {Pid}).", cliName, _process.Id);

        await Task.CompletedTask;
    }

    private async Task ReadOutputLoop()
    {
        if (_process == null)
            return;

        try
        {
            while (!_process.HasExited)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line != null && _onOutput != null)
                    await _onOutput(line + "\n");
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Ausgabe-Loop beendet (Prozess wurde wahrscheinlich abgebrochen).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler im Ausgabe-Loop.");
        }
    }

    private async Task DrainStderrLoop()
    {
        if (_process == null)
            return;

        try
        {
            while (!_process.HasExited)
            {
                await _process.StandardError.ReadLineAsync();
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Stderr-Loop beendet (Prozess wurde wahrscheinlich abgebrochen).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler im Stderr-Loop.");
        }
    }

    /// <inheritdoc/>
    public Task SendAsync(string input)
    {
        _stdin?.WriteLine(input);
        _stdin?.Flush();
        return Task.CompletedTask;
    }
}
