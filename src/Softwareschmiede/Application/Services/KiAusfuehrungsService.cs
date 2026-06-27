using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;

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
    /// <param name="logger">Logger-Instanz.</param>
    /// <param name="scopeFactory">Factory für DI-Scopes (wird für Fehler-Persistierung verwendet).</param>
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
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Den laufenden <see cref="System.Diagnostics.Process"/>, oder null.</returns>
    public System.Diagnostics.Process? GetRunningProcess(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
            return null;
        try { return !handle.Process.HasExited ? handle.Process : null; }
        catch { return null; }
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
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="kiPlugin">Das zu verwendende KI-Plugin.</param>
    /// <param name="localRepoPath">Pfad zum lokalen Repository-Verzeichnis.</param>
    /// <param name="optionalParameters">Optionale zusätzliche Parameter für den CLI-Start.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Das <see cref="CliProcessHandle"/> des gestarteten Prozesses.</returns>
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
            if (!psi.UseShellExecute && !psi.EnvironmentVariables.ContainsKey("PATH"))
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

    /// <summary>Startet einen CLI-Prozess für eine Aufgabe über die Windows Pseudo Console API.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="kiPlugin">Das zu verwendende KI-Plugin.</param>
    /// <param name="localRepoPath">Pfad zum lokalen Repository-Verzeichnis.</param>
    /// <param name="optionalParameters">Optionale zusätzliche Parameter für den CLI-Start.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Das <see cref="CliProcessHandle"/> des gestarteten Prozesses.</returns>
    public async Task<CliProcessHandle> StartWithPseudoConsoleAsync(
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

            // Plugin-Befehl ermitteln (FileName + Arguments) — wird nach cmd.exe-Start in die Konsole gesendet.
            var pluginPsi = await kiPlugin.StartCliAsync(localRepoPath, optionalParameters, ct);
            var pluginCommand = BuildCliCommand(pluginPsi);

            var workingDir = !string.IsNullOrEmpty(localRepoPath) && Directory.Exists(localRepoPath)
                ? localRepoPath
                : Path.GetTempPath();
            var psi = new System.Diagnostics.ProcessStartInfo
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

            System.Diagnostics.Process process;
            try
            {
                process = System.Diagnostics.Process.GetProcessById(startResult.Pid);
                // Das von CreateProcess zurückgegebene Win32-Handle schließen; der verwaltete
                // Process-Handle aus GetProcessById reicht für das weitere Lifecycle-Management.
                PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
            }
            catch
            {
                PseudoConsoleNativeMethods.CloseHandle(startResult.ProcessHandle);
                pseudoConsole.Dispose();
                throw;
            }

            var inputStream = new System.IO.FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.InputWritePipe, ownsHandle: false),
                System.IO.FileAccess.Write,
                bufferSize: 1,
                isAsync: false);

            var outputStream = new System.IO.FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.OutputReadPipe, ownsHandle: false),
                System.IO.FileAccess.Read,
                bufferSize: 4096,
                isAsync: false);

            var session = new PseudoConsoleSession(pseudoConsole, process, inputStream, outputStream);
            var handle = new CliProcessHandle(aufgabeId, process) { PseudoConsoleSession = session };

            // EnableRaisingEvents vor der Handler-Registrierung setzen. So ist sichergestellt, dass
            // das Exited-Event nicht zwischen Prozessstart und Handler-Registrierung verloren geht.
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                // TryRemove ist atomar: gibt false zurück, wenn der Prozess bereits über den
                // HasExited-Check unten bereinigt wurde. So wird jede Aktion genau einmal ausgeführt.
                if (!_handles.TryRemove(aufgabeId, out var removedHandle))
                    return;

                removedHandle.PseudoConsoleSession?.Dispose();

                var exitCode = TryGetExitCode(process);
                _logger.LogInformation(
                    "CLI-Prozess (ConPTY) für Aufgabe {AufgabeId} beendet (ExitCode: {ExitCode}).",
                    aufgabeId,
                    exitCode);

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

            _handles[aufgabeId] = handle;

            // Wenn der Prozess bereits vor dem Setzen von EnableRaisingEvents beendet wurde,
            // wird das Exited-Event nicht mehr ausgelöst. Dann hier manuell bereinigen und
            // frühzeitig zurückkehren — kein Gestartet-Event für einen bereits beendeten Prozess.
            if (process.HasExited && _handles.TryRemove(aufgabeId, out var earlyExitHandle))
            {
                earlyExitHandle.PseudoConsoleSession?.Dispose();
                RaiseRunningCountChanged();
                CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestoppt);
                return handle;
            }

            _logger.LogInformation("CLI-Prozess (ConPTY) für Aufgabe {AufgabeId} gestartet (PID: {Pid}).", aufgabeId, startResult.Pid);
            RaiseRunningCountChanged();
            CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestartet);

            // Plugin-Befehl verzögert senden: cmd.exe braucht ~200ms bis der Prompt bereit ist.
            if (!string.IsNullOrEmpty(pluginCommand))
            {
                _ = SendCommandDelayedAsync(session, pluginCommand, aufgabeId, ct);
            }

            return handle;
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>Gibt die <see cref="PseudoConsoleSession"/> für eine Aufgabe zurück, oder null wenn keine vorhanden.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Die <see cref="PseudoConsoleSession"/>, oder null.</returns>
    public PseudoConsoleSession? GetPseudoConsoleSession(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
            return null;
        return handle.PseudoConsoleSession;
    }

    /// <summary>Stoppt den laufenden CLI-Prozess für eine Aufgabe (SIGTERM → 5s → Kill).</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="ct">Abbruch-Token.</param>
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
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Der Exit-Code, oder null wenn kein Prozess bekannt ist.</returns>
    public int? GetLastExitCode(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
        {
            return null;
        }

        return TryGetExitCode(handle.Process);
    }

    /// <summary>Aktualisiert LastHeartbeatUtc der Aufgabe (für externe Nutzung durch AufgabeService).</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
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

                handle.PseudoConsoleSession?.Dispose();
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

            var protokollService = scope.ServiceProvider.GetRequiredService<ProtokollService>();
            await protokollService.AddEintragAsync(
                aufgabeId,
                ProtokollTyp.SystemMeldung,
                $"CLI-Prozess mit Fehler beendet (ExitCode: {exitCode}). Aufgabe bleibt im Status Gestartet — CLI-Start kann erneut versucht werden.");

            _logger.LogInformation("Aufgabe {AufgabeId}: CLI-Prozess mit Fehler beendet (ExitCode: {ExitCode}), Status bleibt unverändert.", aufgabeId, exitCode);
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

    private async Task SendCommandDelayedAsync(PseudoConsoleSession session, string command, Guid aufgabeId, CancellationToken ct = default)
    {
        try
        {
            await Task.Delay(300, ct);
            var bytes = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
            await session.InputStream.WriteAsync(bytes, ct);
            await session.InputStream.FlushAsync(ct);
            _logger.LogInformation("Plugin-Befehl an cmd.exe gesendet für Aufgabe {AufgabeId}: {Command}", aufgabeId, command);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin-Befehl konnte nicht an cmd.exe gesendet werden für Aufgabe {AufgabeId}.", aufgabeId);
        }
    }

    private static string BuildCliCommand(System.Diagnostics.ProcessStartInfo psi)
    {
        var fileName = psi.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        if (fileName.Contains(' '))
            fileName = $"\"{fileName}\"";

        return string.IsNullOrWhiteSpace(psi.Arguments)
            ? fileName
            : $"{fileName} {psi.Arguments}";
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

    /// <summary>Gibt an, ob der Prozess absichtlich durch <see cref="KiAusfuehrungsService.StopCliAsync"/> beendet wurde.</summary>
    public bool AbsichtlichGestoppt
    {
        get => _absichtlichGestoppt;
        set => _absichtlichGestoppt = value;
    }

    /// <summary>Die zugehörige <see cref="PseudoConsoleSession"/>, oder null bei klassischem Start.</summary>
    public PseudoConsoleSession? PseudoConsoleSession { get; set; }

    /// <summary>Erstellt ein neues Handle.</summary>
    /// <param name="aufgabeId">ID der zugehörigen Aufgabe.</param>
    /// <param name="process">Der verwaltete Prozess.</param>
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
