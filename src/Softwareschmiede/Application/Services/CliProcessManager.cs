using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Verwaltung von CLI-Prozessen: Start, Stop, Heartbeat-Tracking.
/// Koordiniert <see cref="KiAusfuehrungsService"/> und <see cref="AufgabeService"/> für Heartbeat-Updates.
/// </summary>
public sealed class CliProcessManager : IDisposable
{
    private readonly KiAusfuehrungsService _kiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CliProcessManager> _logger;

    private readonly ConcurrentDictionary<Guid, Timer> _heartbeatTimers = new();
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    // Hält pro Aufgabe die PseudoConsoleSession und den dafür registrierten Event-Handler, damit
    // RuntimeStatusChanged beim Stopp/Fehler wieder sauber abgemeldet werden kann (siehe
    // UnsubscribeRuntimeStatus). Ohne dieses Tracking müsste GetPseudoConsoleSession(aufgabeId) erneut
    // abgefragt werden — das Handle ist zu diesem Zeitpunkt aber bereits aus KiAusfuehrungsService._handles
    // entfernt (siehe HandleProcessExited), sodass die Session dort nicht mehr auffindbar wäre.
    private readonly ConcurrentDictionary<Guid, (PseudoConsoleSession Session, EventHandler<CliRuntimeStatusChangedEventArgs> Handler)> _runtimeStatusSubscriptions = new();

    // Ein Semaphore pro Aufgabe statt eines einzelnen klassenweiten Semaphores: verhindert nur das
    // Überlappen von Timer-Ticks derselben Aufgabe, ohne die Heartbeat-Updates unabhängiger Aufgaben
    // gegeneinander zu serialisieren. Wird in StartHeartbeat angelegt und in StopHeartbeat entfernt.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _updateSemaphores = new();

    /// <summary>Erstellt eine neue Instanz des <see cref="CliProcessManager"/>.</summary>
    public CliProcessManager(
        KiAusfuehrungsService kiService,
        IServiceScopeFactory scopeFactory,
        ILogger<CliProcessManager> logger)
    {
        _kiService = kiService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
    }

    /// <summary>Startet den Heartbeat-Timer für eine Aufgabe.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    public void StartHeartbeat(Guid aufgabeId)
    {
        StopHeartbeat(aufgabeId);

        _updateSemaphores[aufgabeId] = new SemaphoreSlim(1, 1);

        var timer = new Timer(
            _ => AktualisierungAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktualisierungAsync"),
            null,
            HeartbeatInterval,
            HeartbeatInterval);

        _heartbeatTimers[aufgabeId] = timer;
        _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestartet.", aufgabeId);
    }

    /// <summary>Stoppt den Heartbeat-Timer für eine Aufgabe.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    public void StopHeartbeat(Guid aufgabeId)
    {
        if (_heartbeatTimers.TryRemove(aufgabeId, out var timer))
        {
            timer.Dispose();
            _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestoppt.", aufgabeId);
        }

        if (_updateSemaphores.TryRemove(aufgabeId, out var semaphore))
        {
            semaphore.Dispose();
        }
    }

    private async Task AktualisierungAsync(Guid aufgabeId)
    {
        if (!_updateSemaphores.TryGetValue(aufgabeId, out var semaphore))
        {
            // Heartbeat wurde zwischenzeitlich gestoppt (Semaphore bereits entfernt).
            return;
        }

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_kiService.IsRunning(aufgabeId))
            {
                StopHeartbeat(aufgabeId);
                return;
            }

