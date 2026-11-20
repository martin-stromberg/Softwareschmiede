using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Verwaltung von CLI-Prozessen: Start, Stop, Heartbeat-Tracking.
/// Koordiniert <see cref="AiExecutionService"/> und <see cref="WorkItemService"/> für Heartbeat-Updates.
/// </summary>
public sealed class CliProcessManager : IDisposable
{
    private readonly AiExecutionService _kiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CliProcessManager> _logger;
    private readonly WorkItemRunDataChangedNotifier _laufdatenChangedNotifier;

    private readonly ConcurrentDictionary<Guid, Timer> _heartbeatTimers = new();
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    // Letztes Sicherheitsnetz der Laufdaten-Selbstheilung: unabhängig von jeglicher Event-Zustellung
    // wird periodisch geprüft, ob für jeden laufenden Prozess die Laufdaten persistiert sind.
    private static readonly TimeSpan ReconcileInterval = HeartbeatInterval;
    private readonly Timer _abgleichTimer;

    // Überlappungsschutz für den periodischen Abgleich: dauert ein Abgleich-Durchlauf länger als
    // das Timer-Intervall, werden weitere Ticks übersprungen statt sich aufzustauen (fire-and-forget).
    private int _abgleichLaeuft;

    // Serialisiert alle mutierenden Zugriffe auf _heartbeatTimers, _updateSemaphores und
    // _runtimeStatusSubscriptions: Die Check-then-act-Sequenzen in StartHeartbeat/
    // SubscribeRuntimeStatus werden aus parallelen Event- und Abgleichpfaden aufgerufen; ohne
    // diese Sperre könnten zwei Aufrufer einen verwaisten Heartbeat-Timer (permanenter 30-s-Tick
    // ohne Dictionary-Eintrag) bzw. eine doppelte, nicht mehr abmeldbare RuntimeStatusChanged-
    // Subscription hinterlassen. Blattsperre: Innerhalb der kritischen Abschnitte wird nie
    // gewartet/awaitet.
    private readonly object _lifecycleLock = new();

    // Wird in Dispose() unter _lifecycleLock gesetzt und dort von allen Timer-/Subscription-
    // Anlagepfaden geprüft: Ein in-flight AbgleichTickAsync-/Heartbeat-Tick oder ein knapp vor der
    // Event-Abmeldung eingetretener Event-Aufruf könnte sonst nach dem Leeren der Dictionaries
    // erneut einen Heartbeat-Timer bzw. eine RuntimeStatusChanged-Subscription anlegen, die nie
    // disposed/abgemeldet würde und bei weiter laufendem Prozess permanent weiter tickt
    // (Dispose-Race, Issue 342). Nur unter _lifecycleLock zugreifen.
    private bool _disposed;

    // Hält pro Aufgabe die ITerminalSession und den dafür registrierten Event-Handler, damit
    // RuntimeStatusChanged beim Stopp/Fehler wieder sauber abgemeldet werden kann (siehe
    // UnsubscribeRuntimeStatus). Ohne dieses Tracking müsste GetTerminalSession(aufgabeId) erneut
    // abgefragt werden — das Handle ist zu diesem Zeitpunkt aber bereits aus KiAusfuehrungsService._handles
    // entfernt (siehe HandleProcessExited), sodass die Session dort nicht mehr auffindbar wäre.
    private readonly ConcurrentDictionary<Guid, (ITerminalSession Session, EventHandler<CliRuntimeStatusChangedEventArgs> Handler)> _runtimeStatusSubscriptions = new();

    // Ein Semaphore pro Aufgabe statt eines einzelnen klassenweiten Semaphores: verhindert nur das
    // Überlappen von Timer-Ticks derselben Aufgabe, ohne die Heartbeat-Updates unabhängiger Aufgaben
    // gegeneinander zu serialisieren. Wird in StartHeartbeat angelegt und in StopHeartbeat entfernt.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _updateSemaphores = new();

