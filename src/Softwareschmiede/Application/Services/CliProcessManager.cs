using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Verwaltung von CLI-Prozessen: Start, Stop, Heartbeat-Tracking.
/// Koordiniert <see cref="KiAusfuehrungsService"/> und <see cref="AufgabeService"/> für Heartbeat-Updates.
/// </summary>
public sealed class CliProcessManager : IDisposable
{
    private readonly KiAusfuehrungsService _kiService;
    private readonly AufgabeService _aufgabeService;
    private readonly ILogger<CliProcessManager> _logger;

    private readonly ConcurrentDictionary<Guid, Timer> _heartbeatTimers = new();
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>Erstellt eine neue Instanz des <see cref="CliProcessManager"/>.</summary>
    public CliProcessManager(
        KiAusfuehrungsService kiService,
        AufgabeService aufgabeService,
        ILogger<CliProcessManager> logger)
    {
        _kiService = kiService;
        _aufgabeService = aufgabeService;
        _logger = logger;

        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
    }

    /// <summary>Startet den Heartbeat-Timer für eine Aufgabe.</summary>
    public void StartHeartbeat(Guid aufgabeId)
    {
        StopHeartbeat(aufgabeId);

        var timer = new Timer(
            _ => AktualisierungDurchfuehren(aufgabeId),
            null,
            HeartbeatInterval,
            HeartbeatInterval);

        _heartbeatTimers[aufgabeId] = timer;
        _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestartet.", aufgabeId);
    }

    /// <summary>Stoppt den Heartbeat-Timer für eine Aufgabe.</summary>
    public void StopHeartbeat(Guid aufgabeId)
    {
        if (_heartbeatTimers.TryRemove(aufgabeId, out var timer))
        {
            timer.Dispose();
            _logger.LogDebug("Heartbeat-Timer für Aufgabe {AufgabeId} gestoppt.", aufgabeId);
        }
    }

    private void AktualisierungDurchfuehren(Guid aufgabeId)
    {
        _ = AktualisierungAsync(aufgabeId);
    }

    private async Task AktualisierungAsync(Guid aufgabeId)
    {
        try
        {
            if (!_kiService.IsCliRunning(aufgabeId))
            {
                StopHeartbeat(aufgabeId);
                return;
            }

            _kiService.UpdateHeartbeat(aufgabeId);
            await _aufgabeService.UpdateHeartbeatAsync(aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat-Aktualisierung für Aufgabe {AufgabeId} fehlgeschlagen.", aufgabeId);
        }
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        switch (status)
        {
            case CliProcessStatus.Gestartet:
                StartHeartbeat(aufgabeId);
                break;
            case CliProcessStatus.Gestoppt:
            case CliProcessStatus.Fehler:
                StopHeartbeat(aufgabeId);
                break;
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
    }
}