            _kiService.UpdateHeartbeat(aufgabeId);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            await aufgabeService.UpdateHeartbeatAsync(aufgabeId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat-Aktualisierung für Aufgabe {AufgabeId} fehlgeschlagen.", aufgabeId);
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

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        try
        {
            switch (status)
            {
                case CliProcessStatus.Gestartet:
                    StartHeartbeat(aufgabeId);
                    // AktiveRunId muss sofort persistiert werden (nicht erst beim ersten periodischen
                    // Heartbeat nach 30s) — sonst zeigt die Seitenleisten-Kachel (KiAusfuehrungsStatusConverter,
                    // Issue 108) für bis zu 30s nach dem Start weiterhin "✓ Bereit" statt "▶ Läuft" an.
                    AktivenLaufSetzenAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktivenLaufSetzenAsync");
                    SubscribeRuntimeStatus(aufgabeId);
                    break;
                case CliProcessStatus.Gestoppt:
                case CliProcessStatus.Fehler:
                    StopHeartbeat(aufgabeId);
                    UnsubscribeRuntimeStatus(aufgabeId);
                    AktivenLaufBeendenAsync(aufgabeId).SafeFireAndForget(_logger, "CliProcessManager.AktivenLaufBeendenAsync");
                    break;
            }
        }
        catch (Exception ex)
        {
            // Ein Fehler beim Start/Stop des Heartbeat-Timers darf die Multicast-Invoke-Kette von
            // KiAusfuehrungsService.CliProcessStatusChanged nicht abbrechen, sonst würde der zweite
            // Abonnent (TaskDetailViewModel.OnCliProcessStatusChanged) nicht mehr benachrichtigt.
            _logger.LogError(ex, "Fehler bei der Verarbeitung des CLI-Prozess-Status {Status} für Aufgabe {AufgabeId}.", status, aufgabeId);
        }
    }

    /// <summary>
    /// Setzt <see cref="Domain.Entities.Aufgabe.AktiveRunId"/> und aktualisiert den Heartbeat sofort,
    /// sobald ein CLI-Prozess gestartet wurde (siehe <see cref="AufgabeService.AktivenLaufSetzenAsync"/>).
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Prozess gestartet wurde.</param>
    private async Task AktivenLaufSetzenAsync(Guid aufgabeId)
    {
        await ExecuteWithAufgabeServiceAsync(
            aufgabeId,
            "Setzen des aktiven Laufs",
            aufgabeService => aufgabeService.AktivenLaufSetzenAsync(aufgabeId, Guid.NewGuid().ToString("N"))).ConfigureAwait(false);
    }

