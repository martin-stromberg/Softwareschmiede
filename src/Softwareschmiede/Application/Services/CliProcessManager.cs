using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                    break;
                case CliProcessStatus.Gestoppt:
                case CliProcessStatus.Fehler:
                    StopHeartbeat(aufgabeId);
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
        await aufgabeService.AktivenLaufSetzenAsync(aufgabeId, Guid.NewGuid().ToString("N")).ConfigureAwait(false);
    }

    /// <summary>
    /// Entfernt <see cref="Domain.Entities.Aufgabe.AktiveRunId"/>, sobald der CLI-Prozess einer Aufgabe
    /// beendet wurde (regulär oder mit Fehler; siehe <see cref="AufgabeService.AktivenLaufBeendenAsync"/>).
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, deren CLI-Prozess beendet wurde.</param>
    private async Task AktivenLaufBeendenAsync(Guid aufgabeId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
        await aufgabeService.AktivenLaufBeendenAsync(aufgabeId).ConfigureAwait(false);
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
    }
}
