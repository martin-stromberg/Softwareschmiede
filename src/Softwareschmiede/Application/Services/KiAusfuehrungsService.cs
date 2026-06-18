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
    private volatile bool _isDisposed;

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

    /// <summary>Gibt den laufenden Prozess für eine Aufgabe zurück, oder null wenn kein Prozess läuft.</summary>
    public System.Diagnostics.Process? GetRunningProcess(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
            return null;
        try { return !handle.Process.HasExited ? handle.Process : null; }
        catch { return null; }
    }

    /// <summary>
    /// Speichert das bekannte Fenster-Handle (HWND) des CLI-Prozesses für späteres Wieder-Einbetten.
    /// Wird vom View aufgerufen sobald das Fenster erstmalig eingebettet wurde.
    /// </summary>
    public void SetFensterHandle(Guid aufgabeId, IntPtr handle)
    {
        if (_handles.TryGetValue(aufgabeId, out var h))
            h.FensterHandle = handle;
    }

    /// <summary>
    /// Gibt das gespeicherte Fenster-Handle des CLI-Prozesses zurück, oder <see cref="IntPtr.Zero"/>
    /// wenn kein Handle gespeichert oder der Prozess nicht mehr aktiv ist.
    /// </summary>
    public IntPtr GetFensterHandle(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var h))
            return IntPtr.Zero;
        return h.FensterHandle;
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

            // Sicherstellen, dass der vollständige PATH des aktuellen Prozesses übergeben wird.
            // Bei UseShellExecute=false wird nur der Prozess-PATH genutzt — der kann bei WPF-Apps
            // kürzer sein als der vollständige Nutzer-PATH (z. B. fehlt npm/node bin-Verzeichnis).
            if (!psi.EnvironmentVariables.ContainsKey("PATH"))
            {
                psi.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            }

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
                    _ = PersistFehlgeschlagenAsync(aufgabeId, exitCode.Value);
                }
                else
                {
                    status = CliProcessStatus.Gestoppt;
                }

                CliProcessStatusChanged?.Invoke(aufgabeId, status);
            };

            // Handle VOR process.Start() eintragen, damit der Exited-Handler
            // das Handle immer vorfindet (Race-Condition bei sehr kurzlebigen Prozessen).
            _handles[aufgabeId] = handle;

            bool started;
            try
            {
                started = process.Start();
            }
            catch
            {
                _handles.TryRemove(aufgabeId, out _);
                throw;
            }

            if (!started)
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
        handle.AbsichtlichGestoppt = true;

        _logger.LogInformation("CLI-Prozess für Aufgabe {AufgabeId} beenden.", aufgabeId);

        try
        {
            if (process.HasExited)
            {
                return;
            }

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
        _isDisposed = true;

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

    private async Task PersistFehlgeschlagenAsync(Guid aufgabeId, int exitCode)
    {
        if (_isDisposed)
        {
            _logger.LogWarning(
                "Status nach Fehler für Aufgabe {AufgabeId} nicht persistiert, da der Dienst bereits beendet wird.",
                aufgabeId);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_isDisposed)
            {
                _logger.LogWarning(
                    "Status nach Fehler für Aufgabe {AufgabeId} nicht persistiert, da der Dienst bereits beendet wird.",
                    aufgabeId);
                return;
            }

            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            await aufgabeService.StatusSetzenAsync(aufgabeId, AufgabeStatus.Beendet);

            var protokollService = scope.ServiceProvider.GetRequiredService<ProtokollService>();
            await protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.SystemMeldung,
                $"CLI-Prozess mit Fehler beendet (ExitCode: {exitCode}).");

            _logger.LogInformation("Aufgabe {AufgabeId}: Status nach Fehler auf Beendet gesetzt (ExitCode: {ExitCode}).", aufgabeId, exitCode);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(
                ex,
                "Status nach Fehler für Aufgabe {AufgabeId} konnte nicht persistiert werden, da der ServiceProvider während des Shutdowns bereits disposed wurde.",
                aufgabeId);
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

    private volatile bool _absichtlichGestoppt;
    private IntPtr _fensterHandle = IntPtr.Zero;

    /// <summary>Gibt an, ob der Prozess absichtlich durch <see cref="KiAusfuehrungsService.StopCliAsync"/> beendet wurde.</summary>
    public bool AbsichtlichGestoppt
    {
        get => _absichtlichGestoppt;
        set => _absichtlichGestoppt = value;
    }

    /// <summary>
    /// Das bekannte HWND des CLI-Fensters. Wird beim ersten Einbetten gesetzt und
    /// für erneutes Einbetten nach Navigation-Zurück wiederverwendet, da SetParent
    /// das HWND nicht verändert.
    /// </summary>
    public IntPtr FensterHandle
    {
        get => _fensterHandle;
        set => _fensterHandle = value;
    }

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
    /// <summary>Prozess läuft.</summary>
    Gestartet,
    /// <summary>Prozess wurde gestoppt.</summary>
    Gestoppt,
    /// <summary>Prozess ist mit einem Fehler beendet.</summary>
    Fehler
}