    /// <summary>
    /// Entfernt <see cref="Domain.Entities.Aufgabe.AktiveRunId"/>, sobald der CLI-Prozess einer Aufgabe
    /// beendet wurde (regulär oder mit Fehler; siehe <see cref="AufgabeService.AktivenLaufBeendenAsync"/>).
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Prozess beendet wurde.</param>
    private async Task AktivenLaufBeendenAsync(Guid aufgabeId)
    {
        await ExecuteWithAufgabeServiceAsync(
            aufgabeId,
            "Beenden des aktiven Laufs",
            aufgabeService => aufgabeService.AktivenLaufBeendenAsync(aufgabeId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Abonniert <see cref="PseudoConsoleSession.RuntimeStatusChanged"/> für die ConPTY-Sitzung einer
    /// gerade gestarteten Aufgabe (sofern vorhanden — beim klassischen, nicht-ConPTY-Start existiert keine
    /// Sitzung), damit Wechsel zwischen "arbeitet" und "wartet auf Eingabe" über
    /// <see cref="AufgabeService.AktualisiereLaufStatusAsync"/> persistiert werden (Issue 108, Folgefehler
    /// des Rückwegs Läuft → Wartet: dieser Substatus wurde vorher ausschließlich lokal in der
    /// <see cref="PseudoConsoleSession"/> gehalten und nie an die Datenbank weitergereicht).
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Prozess gestartet wurde.</param>
    private void SubscribeRuntimeStatus(Guid aufgabeId)
    {
        // Defensive Bereinigung einer eventuell noch vorhandenen alten Registrierung (z. B. wenn ein
        // Gestoppt-Event für einen vorherigen Lauf derselben Aufgabe ausblieb) — verhindert doppelte
        // Event-Registrierungen und einen dadurch überzähligen Persistierungsaufruf pro Statuswechsel.
        UnsubscribeRuntimeStatus(aufgabeId);

        var session = _kiService.GetPseudoConsoleSession(aufgabeId);
        if (session is null)
        {
            // Klassischer Start ohne ConPTY: keine Sitzung, also kein Laufzeit-Substatus verfügbar.
            // KiAusfuehrungsStatusConverter fällt in diesem Fall auf das bisherige Verhalten zurück
            // (LaufStatus bleibt null → "▶ Läuft", solange AktiveRunId gesetzt und Heartbeat aktuell ist).
            return;
        }

        EventHandler<CliRuntimeStatusChangedEventArgs> handler = (_, e) =>
            OnRuntimeStatusChanged(aufgabeId, e.Status);

        session.RuntimeStatusChanged += handler;
        _runtimeStatusSubscriptions[aufgabeId] = (session, handler);
    }

    /// <summary>Meldet die Event-Registrierung aus <see cref="SubscribeRuntimeStatus"/> wieder ab.</summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Prozess beendet wurde.</param>
    private void UnsubscribeRuntimeStatus(Guid aufgabeId)
    {
        if (_runtimeStatusSubscriptions.TryRemove(aufgabeId, out var subscription))
        {
            subscription.Session.RuntimeStatusChanged -= subscription.Handler;
        }
    }

    /// <summary>
    /// Übersetzt einen <see cref="CliRuntimeStatus"/>-Wechsel der <see cref="PseudoConsoleSession"/> in den
    /// Domain-Substatus <see cref="AufgabeLaufStatus"/> und persistiert ihn. <see cref="CliRuntimeStatus.Inaktiv"/>
    /// wird ignoriert: Das Beenden des Laufs (inkl. Zurücksetzen von <see cref="Domain.Entities.Aufgabe.LaufStatus"/>)
    /// übernimmt bereits <see cref="AktivenLaufBeendenAsync"/> über das separate <see cref="CliProcessStatus.Gestoppt"/>-
    /// bzw. <see cref="CliProcessStatus.Fehler"/>-Ereignis.
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren Laufzeit-Substatus sich geändert hat.</param>
    /// <param name="status">Der neue Laufzeitstatus der CLI-Sitzung.</param>
    private void OnRuntimeStatusChanged(Guid aufgabeId, CliRuntimeStatus status)
    {
        if (status == CliRuntimeStatus.Inaktiv)
            return;

        var laufStatus = status == CliRuntimeStatus.WartetAufEingabe
            ? AufgabeLaufStatus.WartetAufEingabe
            : AufgabeLaufStatus.Laeuft;

        AktualisiereLaufStatusAsync(aufgabeId, laufStatus).SafeFireAndForget(_logger, "CliProcessManager.AktualisiereLaufStatusAsync");
    }

    /// <summary>Persistiert einen geänderten Laufzeit-Substatus über <see cref="AufgabeService.AktualisiereLaufStatusAsync"/>.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <param name="laufStatus">Neuer Laufzeit-Substatus.</param>
    private async Task AktualisiereLaufStatusAsync(Guid aufgabeId, AufgabeLaufStatus laufStatus)
    {
        await ExecuteWithAufgabeServiceAsync(
            aufgabeId,
            "Aktualisieren des Laufstatus",
            aufgabeService => aufgabeService.AktualisiereLaufStatusAsync(aufgabeId, laufStatus)).ConfigureAwait(false);
    }

    private async Task ExecuteWithAufgabeServiceAsync(
        Guid aufgabeId,
        string operation,
        Func<AufgabeService, Task> action)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            await action(aufgabeService).ConfigureAwait(false);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "ServiceScopeFactory ist bereits disposed; {Operation} wird fuer Aufgabe {AufgabeId} uebersprungen.",
                operation,
                aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fehler bei {Operation} fuer Aufgabe {AufgabeId}.",
                operation,
                aufgabeId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;

        foreach (var (aufgabeId, timer) in _heartbeatTimers)
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
