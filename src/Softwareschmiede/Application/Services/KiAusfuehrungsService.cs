using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
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
    private static readonly TimeSpan ConPtyOutputDrainTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CliOutputWriterDrainTimeout = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<Guid, CliProcessHandle> _handles = new();
    private readonly ILogger<KiAusfuehrungsService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPseudoConsoleProcessLauncher _launcher;
    private volatile bool _isDisposed;

    /// <summary>Erstellt eine neue Instanz des <see cref="KiAusfuehrungsService"/>.</summary>
    /// <param name="logger">Logger-Instanz.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen kategoriespezifischer Logger (z. B. für <see cref="PseudoConsoleSession"/>).</param>
    /// <param name="scopeFactory">Factory für DI-Scopes (wird für Fehler-Persistierung verwendet).</param>
    /// <param name="launcher">Austauschpunkt für den ConPTY-Prozessstart. Bei <c>null</c> wird <see cref="Win32PseudoConsoleProcessLauncher"/> verwendet.</param>
    public KiAusfuehrungsService(ILogger<KiAusfuehrungsService> logger, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IPseudoConsoleProcessLauncher? launcher = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _scopeFactory = scopeFactory;
        _launcher = launcher ?? new Win32PseudoConsoleProcessLauncher(loggerFactory.CreateLogger<Win32PseudoConsoleProcessLauncher>(), loggerFactory);
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
    /// <param name="startConfig">Optionale Startkonfiguration des Repositories (z. B. Arbeitsverzeichnis).</param>
    /// <param name="gitPlugin">
    /// Optionales Git-Plugin, das zum Klonen des Repositories verwendet wurde (für die Auflösung des
    /// tatsächlichen Repository-Pfads, z. B. bei <c>LocalDirectoryPlugin</c> im <c>InSourceDirectory</c>-Modus).
    /// </param>
    /// <returns>Das <see cref="CliProcessHandle"/> des gestarteten Prozesses.</returns>
    public async Task<CliProcessHandle> StartCliAsync(
        Guid aufgabeId,
        IKiPlugin kiPlugin,
        string localRepoPath,
        string? optionalParameters = null,
        CancellationToken ct = default,
        RepositoryStartKonfiguration? startConfig = null,
        IGitPlugin? gitPlugin = null)
    {
        await _startLock.WaitAsync(ct).ConfigureAwait(false);
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

            var effectiveWorkdir = await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(localRepoPath, startConfig, gitPlugin, ct).ConfigureAwait(false);

            var psi = await kiPlugin.StartCliAsync(effectiveWorkdir, optionalParameters, ct).ConfigureAwait(false);

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

            process.Exited += (_, _) => HandleProcessExitedAsync(aufgabeId, process, handle, "Standard").SafeFireAndForget(_logger, "KiAusfuehrungsService.HandleProcessExitedAsync");

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
    /// <param name="startConfig">Optionale Startkonfiguration des Repositories (z. B. Arbeitsverzeichnis).</param>
    /// <param name="gitPlugin">
    /// Optionales Git-Plugin, das zum Klonen des Repositories verwendet wurde (für die Auflösung des
    /// tatsächlichen Repository-Pfads, z. B. bei <c>LocalDirectoryPlugin</c> im <c>InSourceDirectory</c>-Modus).
    /// </param>
    /// <returns>Das <see cref="CliProcessHandle"/> des gestarteten Prozesses.</returns>
    public async Task<CliProcessHandle> StartWithPseudoConsoleAsync(
        Guid aufgabeId,
        IKiPlugin kiPlugin,
        string localRepoPath,
        string? optionalParameters = null,
        CancellationToken ct = default,
        RepositoryStartKonfiguration? startConfig = null,
        IGitPlugin? gitPlugin = null)
    {
        await _startLock.WaitAsync(ct).ConfigureAwait(false);
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

            var effectiveWorkdir = await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(localRepoPath, startConfig, gitPlugin, ct).ConfigureAwait(false);

            // Plugin-Befehl ermitteln (FileName + Arguments) — wird nach cmd.exe-Start in die Konsole gesendet.
            var pluginPsi = await kiPlugin.StartCliAsync(effectiveWorkdir, optionalParameters, ct).ConfigureAwait(false);
            var pluginCommand = BuildCliCommand(pluginPsi);

            var outputWriter = new CliOutputProtokollWriter(
                aufgabeId,
                _scopeFactory,
                _loggerFactory.CreateLogger<CliOutputProtokollWriter>());

            Process process;
            PseudoConsoleSession session;
            IntPtr nativeProcessHandle;
            try
            {
                (process, session, nativeProcessHandle) = _launcher.Start(aufgabeId, effectiveWorkdir, pluginCommand, outputWriter);
            }
            catch
            {
                await outputWriter.CompleteAsync(CliOutputWriterDrainTimeout, ct).ConfigureAwait(false);
                throw;
            }

            var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Token sofort abgreifen: Wird er erst später (z. B. beim Fire-and-Forget-Aufruf weiter unten)
            // von sendCts gelesen, kann ein zwischenzeitlich auf einem anderen Thread ausgelöstes Exited-Event
            // sendCts bereits disposed haben (siehe CancelAndDisposeConPtyResourcesAsync) — der Zugriff auf
            // sendCts.Token würde dann selbst eine ObjectDisposedException werfen. Ein einmal (vor dem Dispose)
            // abgegriffener CancellationToken bleibt dagegen gültig auswertbar.
            var sendToken = sendCts.Token;
            var handle = new CliProcessHandle(aufgabeId, process)
            {
                PseudoConsoleSession = session,
                SendCts = sendCts,
                NativeProcessHandle = nativeProcessHandle,
                OutputSink = outputWriter
            };

            // EnableRaisingEvents vor der Handler-Registrierung setzen. So ist sichergestellt, dass
            // das Exited-Event nicht zwischen Prozessstart und Handler-Registrierung verloren geht.
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => HandleProcessExitedAsync(aufgabeId, process, handle, "ConPTY", () => CancelAndDisposeConPtyResourcesAsync(handle)).SafeFireAndForget(_logger, "KiAusfuehrungsService.HandleProcessExitedAsync");

            _handles[aufgabeId] = handle;

            // Wenn der Prozess bereits vor dem Setzen von EnableRaisingEvents beendet wurde,
            // wird das Exited-Event nicht mehr ausgelöst. Dann hier manuell bereinigen und
            // frühzeitig zurückkehren — kein Gestartet-Event für einen bereits beendeten Prozess.
            if (process.HasExited && _handles.TryRemove(aufgabeId, out var earlyExitHandle))
            {
                await CancelAndDisposeConPtyResourcesAsync(earlyExitHandle).ConfigureAwait(false);
                RaiseRunningCountChanged();
                CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestoppt);
                return handle;
            }

            _logger.LogInformation("CLI-Prozess (ConPTY) für Aufgabe {AufgabeId} gestartet (PID: {Pid}).", aufgabeId, process.Id);
            RaiseRunningCountChanged();
            CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestartet);

            // Plugin-Befehl verzögert senden: cmd.exe braucht ~200ms bis der Prompt bereit ist.
            if (!string.IsNullOrEmpty(pluginCommand))
            {
                SendCommandDelayedAsync(session, pluginCommand, aufgabeId, sendToken).SafeFireAndForget(_logger, "KiAusfuehrungsService.SendCommandDelayedAsync");
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
            var exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
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

        return TryGetExitCode(handle.Process, handle.NativeProcessHandle);
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

                CancelAndDisposeConPtyResourcesAsync(handle).GetAwaiter().GetResult();
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
                $"CLI-Prozess mit Fehler beendet (ExitCode: {exitCode}). Aufgabe bleibt im Status Gestartet — CLI-Start kann erneut versucht werden.").ConfigureAwait(false);

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

    /// <summary>
    /// Gemeinsame Behandlung des <see cref="Process.Exited"/>-Events für klassischen und ConPTY-Start:
    /// Ermittelt Exit-Code und Status, persistiert Fehler bei Bedarf und löst <see cref="CliProcessStatusChanged"/> aus.
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="process">Der beendete Prozess.</param>
    /// <param name="handle">Das zugehörige <see cref="CliProcessHandle"/>.</param>
    /// <param name="logKontext">Bezeichnung des Start-Modus für die Log-Ausgabe (z. B. "Standard" oder "ConPTY").</param>
    /// <param name="vorAufraeumenAsync">Optionale zusätzliche Aufräumlogik (z. B. Drain und Dispose der PseudoConsoleSession), die nach der Handle-Entfernung, aber vor der Statusermittlung ausgeführt wird.</param>
    private async Task HandleProcessExitedAsync(Guid aufgabeId, Process process, CliProcessHandle handle, string logKontext, Func<Task>? vorAufraeumenAsync = null)
    {
        try
        {
            // TryRemove ist atomar: gibt false zurück, wenn der Prozess bereits über den
            // HasExited-Check nach dem Start bereinigt wurde. So wird jede Aktion genau einmal ausgeführt.
            if (!_handles.TryRemove(aufgabeId, out _))
            {
                return;
            }

            // Exit-Code VOR dem Aufräumen ermitteln: CancelAndDisposeConPtyResourcesAsync (vorAufraeumen)
            // schließt handle.NativeProcessHandle - danach wäre GetExitCodeProcess nicht mehr möglich.
            var exitCode = TryGetExitCode(process, handle.NativeProcessHandle);

            if (vorAufraeumenAsync is not null)
                await vorAufraeumenAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "CLI-Prozess ({LogKontext}) für Aufgabe {AufgabeId} beendet (ExitCode: {ExitCode}).",
                logKontext,
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
                PersistFehlgeschlagenAsync(aufgabeId, exitCode.Value).SafeFireAndForget(_logger, "KiAusfuehrungsService.PersistFehlgeschlagenAsync");
            }
            else
            {
                status = CliProcessStatus.Gestoppt;
            }

            CliProcessStatusChanged?.Invoke(aufgabeId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler im Exited-Handler ({LogKontext}) für Aufgabe {AufgabeId}.", logKontext, aufgabeId);
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

    /// <summary>
    /// Ermittelt den Exit-Code eines beendeten Prozesses. Beim ConPTY-Start (erkennbar an
    /// <paramref name="nativeProcessHandle"/> != <see cref="IntPtr.Zero"/>) wird bewusst
    /// <c>GetExitCodeProcess</c> auf dem nativen Handle genutzt statt <see cref="Process.ExitCode"/>:
    /// Das <see cref="Process"/>-Objekt stammt dort aus <see cref="Process.GetProcessById(int)"/> und
    /// kann, wenn dessen PID zwischenzeitlich einem anderen (bereits beendeten) Prozess zugeordnet
    /// wurde, mit <see cref="InvalidOperationException"/> ("No process is associated with this
    /// object") fehlschlagen. Das native Handle ist dagegen unabhängig von PID-Wiederverwendung
    /// eindeutig an den ursprünglichen Prozess gebunden.
    /// </summary>
    /// <param name="process">Der Prozess, dessen Exit-Code ermittelt werden soll.</param>
    /// <param name="nativeProcessHandle">Natives Win32-Handle aus <c>CreateProcess</c> (ConPTY-Start), oder <see cref="IntPtr.Zero"/> beim klassischen Start.</param>
    /// <returns>Der Exit-Code, oder <c>null</c> wenn der Prozess noch läuft oder nicht ermittelbar ist.</returns>
    private int? TryGetExitCode(Process process, IntPtr nativeProcessHandle = default)
    {
        if (nativeProcessHandle != IntPtr.Zero)
        {
            if (!PseudoConsoleNativeMethods.GetExitCodeProcess(nativeProcessHandle, out var rawExitCode))
            {
                _logger.LogWarning("GetExitCodeProcess für ConPTY-Prozess fehlgeschlagen (Win32-Fehler {Win32Error}).", Marshal.GetLastWin32Error());
                return null;
            }
            return rawExitCode == PseudoConsoleNativeMethods.STILL_ACTIVE ? null : unchecked((int)rawExitCode);
        }

        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception ex)
        {
            // Bewusst geloggt statt stillschweigend verschluckt: Ein via Process.GetProcessById()
            // erzeugtes Process-Objekt kann hier InvalidOperationException ("No process is
            // associated with this object") werfen, wenn die PID zwischenzeitlich einem anderen
            // (bereits beendeten) Prozess zugeordnet wurde. Das vorherige "return null" verschleierte
            // diese Fehlerursache und liess einen echten Exit-Code faelschlich wie einen legitimen
            // "kein Exit-Code" (null) aussehen.
            _logger.LogWarning(ex, "Exit-Code für CLI-Prozess konnte nicht ermittelt werden.");
            return null;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
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
            await Task.Delay(300, ct).ConfigureAwait(false);
            var bytes = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
            await session.InputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await session.InputStream.FlushAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Plugin-Befehl an cmd.exe gesendet für Aufgabe {AufgabeId}: {Command}", aufgabeId, command);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "Plugin-Befehl konnte nicht an cmd.exe gesendet werden für Aufgabe {AufgabeId}, da der Prozess bereits beendet und die Session disposed wurde.", aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin-Befehl konnte nicht an cmd.exe gesendet werden für Aufgabe {AufgabeId}.", aufgabeId);
        }
    }

    /// <summary>
    /// Storniert den ggf. noch ausstehenden verzögerten Plugin-Befehlsversand (<see cref="SendCommandDelayedAsync"/>)
    /// und disposed anschließend CancellationTokenSource und <see cref="PseudoConsoleSession"/> des Handles.
    /// Verhindert, dass nach dem Dispose der Session noch versucht wird, in den bereits geschlossenen
    /// Input-Stream zu schreiben (Race Condition bei sehr kurzlebigen ConPTY-Kindprozessen).
    /// </summary>
    /// <param name="handle">Das Handle, dessen ConPTY-Ressourcen bereinigt werden sollen.</param>
    private async Task CancelAndDisposeConPtyResourcesAsync(CliProcessHandle handle)
    {
        try
        {
            handle.SendCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        handle.SendCts?.Dispose();

        if (handle.PseudoConsoleSession is not null)
            await handle.PseudoConsoleSession.DrainOutputAsync(ConPtyOutputDrainTimeout).ConfigureAwait(false);

        handle.PseudoConsoleSession?.Dispose();

        if (handle.OutputSink is not null)
            await handle.OutputSink.CompleteAsync(CliOutputWriterDrainTimeout).ConfigureAwait(false);

        if (handle.NativeProcessHandle != IntPtr.Zero)
        {
            PseudoConsoleNativeMethods.CloseHandle(handle.NativeProcessHandle);
            handle.NativeProcessHandle = IntPtr.Zero;
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

    /// <summary>
    /// Natives Win32-Prozess-Handle aus <c>CreateProcess</c> (nur beim ConPTY-Start gesetzt, sonst
    /// <see cref="IntPtr.Zero"/>). Wird bewusst offen gehalten statt sofort geschlossen, damit der
    /// Exit-Code zuverlässig per <c>GetExitCodeProcess</c> gelesen werden kann - im Gegensatz zu
    /// <see cref="Process.GetProcessById(int)"/>-Objekten, die nach Wiederverwendung der PID durch
    /// einen anderen (bereits beendeten) Prozess mit <see cref="InvalidOperationException"/>
    /// ("No process is associated with this object") auf <see cref="Process.HasExited"/>/
    /// <see cref="Process.ExitCode"/> fehlschlagen koennen. Muss ueber
    /// <c>CancelAndDisposeConPtyResourcesAsync</c> geschlossen werden.
    /// </summary>
    public IntPtr NativeProcessHandle { get; set; }

    /// <summary>
    /// Koppelt den verzögerten Plugin-Befehlsversand (<see cref="KiAusfuehrungsService.SendCommandDelayedAsync"/>)
    /// an das Prozess-Lebensende: Wird beim <see cref="Process.Exited"/>-Event storniert, damit kein Zugriff
    /// auf die bereits disposte <see cref="PseudoConsoleSession"/> erfolgt. Nur beim ConPTY-Start gesetzt.
    /// </summary>
    public CancellationTokenSource? SendCts { get; set; }

    /// <summary>Optionale Senke fuer Terminal-Ausgabe, die beim Aufraeumen abgeschlossen wird.</summary>
    public ITerminalOutputSink? OutputSink { get; set; }

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
