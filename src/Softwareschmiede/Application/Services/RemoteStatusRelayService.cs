using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Hält die ausgehende Relay-Verbindung der Remote-Übersicht, orchestriert
/// Connect/Backoff/Register/Snapshot-Push und prüft den Aktivierungs-Schalter.
/// Reagiert gedrosselt auf Change-Signale (<see cref="WorkItemRunDataChangedNotifier"/>,
/// <see cref="ScmEventChangeNotifier"/>); der Dienst verwaltet einen eigenen
/// Zustandsautomaten in <see cref="RemoteStatusConnectionInfo"/>.
/// </summary>
public sealed class RemoteStatusRelayService : BackgroundService
{
    /// <summary>Mindestabstand zwischen zwei Snapshot-Pushes (Change-Drossel).</summary>
    public static readonly TimeSpan MinPushInterval = TimeSpan.FromSeconds(10);

    /// <summary>Prüfintervall im deaktivierten/fehlerhaften Zustand (Laufzeit-wirksames Aktivieren).</summary>
    public static readonly TimeSpan SettingsPollInterval = TimeSpan.FromSeconds(60);

    /// <summary>Anfänglicher Reconnect-Backoff.</summary>
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Maximaler Reconnect-Backoff.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRemoteStatusRelayClient _client;
    private readonly RemoteStatusConnectionInfo _info;
    private readonly StatusSnapshotProjector _projector;
    private readonly WorkItemRunDataChangedNotifier _runDataNotifier;
    private readonly ScmEventChangeNotifier _scmNotifier;
    private readonly ScmEventStartupGate _startupGate;
    private readonly ICredentialStore _credentialStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RemoteStatusRelayService> _logger;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);

    private int _snapshotDirty;
    private int _registrationPending;
    private DateTimeOffset _lastPushUtc = DateTimeOffset.MinValue;

    /// <summary>Erstellt den Dienst.</summary>
    /// <param name="scopeFactory">Die Scope-Factory für den DB-/Settings-Zugriff.</param>
    /// <param name="client">Der Relay-Client.</param>
    /// <param name="info">Der prozessweite Verbindungszustand.</param>
    /// <param name="projector">Der Snapshot-Projektor.</param>
    /// <param name="runDataNotifier">Der Change-Notifier der Aufgaben-Laufdaten.</param>
    /// <param name="scmNotifier">Der Change-Notifier der SCM-Ereignisse.</param>
    /// <param name="startupGate">Das Startup-Gate (wartet auf die Datenbankmigration).</param>
    /// <param name="credentialStore">Der Credential-Speicher für das Relay-Token.</param>
    /// <param name="timeProvider">Die Zeitquelle für Intervall, Drossel und Backoff.</param>
    /// <param name="logger">Der Logger.</param>
    public RemoteStatusRelayService(
        IServiceScopeFactory scopeFactory,
        IRemoteStatusRelayClient client,
        RemoteStatusConnectionInfo info,
        StatusSnapshotProjector projector,
        WorkItemRunDataChangedNotifier runDataNotifier,
        ScmEventChangeNotifier scmNotifier,
        ScmEventStartupGate startupGate,
        ICredentialStore credentialStore,
        TimeProvider timeProvider,
        ILogger<RemoteStatusRelayService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(runDataNotifier);
        ArgumentNullException.ThrowIfNull(scmNotifier);
        ArgumentNullException.ThrowIfNull(startupGate);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _client = client;
        _info = info;
        _projector = projector;
        _runDataNotifier = runDataNotifier;
        _scmNotifier = scmNotifier;
        _startupGate = startupGate;
        _credentialStore = credentialStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _runDataNotifier.RunDataChanged += OnRunDataChanged;
        _scmNotifier.Changed += OnScmChanged;
        _client.Closed += OnClientClosed;
        _client.Reconnecting += OnClientReconnecting;
        _client.Reconnected += OnClientReconnected;
        try
        {
            await _startupGate.WaitAsync(stoppingToken).ConfigureAwait(false);
            var backoff = InitialBackoff;
            while (!stoppingToken.IsCancellationRequested)
            {
                var config = await ReadConfigurationAsync(stoppingToken).ConfigureAwait(false);
                if (config.Error is { } configError)
                {
                    // Disabled bewusst ohne Detailtext setzen — der Zustandstext allein
                    // („deaktiviert“) trägt die Information, ein Fehlertext wäre redundant.
                    // Erst trennen (das Closed-Ereignis würde den Status sonst mit
                    // „Reconnecting“ überschreiben), dann den Zielzustand melden.
                    await DisconnectQuietlyAsync(stoppingToken).ConfigureAwait(false);
                    _info.SetState(config.Enabled
                        ? RemoteStatusConnectionState.ConfigurationError
                        : RemoteStatusConnectionState.Disabled,
                        config.Enabled ? configError : null);
                    // Der Warte-Schlaf darf nicht durch Change-Signale geweckt werden —
                    // sonst wird die Poll-/Backoff-Kadenz ereignisgesteuert.
                    await Task.Delay(SettingsPollInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (!_client.IsConnected)
                {
                    var connected = await TryConnectAndRegisterAsync(config, stoppingToken).ConfigureAwait(false);
                    if (!connected)
                    {
                        await Task.Delay(backoff, _timeProvider, stoppingToken).ConfigureAwait(false);
                        backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                        continue;
                    }

                    backoff = InitialBackoff;
                    await TryPushSnapshotAsync(force: true, stoppingToken).ConfigureAwait(false);
                }

                await PumpWhileConnectedAsync(config, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // Unerwartete Fehler (z. B. beim Lesen der Konfiguration) dürfen den Dienst
            // nicht still als „deaktiviert“ enden lassen — der Fehler bleibt sichtbar.
            _logger.LogError(ex, "Remote-Übersicht: Unerwarteter Fehler im Relay-Dienst.");
            _info.SetState(RemoteStatusConnectionState.Error, ex.Message);
        }
        finally
        {
            _runDataNotifier.RunDataChanged -= OnRunDataChanged;
            _scmNotifier.Changed -= OnScmChanged;
            _client.Closed -= OnClientClosed;
            _client.Reconnecting -= OnClientReconnecting;
            _client.Reconnected -= OnClientReconnected;
            // Nur beim regulären Stopp auf „deaktiviert“ zurücksetzen — ein zuvor
            // gemeldeter Fehlerzustand soll sichtbar bleiben.
            if (stoppingToken.IsCancellationRequested)
                _info.SetState(RemoteStatusConnectionState.Disabled);
        }
    }

    /// <summary>Liest die wirksame Remote-Status-Konfiguration (Einstellungen + Token).</summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die gelesene Konfiguration inkl. Fehlertext statt Ausnahme.</returns>
    internal async Task<RelayConfiguration> ReadConfigurationAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var appSettings = scope.ServiceProvider.GetRequiredService<AppSettingService>();

        var enabled = await appSettings.GetRemoteStatusEnabledAsync(ct).ConfigureAwait(false);
        if (!enabled)
            return new RelayConfiguration(false, null, null, null, 0, null, "Remote-Übersicht ist deaktiviert.");

        var urlValue = await appSettings.GetRemoteStatusUrlAsync(ct).ConfigureAwait(false);
        if (!RemoteStatusUrlParser.TryCreateHubUrl(urlValue, out var hubUrl, out var urlError))
            return new RelayConfiguration(true, null, null, null, 0, null, urlError);

        var token = _credentialStore.GetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget);
        if (string.IsNullOrEmpty(token))
            return new RelayConfiguration(true, null, null, null, 0, null,
                "Das Relay-Token fehlt (Einstellungen → Remote-Übersicht).");

        var instanceId = await appSettings.GetOrCreateRemoteStatusInstanceIdAsync(ct).ConfigureAwait(false);
        var instanceName = await appSettings.GetRemoteStatusInstanceNameAsync(ct).ConfigureAwait(false);
        var intervalSeconds = await appSettings.GetRemoteStatusSnapshotIntervalSecondsAsync(ct).ConfigureAwait(false);

        return new RelayConfiguration(true, hubUrl, token, instanceId, intervalSeconds, instanceName, null);
    }

    private async Task<bool> TryConnectAndRegisterAsync(RelayConfiguration config, CancellationToken ct)
    {
        _info.SetState(RemoteStatusConnectionState.Connecting);
        try
        {
            await _client.ConnectAsync(config.HubUrl!, config.Token!, ct).ConfigureAwait(false);
            var result = await _client.RegisterInstanceAsync(
                new RegisterInstanceRequest(config.InstanceId!, config.InstanceName, RelayProtocolVersion.Current),
                ct).ConfigureAwait(false);
            if (!result.Accepted)
            {
                _logger.LogWarning("Remote-Übersicht: Registrierung abgelehnt: {Reason}", result.RejectionReason);
                // Die Verbindung trennen, damit die Pump-Schleife keine unregistrierten
                // Snapshots pumpt; die äußere Schleife versucht es mit Backoff erneut.
                await DisconnectQuietlyAsync(ct).ConfigureAwait(false);
                _info.SetState(RemoteStatusConnectionState.Error,
                    result.RejectionReason ?? "Registrierung abgelehnt.");
                return false;
            }

            Interlocked.Exchange(ref _registrationPending, 0);
            _info.SetState(RemoteStatusConnectionState.Connected);
            _logger.LogInformation("Remote-Übersicht: Instanz {InstanceId} verbunden und registriert.", config.InstanceId);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // War ConnectAsync erfolgreich und scheitert erst die Registrierung, bliebe
            // die Verbindung offen — die Pump-Schleife würde unregistriert pushen.
            await DisconnectQuietlyAsync(ct).ConfigureAwait(false);
            _info.SetState(RemoteStatusConnectionState.Reconnecting, ex.Message);
            _logger.LogWarning(ex, "Remote-Übersicht: Verbindungsaufbau fehlgeschlagen, neuer Versuch folgt.");
            return false;
        }
    }

    private async Task PumpWhileConnectedAsync(
        RelayConfiguration config, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(config.SnapshotIntervalSeconds);
        while (!ct.IsCancellationRequested && _client.IsConnected)
        {
            // Nach einem SignalR-Auto-Reconnect ist die (neue) Verbindung serverseitig
            // nicht registriert — die Registrierung muss vor dem nächsten Push nachgeholt
            // werden, sonst scheitert jeder Push mit „Instanz ist nicht registriert.“.
            if (Volatile.Read(ref _registrationPending) == 1)
            {
                // Vor dem Versuch zurücksetzen: Re-Register ist idempotent, ein zwischen
                // Versuch und Rücksetzen eintreffendes Reconnected ginge sonst verloren.
                Interlocked.Exchange(ref _registrationPending, 0);
                await TryReregisterAsync(config, ct).ConfigureAwait(false);
                if (!_client.IsConnected)
                    break;
            }

            var now = _timeProvider.GetUtcNow();
            var sinceLast = now - _lastPushUtc;
            var dirty = Volatile.Read(ref _snapshotDirty) == 1;
            if (sinceLast >= interval || (dirty && sinceLast >= MinPushInterval))
            {
                // Das Dirty-Flag erst beim tatsächlichen Push-Versuch zurücksetzen —
                // andernfalls ginge ein Change-Signal <MinPushInterval verloren.
                Interlocked.Exchange(ref _snapshotDirty, 0);
                await TryPushSnapshotAsync(force: false, ct).ConfigureAwait(false);
            }

            var untilNext = interval - (_timeProvider.GetUtcNow() - _lastPushUtc);
            var wait = untilNext < MinPushInterval ? untilNext : MinPushInterval;
            if (wait < TimeSpan.Zero)
                wait = TimeSpan.Zero;
            await WaitOrWakeAsync(wait, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Registriert die Instanz nach einem Auto-Reconnect erneut am Hub.</summary>
    /// <param name="config">Die wirksame Konfiguration.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn die Registrierung akzeptiert wurde.</returns>
    private async Task<bool> TryReregisterAsync(RelayConfiguration config, CancellationToken ct)
    {
        try
        {
            var result = await _client.RegisterInstanceAsync(
                new RegisterInstanceRequest(config.InstanceId!, config.InstanceName, RelayProtocolVersion.Current),
                ct).ConfigureAwait(false);
            if (result.Accepted)
            {
                _info.SetState(RemoteStatusConnectionState.Connected);
                _logger.LogInformation(
                    "Remote-Übersicht: Instanz {InstanceId} nach Reconnect erneut registriert.",
                    config.InstanceId);
                return true;
            }

            _logger.LogWarning(
                "Remote-Übersicht: Erneute Registrierung abgelehnt: {Reason}", result.RejectionReason);
            await DisconnectQuietlyAsync(ct).ConfigureAwait(false);
            _info.SetState(RemoteStatusConnectionState.Error,
                result.RejectionReason ?? "Erneute Registrierung abgelehnt.");
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _info.ReportError(ex.Message);
            _logger.LogWarning(ex, "Remote-Übersicht: Erneute Registrierung nach Reconnect fehlgeschlagen.");
            return false;
        }
    }

    private async Task TryPushSnapshotAsync(bool force, CancellationToken ct)
    {
        if (!force && !_client.IsConnected)
            return;

        // Versuchszeitpunkt merken, damit ein wiederkehrender Fehler nicht in eine
        // enge Retry-Schleife läuft — der nächste Versuch folgt frühestens nach dem Intervall.
        _lastPushUtc = _timeProvider.GetUtcNow();
        try
        {
            var snapshot = await _projector.CreateAsync(ct).ConfigureAwait(false);
            await _client.PushSnapshotAsync(snapshot, ct).ConfigureAwait(false);
            _info.ReportSnapshotPushed(_lastPushUtc);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _info.ReportError(ex.Message);
            _logger.LogWarning(ex, "Remote-Übersicht: Snapshot-Push fehlgeschlagen.");
        }
    }

    private async Task DisconnectQuietlyAsync(CancellationToken ct)
    {
        if (!_client.IsConnected)
            return;

        try
        {
            await _client.DisconnectAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Remote-Übersicht: Fehler beim Trennen der Relay-Verbindung.");
        }
    }

    /// <summary>Wartet bis zum Ablauf von <paramref name="delay"/> oder bis ein Change-Signal
    /// (<see cref="MarkDirty"/>) oder der Abbruch den Schlaf beendet.</summary>
    /// <param name="delay">Die maximale Wartezeit.</param>
    /// <param name="ct">Abbruchtoken.</param>
    private async Task WaitOrWakeAsync(TimeSpan delay, CancellationToken ct)
    {
        if (delay <= TimeSpan.Zero)
            return;

        var wakeTask = _wakeSignal.WaitAsync(ct);
        var delayTask = Task.Delay(delay, _timeProvider, ct);
        var completed = await Task.WhenAny(wakeTask, delayTask).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
    }

    private void OnRunDataChanged(Guid _) => MarkDirty();
    private void OnScmChanged() => MarkDirty();

    private void MarkDirty()
    {
        Interlocked.Exchange(ref _snapshotDirty, 1);
        SignalWake();
    }

    /// <summary>Weckt die Pump-Schleife ohne zu blockieren; ein bereits gesetztes Signal wird toleriert.</summary>
    private void SignalWake()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Bereits signalisiert — gleichzeitige Change-Signale zweier Threads sind unkritisch.
        }
    }

    private Task OnClientClosed(Exception? error)
    {
        _info.SetState(RemoteStatusConnectionState.Reconnecting,
            error?.Message ?? "Verbindung geschlossen.");
        return Task.CompletedTask;
    }

    private Task OnClientReconnecting(Exception? error)
    {
        _info.SetState(RemoteStatusConnectionState.Reconnecting, error?.Message);
        return Task.CompletedTask;
    }

    private Task OnClientReconnected(string? _)
    {
        // Der Auto-Reconnect stellt serverseitig eine NEUE Verbindung her, die noch nicht
        // registriert ist: Die Pump-Schleife holt RegisterInstance vor dem nächsten Push
        // nach (_registrationPending); das Dirty-Flag sorgt für den sofortigen Folge-Push.
        Interlocked.Exchange(ref _registrationPending, 1);
        Interlocked.Exchange(ref _snapshotDirty, 1);
        _info.SetState(RemoteStatusConnectionState.Connected);
        SignalWake();
        return Task.CompletedTask;
    }

    /// <summary>Wirksame Remote-Status-Konfiguration oder Fehlertext.</summary>
    /// <param name="Enabled">Ob die Remote-Übersicht aktiviert ist.</param>
    /// <param name="HubUrl">Die normalisierte Hub-URL.</param>
    /// <param name="Token">Das Relay-Zugangstoken.</param>
    /// <param name="InstanceId">Die persistente Instanz-ID.</param>
    /// <param name="SnapshotIntervalSeconds">Das Push-Intervall in Sekunden.</param>
    /// <param name="InstanceName">Der optionale Anzeigename.</param>
    /// <param name="Error">Der Konfigurationsfehler, sofern vorhanden.</param>
    public sealed record RelayConfiguration(
        bool Enabled,
        Uri? HubUrl,
        string? Token,
        string? InstanceId,
        int SnapshotIntervalSeconds,
        string? InstanceName,
        string? Error);
}
