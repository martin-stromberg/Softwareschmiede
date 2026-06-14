using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Service, der laufende CLI-Prozesse für KI-Ausführungen verwaltet.
/// Startet, stoppt und überwacht CLI-Prozesse pro Aufgabe.
/// </summary>
public sealed class KiAusfuehrungsService : IRunningAutomationStatusSource, IDisposable
{
    private readonly ConcurrentDictionary<Guid, CliProcessHandle> _handles = new();
    private readonly ILogger<KiAusfuehrungsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Erstellt eine neue Instanz des <see cref="KiAusfuehrungsService"/>.</summary>
    public KiAusfuehrungsService(ILogger<KiAusfuehrungsService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Wird ausgelöst, wenn ein CLI-Prozess gestartet, gestoppt oder ein Fehler aufgetreten ist.</summary>
    public event Action<Guid, CliProcessStatus>? CliProcessStatusChanged;

    /// <inheritdoc/>
    public event Action<int, int>? RunningCountChanged;

    /// <inheritdoc/>
    public bool IsRunning(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
        {
            return false;
        }

        try { return !handle.Process.HasExited; }
        catch { return false; }
    }

    /// <inheritdoc/>
    public int GetRunningCount()
        => _handles.Values.Count(h =>
        {
            try { return !h.Process.HasExited; }
            catch { return false; }
        });

    private readonly SemaphoreSlim _startLock = new(1, 1);

    /// <summary>Startet einen CLI-Prozess für eine Aufgabe und gibt das Handle zurück.</summary>
    public async Task<CliProcessHandle> StartCliAsync(
        Guid aufgabeId,
        IKiPlugin kiPlugin,
        string localRepoPath,
        string? optionalParameters = null,
        CancellationToken ct = default)
    {
        await _startLock.WaitAsync(ct);
        try
        {
            if (_handles.TryGetValue(aufgabeId, out var existing))
            {
                bool istNochAktiv;
                try { istNochAktiv = !existing.Process.HasExited; }
                catch { istNochAktiv = false; }

                if (istNochAktiv)
                {
                    _logger.LogWarning("CLI-Prozess für Aufgabe {AufgabeId} läuft bereits – zweiter Start abgewiesen.", aufgabeId);
                    return existing;
                }
            }

            var psi = await kiPlugin.StartCliAsync(localRepoPath, optionalParameters, ct);

            _logger.LogInformation("CLI-Prozess für Aufgabe {AufgabeId} starten.", aufgabeId);

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var handle = new CliProcessHandle(aufgabeId, process);

            process.Exited += (_, _) =>
            {
                var exitCode = TryGetExitCode(process);
                _logger.LogInformation(
                    "CLI-Prozess für Aufgabe {AufgabeId} beendet (ExitCode: {ExitCode}).",
                    aufgabeId,
                    exitCode);

                _handles.TryRemove(aufgabeId, out _);

                RaiseRunningCountChanged();

                CliProcessStatus status;
                if (handle.AbsichtlichGestoppt)
                {
                    status = CliProcessStatus.Gestoppt;
                }
                else if (exitCode.HasValue && exitCode.Value != 0)
                {
                    status = CliProcessStatus.Fehler;
                    _ = PersistFehlgeschlagenAsync(aufgabeId);
                }
                else
                {
                    status = CliProcessStatus.Gestoppt;
                }

                CliProcessStatusChanged?.Invoke(aufgabeId, status);
            };

            // Handle VOR process.Start() eintragen, damit der Exited-Handler
            // das Handle immer vorfindet (Befund #3).
            _handles[aufgabeId] = handle;

            if (!process.Start())
            {
                _handles.TryRemove(aufgabeId, out _);
                throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
            }

            _logger.LogInformation("CLI-Prozess für Aufgabe {AufgabeId} gestartet (PID: {Pid}).", aufgabeId, process.Id);
            RaiseRunningCountChanged();
            CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestartet);

            return handle;
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>Stoppt den laufenden CLI-Prozess für eine Aufgabe (SIGTERM → 5s → Kill).</summary>
    public async Task StopCliAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
        {
            return;
        }

        var process = handle.Process;
        if (process.HasExited)
        {
            return;
        }

        handle.AbsichtlichGestoppt = true;

        _logger.LogInformation("CLI-Prozess für Aufgabe {AufgabeId} beenden.", aufgabeId);

        try
        {
            process.CloseMainWindow();
            var exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(5), ct);
            if (!exited)
            {
                _logger.LogWarning("CLI-Prozess für Aufgabe {AufgabeId} antwortet nicht – Kill.", aufgabeId);
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Beenden des CLI-Prozesses für Aufgabe {AufgabeId}.", aufgabeId);
        }
    }

    /// <summary>Gibt den Exit-Code des letzten Prozesses zurück.</summary>
    public int? GetLastExitCode(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
        {
            return null;
        }

        return TryGetExitCode(handle.Process);
    }

    /// <summary>Aktualisiert LastHeartbeatUtc der Aufgabe (für externe Nutzung durch AufgabeService).</summary>
    public void UpdateHeartbeat(Guid aufgabeId)
    {
        if (_handles.TryGetValue(aufgabeId, out var handle))
        {
            handle.LastHeartbeat = DateTimeOffset.UtcNow;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var handle in _handles.Values)
        {
            try
            {
                if (!handle.Process.HasExited)
                {
                    handle.Process.Kill(entireProcessTree: true);
                }

                handle.Process.Dispose();
            }
            catch (Exception)
            {
            }
        }

        _handles.Clear();
        _startLock.Dispose();
    }

    private async Task PersistFehlgeschlagenAsync(Guid aufgabeId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            await aufgabeService.StatusSetzenAsync(aufgabeId, AufgabeStatus.Beendet);
            _logger.LogInformation("Aufgabe {AufgabeId}: Status nach Fehler auf Beendet gesetzt.", aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Persistieren des Beendet-Status für Aufgabe {AufgabeId}.", aufgabeId);
        }
    }

    private void RaiseRunningCountChanged()
    {
        int previous;
        int current;
        lock (_runningCountLock)
        {
            previous = _previousRunningCount;
            current = GetRunningCount();
            _previousRunningCount = current;
        }

        RunningCountChanged?.Invoke(previous, current);
    }

    private int _previousRunningCount;
    private readonly object _runningCountLock = new();

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Handle auf einen laufenden CLI-Prozess.</summary>
public sealed class CliProcessHandle
{
    /// <summary>Aufgaben-ID zu der dieser Prozess gehört.</summary>
    public Guid AufgabeId { get; }

    /// <summary>Der verwaltete Prozess.</summary>
    public Process Process { get; }

    /// <summary>Zeitstempel des letzten Heartbeats.</summary>
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gibt an, ob der Prozess absichtlich durch <see cref="KiAusfuehrungsService.StopCliAsync"/> beendet wurde.</summary>
    public bool AbsichtlichGestoppt { get; set; }

    /// <summary>Erstellt ein neues Handle.</summary>
    public CliProcessHandle(Guid aufgabeId, Process process)
    {
        AufgabeId = aufgabeId;
        Process = process;
    }
}

/// <summary>Status eines CLI-Prozesses.</summary>
public enum CliProcessStatus
{
    Gestartet,
    Gestoppt,
    Fehler
}
