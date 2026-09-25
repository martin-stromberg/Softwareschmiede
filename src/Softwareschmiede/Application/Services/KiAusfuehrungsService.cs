using System.Collections.Concurrent;
using System.Diagnostics;
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
    private readonly ITerminalSessionFactory _sessionFactory;
    private volatile bool _isDisposed;

    /// <summary>Erstellt eine neue Instanz des <see cref="KiAusfuehrungsService"/>.</summary>
    /// <param name="logger">Logger-Instanz.</param>
    /// <param name="loggerFactory">Factory zum Erzeugen kategoriespezifischer Logger (z. B. für <see cref="PseudoConsoleSession"/>).</param>
    /// <param name="scopeFactory">Factory für DI-Scopes (wird für Fehler-Persistierung verwendet).</param>
    /// <param name="sessionFactory">Zentrale Erzeugung der interaktiven Terminal-Session (Auflösung, Preflight, Backend-Wahl).</param>
    public KiAusfuehrungsService(ILogger<KiAusfuehrungsService> logger, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, ITerminalSessionFactory sessionFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _scopeFactory = scopeFactory;
        _sessionFactory = sessionFactory;
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

    /// <summary>Startet einen CLI-Prozess für eine Aufgabe über eine interaktive Terminal-Session
    /// (PTY-Backend oder diagnostizierter Pipe-Fallback, gewählt durch <see cref="ITerminalSessionFactory"/>).</summary>
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
    public async Task<CliProcessHandle> StartTerminalSessionAsync(
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

            var spec = await kiPlugin.GetTerminalStartSpecAsync(effectiveWorkdir, optionalParameters, ct).ConfigureAwait(false);

            var outputWriter = new CliOutputProtokollWriter(
                aufgabeId,
                _scopeFactory,
                _loggerFactory.CreateLogger<CliOutputProtokollWriter>());

            TerminalSessionStartResult startResult;
            try
            {
                startResult = await _sessionFactory.StartAsync(aufgabeId, spec, outputWriter, kiPlugin.CheckHealthAsync, ct).ConfigureAwait(false);
            }
            catch
            {
                await outputWriter.CompleteAsync(CliOutputWriterDrainTimeout, ct).ConfigureAwait(false);
                throw;
            }

            var process = startResult.Process;
            var session = startResult.Session;
            var handle = new CliProcessHandle(aufgabeId, process)
            {
                Session = session,
                OutputSink = outputWriter
            };

            // Exit-/Fehlerbehandlung läuft über die Session — sie besitzt das native Prozess-Handle und
            // erkennt das Prozessende selbst (Process.Exited bzw. Ende des Output-Streams). Das Handle wird
            // vor der Event-Verdrahtung eingetragen, damit ein bereits ausgelöstes Exited/Failed das
            // Handle vorfindet (sonst würde HandleExitedCoreAsync es stillschweigend verwerfen).
            _handles[aufgabeId] = handle;

            session.Exited += (_, e) => HandleSessionEndedAsync(aufgabeId, handle, e.ExitCode, "Terminal").SafeFireAndForget(_logger, "KiAusfuehrungsService.HandleSessionEndedAsync");
            session.Failed += (_, e) => HandleSessionFailedAsync(aufgabeId, handle, e).SafeFireAndForget(_logger, "KiAusfuehrungsService.HandleSessionFailedAsync");

            // Wenn der Prozess bereits vor der Event-Verdrahtung beendet wurde, hat die Session ihr Exited
            // eventuell schon vor der Registrierung ausgelöst — dann hier manuell bereinigen (kein
            // Gestartet-Event für einen bereits beendeten Prozess).
            if (process.HasExited && _handles.TryRemove(aufgabeId, out var earlyExitHandle))
            {
                await DisposeSessionResourcesAsync(earlyExitHandle).ConfigureAwait(false);
                RaiseRunningCountChanged();
                await PersistAusfuehrungBeendetAsync(aufgabeId).ConfigureAwait(false);
                CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestoppt);
                return handle;
            }

            // Hat ein zwischenzeitlich ausgelöstes Session-Event (Exited/Failed) das Handle bereits
            // entfernt und den Endzustand gemeldet, darf kein Gestartet-Event mehr folgen.
            if (!_handles.TryGetValue(aufgabeId, out var registered) || !ReferenceEquals(registered, handle))
            {
                return handle;
            }

            _logger.LogInformation("CLI-Prozess (Terminal-Session) für Aufgabe {AufgabeId} gestartet (PID: {Pid}).", aufgabeId, process.Id);
            RaiseRunningCountChanged();
            CliProcessStatusChanged?.Invoke(aufgabeId, CliProcessStatus.Gestartet);

            return handle;
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>Gibt die <see cref="ITerminalSession"/> für eine Aufgabe zurück, oder null wenn keine vorhanden.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Die <see cref="ITerminalSession"/>, oder null.</returns>
    public ITerminalSession? GetTerminalSession(Guid aufgabeId)
    {
        if (!_handles.TryGetValue(aufgabeId, out var handle))
            return null;
        return handle.Session;
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
                await WaitForExitAsync(process, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
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

        return handle.Session?.ExitCode ?? TryGetExitCode(handle.Process);
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

                DisposeSessionResourcesAsync(handle).GetAwaiter().GetResult();
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
    /// <param name="vorAufraeumenAsync">Optionale zusätzliche Aufräumlogik (z. B. Drain und Dispose der Session), die nach der Handle-Entfernung, aber vor der Statusermittlung ausgeführt wird.</param>
    private async Task HandleProcessExitedAsync(Guid aufgabeId, Process process, CliProcessHandle handle, string logKontext, Func<Task>? vorAufraeumenAsync = null)
    {
        await HandleExitedCoreAsync(aufgabeId, handle, TryGetExitCode(process), logKontext, vorAufraeumenAsync).ConfigureAwait(false);
    }

    /// <summary>Behandelt das <see cref="ITerminalSession.Exited"/>-Ereignis einer Terminal-Session:
    /// Exit-Code kommt aus <see cref="TerminalSessionExitedEventArgs"/>, die Session-Ressourcen werden
    /// über <see cref="DisposeSessionResourcesAsync"/> aufgeräumt.</summary>
    private async Task HandleSessionEndedAsync(Guid aufgabeId, CliProcessHandle handle, int? exitCode, string logKontext)
    {
        await HandleExitedCoreAsync(aufgabeId, handle, exitCode, logKontext, () => DisposeSessionResourcesAsync(handle)).ConfigureAwait(false);
    }

    /// <summary>Behandelt das <see cref="ITerminalSession.Failed"/>-Ereignis einer Terminal-Session:
    /// ein fataler Laufzeitfehler wird wie ein Exit ohne Code behandelt (Status <see cref="CliProcessStatus.Fehler"/>).</summary>
    private async Task HandleSessionFailedAsync(Guid aufgabeId, CliProcessHandle handle, TerminalSessionFailedEventArgs args)
    {
        _logger.LogError(args.Error, "Terminal-Session für Aufgabe {AufgabeId} fehlgeschlagen (Phase: {Phase}).", aufgabeId, args.Phase);
        await HandleSessionEndedAsync(aufgabeId, handle, null, "Terminal-Fehler").ConfigureAwait(false);
    }

    private async Task HandleExitedCoreAsync(Guid aufgabeId, CliProcessHandle handle, int? exitCode, string logKontext, Func<Task>? vorAufraeumenAsync)
    {
        try
        {
            // TryRemove ist atomar: gibt false zurück, wenn der Prozess bereits über den
            // HasExited-Check nach dem Start bereinigt wurde. So wird jede Aktion genau einmal ausgeführt.
            if (!_handles.TryRemove(aufgabeId, out var removedHandle))
            {
                return;
            }

            // Falls zwischen Prozess-Start und Exited-Event ein neuer Prozess für dieselbe Aufgabe
            // gestartet wurde (z. B. Plugin-Wechsel), wurde der neue Handle entfernt. Diesen wieder
            // einsetzen und den alten Exit-Event ignorieren.
            if (!ReferenceEquals(removedHandle, handle))
            {
                _handles.TryAdd(aufgabeId, removedHandle);
                return;
            }

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

            await PersistAusfuehrungBeendetAsync(aufgabeId).ConfigureAwait(false);
            CliProcessStatusChanged?.Invoke(aufgabeId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler im Exited-Handler ({LogKontext}) für Aufgabe {AufgabeId}.", logKontext, aufgabeId);
        }
    }

    private async Task PersistAusfuehrungBeendetAsync(Guid aufgabeId)
    {
        if (_isDisposed)
        {
            _logger.LogWarning(
                "Ausführungsstatus für Aufgabe {AufgabeId} nicht persistiert, da der Dienst bereits beendet wird.",
                aufgabeId);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_isDisposed)
            {
                _logger.LogWarning(
                    "Ausführungsstatus für Aufgabe {AufgabeId} nicht persistiert, da der Dienst bereits beendet wird.",
                    aufgabeId);
                return;
            }

            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            await aufgabeService.AktivenLaufBeendenAsync(aufgabeId).ConfigureAwait(false);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "Ausführungsstatus für Aufgabe {AufgabeId} konnte nicht persistiert werden, da der ServiceProvider bereits disposed wurde.",
                aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Persistieren des beendeten Ausführungsstatus für Aufgabe {AufgabeId}.", aufgabeId);
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
    /// Ermittelt den Exit-Code eines beendeten Prozesses (nur klassischer Pipe-Start ohne Session —
    /// beim Terminal-Session-Pfad kommt der Code aus <see cref="TerminalSessionExitedEventArgs"/>).
    /// </summary>
    /// <param name="process">Der Prozess, dessen Exit-Code ermittelt werden soll.</param>
    /// <returns>Der Exit-Code, oder <c>null</c> wenn der Prozess noch läuft oder nicht ermittelbar ist.</returns>
    private int? TryGetExitCode(Process process)
    {
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

    /// <summary>
    /// Beendet die Session-Ressourcen eines Handles kontrolliert: erst auf den Leseschleifen-Drain
    /// warten (Tail-Output landet noch in der Protokoll-Senke), dann die Session und zuletzt die
    /// Output-Senke abschließen. Das native Prozess-Handle liegt in der Session und wird von deren
    /// <see cref="IDisposable.Dispose"/> geschlossen.
    /// </summary>
    /// <param name="handle">Das Handle, dessen Session-Ressourcen bereinigt werden sollen.</param>
    private async Task DisposeSessionResourcesAsync(CliProcessHandle handle)
    {
        if (handle.Session is not null)
            await handle.Session.DrainOutputAsync(ConPtyOutputDrainTimeout).ConfigureAwait(false);

        handle.Session?.Dispose();

        if (handle.OutputSink is not null)
            await handle.OutputSink.CompleteAsync(CliOutputWriterDrainTimeout).ConfigureAwait(false);
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

    /// <summary>Die zugehörige <see cref="ITerminalSession"/>, oder null bei klassischem Start.</summary>
    public ITerminalSession? Session { get; set; }

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
