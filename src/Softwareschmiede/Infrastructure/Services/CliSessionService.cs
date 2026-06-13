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
    private CancellationTokenSource? _loopCts;
    private Task? _outputLoopTask;
    private Task? _stderrLoopTask;

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

        _loopCts?.Cancel();
        try
        {
            if (_outputLoopTask is not null) await _outputLoopTask.ConfigureAwait(false);
            if (_stderrLoopTask is not null) await _stderrLoopTask.ConfigureAwait(false);
        }
        catch { }

        _loopCts?.Dispose();
        _loopCts = new CancellationTokenSource();

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

        var loopToken = _loopCts.Token;
        _outputLoopTask = Task.Run(() => ReadOutputLoop(loopToken), loopToken);
        _stderrLoopTask = Task.Run(() => DrainStderrLoop(loopToken), loopToken);

        _logger.LogInformation("CLI-Prozess '{CliName}' gestartet (PID: {Pid}).", cliName, _process.Id);

        await Task.CompletedTask;
    }

    private async Task ReadOutputLoop(CancellationToken ct)
    {
        if (_process == null)
            return;

        try
        {
            string? line;
            while (!ct.IsCancellationRequested
                && (line = await _process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) != null)
            {
                if (_onOutput != null)
                    await _onOutput(line + "\n").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
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

    private async Task DrainStderrLoop(CancellationToken ct)
    {
        if (_process == null)
            return;

        try
        {
            while (!ct.IsCancellationRequested
                && await _process.StandardError.ReadLineAsync(ct).ConfigureAwait(false) != null)
            {
            }
        }
        catch (OperationCanceledException)
        {
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

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        _loopCts?.Cancel();

        try
        {
            if (_outputLoopTask is not null) await _outputLoopTask.ConfigureAwait(false);
            if (_stderrLoopTask is not null) await _stderrLoopTask.ConfigureAwait(false);
        }
        catch { }

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Beenden des CLI-Prozesses.");
            }
        }

        _stdin?.Dispose();
        _stdin = null;
        _process?.Dispose();
        _process = null;
        _loopCts?.Dispose();
        _loopCts = null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