    /// <summary>Erstellt eine neue Instanz des <see cref="CliProcessManager"/>.</summary>
    public CliProcessManager(
        AiExecutionService kiService,
        IServiceScopeFactory scopeFactory,
        ILogger<CliProcessManager> logger,
        WorkItemRunDataChangedNotifier laufdatenChangedNotifier)
    {
        _kiService = kiService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _laufdatenChangedNotifier = laufdatenChangedNotifier;

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
        _kiService.CliLaufAktiv += OnCliLaufAktiv;

        _abgleichTimer = new Timer(
            _ => ReconcileTickAsync().SafeFireAndForget(_logger, "CliProcessManager.AbgleichTickAsync"),
            null,
            ReconcileInterval,
            ReconcileInterval);
    }

    /// <summary>Startet den Heartbeat-Timer für eine Aufgabe.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    public void StartHeartbeat(Guid workItemId)
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            StopHeartbeatCore(workItemId);
            StartHeartbeatCore(workItemId);
        }
    }

    /// <summary>Stoppt den Heartbeat-Timer für eine Aufgabe.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    public void StopHeartbeat(Guid workItemId)
    {
        lock (_lifecycleLock)
        {
            StopHeartbeatCore(workItemId);
        }
    }

    // Aufrufer muss _lifecycleLock halten.
    private void StartHeartbeatCore(Guid workItemId)
    {
        _updateSemaphores[workItemId] = new SemaphoreSlim(1, 1);

        var timer = new Timer(
            _ => UpdateAsync(workItemId).SafeFireAndForget(_logger, "CliProcessManager.AktualisierungAsync"),
            null,
            HeartbeatInterval,
            HeartbeatInterval);

        _heartbeatTimers[workItemId] = timer;
        _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestartet.", workItemId);
    }

    // Aufrufer muss _lifecycleLock halten.
    private void StopHeartbeatCore(Guid workItemId)
    {
        if (_heartbeatTimers.TryRemove(workItemId, out var timer))
        {
            timer.Dispose();
            _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestoppt.", workItemId);
        }

        if (_updateSemaphores.TryRemove(workItemId, out var semaphore))
        {
            semaphore.Dispose();
        }
    }

    private async Task UpdateAsync(Guid workItemId)
    {
        if (!_updateSemaphores.TryGetValue(workItemId, out var semaphore))
        {
            // Heartbeat wurde zwischenzeitlich gestoppt (Semaphore bereits entfernt).
            return;
        }

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_kiService.IsRunning(workItemId))
            {
                StopHeartbeat(workItemId);
                return;
            }

            _kiService.UpdateHeartbeat(workItemId);
            await EnsureRunDataAsync(workItemId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat-Aktualisierung für Aufgabe {AufgabeId} fehlgeschlagen.", workItemId);
        }
        finally
        {
            try
            {
                semaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore wurde innerhalb dieses Aufrufs bereits über StopHeartbeat entfernt und disposed
                // (z. B. weil der Prozess als nicht mehr laufend erkannt wurde).
            }
        }
    }

    private void OnCliProcessStatusChanged(Guid workItemId, CliProcessStatus status)
    {
        try
        {
            switch (status)
            {
                case CliProcessStatus.Started:
                    StartHeartbeat(workItemId);
                    // AktiveRunId muss sofort persistiert werden (nicht erst beim ersten periodischen
                    // Heartbeat nach 30s) — sonst zeigt die Seitenleisten-Kachel (KiAusfuehrungsStatusConverter,
                    // Issue 108) für bis zu 30s nach dem Start weiterhin "✓ Bereit" statt "▶ Läuft" an.
                    SetActiveRunAsync(workItemId).SafeFireAndForget(_logger, "CliProcessManager.AktivenLaufSetzenAsync");
                    SubscribeRuntimeStatus(workItemId);
                    break;
                case CliProcessStatus.Gestoppt:
                case CliProcessStatus.Error:
                    StopHeartbeat(workItemId);
                    UnsubscribeRuntimeStatus(workItemId);
                    EndActiveRunAsync(workItemId).SafeFireAndForget(_logger, "CliProcessManager.AktivenLaufBeendenAsync");
                    break;
            }
        }
        catch (Exception ex)
        {
            // Ein Fehler beim Start/Stop des Heartbeat-Timers darf die Multicast-Invoke-Kette von
            // KiAusfuehrungsService.CliProcessStatusChanged nicht abbrechen, sonst würde der zweite
            // Abonnent (TaskDetailViewModel.OnCliProcessStatusChanged) nicht mehr benachrichtigt.
            _logger.LogError(ex, "Fehler bei der Verarbeitung des CLI-Prozess-Status {Status} für Aufgabe {AufgabeId}.", status, workItemId);
        }
    }

    /// <summary>
    /// Setzt <see cref="Domain.Entities.WorkItem.ActiveRunId"/> und aktualisiert den Heartbeat sofort,
    /// sobald ein CLI-Prozess gestartet wurde (siehe <see cref="WorkItemService.SetActiveRunAsync"/>).
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, deren CLI-Prozess gestartet wurde.</param>
    private async Task SetActiveRunAsync(Guid workItemId)
    {
        await ExecuteWithWorkItemServiceAsync(
            workItemId,
            "Setzen des aktiven Laufs",
            aufgabeService => aufgabeService.SetActiveRunAsync(
                workItemId,
                Guid.NewGuid().ToString("N"),
                laeuftGuard: () => _kiService.IsRunning(workItemId))).ConfigureAwait(false);
    }

    /// <summary>
    /// Entfernt <see cref="Domain.Entities.WorkItem.ActiveRunId"/>, sobald der CLI-Prozess einer Aufgabe
    /// beendet wurde (regulär oder mit Fehler; siehe <see cref="WorkItemService.EndActiveRunAsync"/>).
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, deren CLI-Prozess beendet wurde.</param>
    private async Task EndActiveRunAsync(Guid workItemId)
    {
        await ExecuteWithWorkItemServiceAsync(
            workItemId,
            "Beenden des aktiven Laufs",
            aufgabeService => aufgabeService.EndActiveRunAsync(workItemId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Abonniert <see cref="ITerminalSession.RuntimeStatusChanged"/> für die Terminal-Sitzung einer
    /// Aufgabe (sofern vorhanden — beim klassischen, nicht-interaktiven Start existiert keine
    /// Sitzung), damit Wechsel zwischen "arbeitet" und "wartet auf Eingabe" über
    /// <see cref="EnsureRunDataAsync"/> → <see cref="WorkItemService.EnsureActiveRunAsync"/>
    /// persistiert werden (Issue 108, Folgefehler des Rückwegs Läuft → Wartet: dieser Substatus wurde
    /// vorher ausschließlich lokal in der <see cref="ITerminalSession"/> gehalten und nie an die
    /// Datenbank weitergereicht). Wird aus allen Selbstheilungspfaden aufgerufen (Gestartet-Ereignis,
    /// CliLaufAktiv, Heartbeat-Tick, periodischer Abgleich); die Registrierung ist idempotent und
    /// unter <c>_lifecycleLock</c> atomar, damit parallele Aufrufer keine doppelte, nicht mehr
    /// abmeldbare Subscription hinterlassen.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, für deren laufenden Prozess der Substatus beobachtet werden soll.</param>
    private void SubscribeRuntimeStatus(Guid workItemId)
    {
        var session = _kiService.GetTerminalSession(workItemId);
        if (session is null)
        {
            // Klassischer Start ohne ConPTY: keine Sitzung, also kein Laufzeit-Substatus verfügbar.
            // KiAusfuehrungsStatusConverter fällt in diesem Fall auf das bisherige Verhalten zurück
            // (LaufStatus bleibt null → "▶ Läuft", solange AktiveRunId gesetzt und Heartbeat aktuell ist).
            return;
        }

        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            // Idempotent: Ist für die Aufgabe bereits dieselbe Session registriert (z. B. bei wiederholten
            // Aufrufen aus den Selbstheilungspfaden), wird nicht erneut registriert — sonst würde jeder
            // Statuswechsel den Persistierungsaufruf mehrfach auslösen.
            if (_runtimeStatusSubscriptions.TryGetValue(workItemId, out var existing)
                && ReferenceEquals(existing.Session, session))
            {
                return;
            }

            // Defensive Bereinigung einer eventuell noch vorhandenen alten Registrierung (z. B. wenn ein
            // Gestoppt-Event für einen vorherigen Lauf derselben Aufgabe ausblieb) — verhindert doppelte
            // Event-Registrierungen und einen dadurch überzähligen Persistierungsaufruf pro Statuswechsel.
            UnsubscribeRuntimeStatusCore(workItemId);

            EventHandler<CliRuntimeStatusChangedEventArgs> handler = (_, e) =>
                OnRuntimeStatusChanged(workItemId, e.Status);

            session.RuntimeStatusChanged += handler;
            _runtimeStatusSubscriptions[workItemId] = (session, handler);
        }
    }

    /// <summary>Meldet die Event-Registrierung aus <see cref="SubscribeRuntimeStatus"/> wieder ab.</summary>
    /// <param name="workItemId">ID der Aufgabe, deren CLI-Prozess beendet wurde.</param>
    private void UnsubscribeRuntimeStatus(Guid workItemId)
    {
        lock (_lifecycleLock)
        {
            UnsubscribeRuntimeStatusCore(workItemId);
        }
    }

    // Aufrufer muss _lifecycleLock halten.
    private void UnsubscribeRuntimeStatusCore(Guid workItemId)
    {
        if (_runtimeStatusSubscriptions.TryRemove(workItemId, out var subscription))
        {
            subscription.Session.RuntimeStatusChanged -= subscription.Handler;
        }
    }

    /// <summary>
    /// Übersetzt einen <see cref="CliRuntimeStatus"/>-Wechsel der <see cref="PseudoConsoleSession"/> in den
    /// Domain-Substatus <see cref="WorkItemRunStatus"/> und persistiert ihn. <see cref="CliRuntimeStatus.Inaktiv"/>
    /// wird ignoriert: Das Beenden des Laufs (inkl. Zurücksetzen von <see cref="Domain.Entities.WorkItem.RunStatus"/>)
    /// übernimmt bereits <see cref="EndActiveRunAsync"/> über das separate <see cref="CliProcessStatus.Gestoppt"/>-
    /// bzw. <see cref="CliProcessStatus.Error"/>-Ereignis.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, deren Laufzeit-Substatus sich geändert hat.</param>
    /// <param name="status">Der neue Laufzeitstatus der CLI-Sitzung.</param>
    private void OnRuntimeStatusChanged(Guid workItemId, CliRuntimeStatus status)
    {
        if (status == CliRuntimeStatus.Inaktiv)
            return;

        EnsureRunDataAsync(workItemId).SafeFireAndForget(_logger, "CliProcessManager.LaufdatenSicherstellenAsync");
    }

    /// <summary>Behandelt das <see cref="AiExecutionService.CliLaufAktiv"/>-Signal: ein laufender
    /// Prozess ohne Gestartet-Ereignis (Wiederverwendungs-/Early-Return-Pfad) löst dieselbe
    /// Laufdaten-Sicherstellung aus wie ein regulärer Start.</summary>
    /// <param name="workItemId">ID der Aufgabe, deren Lauf als aktiv gemeldet wurde.</param>
    private void OnCliLaufAktiv(Guid workItemId)
        => EnsureRunDataAsync(workItemId).SafeFireAndForget(_logger, "CliProcessManager.LaufdatenSicherstellenAsync");

    /// <summary>
    /// Zentraler Selbstheilungspunkt: Stellt für einen nachweislich laufenden Prozess die vollständige
    /// Beobachtungs- und Persistenzkette wieder her — Heartbeat-Timer (nur falls noch keiner läuft;
    /// atomar unter <c>_lifecycleLock</c>), RuntimeStatus-Subscription (idempotent, atomar) und
    /// Laufdaten via <see cref="WorkItemService.EnsureActiveRunAsync"/> mit dem aktuellen
    /// Live-Substatus (<see cref="CliRuntimeStatusExtensions.ToAufgabeLaufStatus"/>).
    /// Läuft kein Prozess, ist der Aufruf eine No-Op — verwaiste oder beendete Zustände werden nie
    /// wieder als aktiv geschrieben.
    /// </summary>
    /// <remarks>
    /// TOCTOU-Absicherung (Issue 342): Der <see cref="AiExecutionService.IsRunning"/>-Check wird
    /// zusätzlich als <c>laeuftGuard</c> mitgegeben und von
    /// <see cref="WorkItemService.EnsureActiveRunAsync"/> innerhalb der prozessweiten
    /// Aufgabensperre (<c>LaufGates</c>) unmittelbar vor dem Schreiben erneut ausgewertet. Endet der
    /// Prozess zwischen der Vorprüfung und der Persistenz, schreibt die Sicherstellung nichts mehr —
    /// und ein zeitgleich anstehender <see cref="WorkItemService.EndActiveRunAsync"/>-Save
    /// serialisiert unter derselben Sperre entweder davor (Guard wird false → kein Save) oder danach
    /// (Beenden hat das letzte Wort). Ein Phantom-Lauf kann so nicht wiederbelebt werden.
    /// Der Live-Substatus wird analog als Provider übergeben und von
    /// <see cref="WorkItemService.EnsureActiveRunAsync"/> ebenfalls erst innerhalb der Sperre
    /// ausgewertet — sonst könnte ein vor dem Sperreintritt gelesener, veralteter Wert einen
    /// frischeren Substatus als letzter Schreiber überschreiben (Stale-Write).
    /// </remarks>
    /// <param name="workItemId">ID der Aufgabe, deren Laufdaten sichergestellt werden sollen.</param>
    private async Task EnsureRunDataAsync(Guid workItemId)
    {
        if (!_kiService.IsRunning(workItemId))
        {
            return;
        }

        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_heartbeatTimers.ContainsKey(workItemId))
            {
                StartHeartbeatCore(workItemId);
            }
        }

        SubscribeRuntimeStatus(workItemId);

        await ExecuteWithWorkItemServiceAsync(
            workItemId,
            "Sicherstellen der Laufdaten",
            aufgabeService => aufgabeService.EnsureActiveRunAsync(
                workItemId,
                laufStatusProvider: () => _kiService.GetTerminalSession(workItemId)?.RuntimeStatus.ToAufgabeLaufStatus(),
                laeuftGuard: () => _kiService.IsRunning(workItemId))).ConfigureAwait(false);
    }

    /// <summary>Tick-Wrapper des <c>_abgleichTimer</c>: stellt sicher, dass sich bei einem
    /// über das Intervall hinaus laufenden Abgleich keine Durchläufe überlappen (fire-and-forget).</summary>
    private async Task ReconcileTickAsync()
    {
        if (Interlocked.Exchange(ref _abgleichLaeuft, 1) != 0)
        {
            return;
        }

        try
        {
            await ReconcileAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _abgleichLaeuft, 0);
        }
    }

    /// <summary>Periodischer Abgleich aller laufenden Aufgaben: iteriert
    /// <see cref="AiExecutionService.GetLaufendeAufgabenIds"/> und stellt pro Aufgabe die
    /// Laufdaten sicher (fehlerisoliert — einzelne Fehlschläge werden nur geloggt).</summary>
    private async Task ReconcileAsync()
    {
        foreach (var workItemId in _kiService.GetLaufendeAufgabenIds())
        {
            try
            {
                await EnsureRunDataAsync(workItemId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Abgleich der Laufdaten für Aufgabe {AufgabeId} fehlgeschlagen.", workItemId);
            }
        }
    }

    private async Task ExecuteWithWorkItemServiceAsync(
        Guid workItemId,
        string operation,
        Func<WorkItemService, Task> action)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<WorkItemService>();
            await action(aufgabeService).ConfigureAwait(false);
            _laufdatenChangedNotifier.NotifyLaufdatenChanged(workItemId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "ServiceScopeFactory ist bereits disposed; {Operation} wird fuer Aufgabe {AufgabeId} uebersprungen.",
                operation,
                workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fehler bei {Operation} fuer Aufgabe {AufgabeId}.",
                operation,
                workItemId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _abgleichTimer.Dispose();

        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
        _kiService.CliLaufAktiv -= OnCliLaufAktiv;

        lock (_lifecycleLock)
        {
            // Zuerst das Flag setzen: verspätete Anlagepfade (in-flight Abgleich-/Heartbeat-Ticks,
            // Event-Aufrufe) dürfen nach diesem Punkt keine Timer/Subscriptions mehr erzeugen.
            _disposed = true;

            foreach (var (workItemId, timer) in _heartbeatTimers)
            {
                timer.Dispose();
            }

            _heartbeatTimers.Clear();

            foreach (var semaphore in _updateSemaphores.Values)
            {
                semaphore.Dispose();
            }

            _updateSemaphores.Clear();

            foreach (var (session, handler) in _runtimeStatusSubscriptions.Values)
            {
                session.RuntimeStatusChanged -= handler;
            }

            _runtimeStatusSubscriptions.Clear();
        }
    }
}
