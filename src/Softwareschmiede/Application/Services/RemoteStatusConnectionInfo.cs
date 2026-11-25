namespace Softwareschmiede.Application.Services;

/// <summary>Zustandswerte der Remote-Übersichts-Verbindung.</summary>
public enum RemoteStatusConnectionState
{
    /// <summary>Remote-Übersicht ist deaktiviert.</summary>
    Disabled,

    /// <summary>Aktiviert, aber Konfiguration unvollständig/ungültig (URL oder Token fehlt).</summary>
    ConfigurationError,

    /// <summary>Verbindung wird aufgebaut.</summary>
    Connecting,

    /// <summary>Verbindung steht, Snapshots werden geliefert.</summary>
    Connected,

    /// <summary>Verbindung unterbrochen, Wiederverbindung läuft.</summary>
    Reconnecting,

    /// <summary>Fehlerzustand (z. B. Token abgelehnt, Protokollversion inkompatibel).</summary>
    Error,
}

/// <summary>
/// Prozessweiter, thread-sicherer Verbindungszustand der Remote-Übersicht für UI und Diagnose.
/// </summary>
public sealed class RemoteStatusConnectionInfo
{
    private readonly object _sync = new();
    private RemoteStatusConnectionState _state = RemoteStatusConnectionState.Disabled;
    private string? _statusMessage;
    private string? _lastError;
    private DateTimeOffset? _lastSnapshotUtc;

    /// <summary>Wird ausgelöst, wenn sich Zustand, Meldung oder Snapshot-Zeitpunkt ändert.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Aktueller Verbindungszustand.</summary>
    public RemoteStatusConnectionState State
    {
        get { lock (_sync) return _state; }
    }

    /// <summary>Verständliche Detailmeldung zum Zustand (z. B. Konfigurationsfehler-Text).</summary>
    public string? StatusMessage
    {
        get { lock (_sync) return _statusMessage; }
    }

    /// <summary>Letzte Fehlermeldung.</summary>
    public string? LastError
    {
        get { lock (_sync) return _lastError; }
    }

    /// <summary>UTC-Zeitpunkt des letzten erfolgreich gepushten Snapshots.</summary>
    public DateTimeOffset? LastSnapshotUtc
    {
        get { lock (_sync) return _lastSnapshotUtc; }
    }

    /// <summary>Setzt Zustand und optionale Detailmeldung; löst <see cref="StateChanged"/> bei Änderung aus.</summary>
    /// <param name="state">Der neue Zustand.</param>
    /// <param name="statusMessage">Optionale verständliche Detailmeldung.</param>
    public void SetState(RemoteStatusConnectionState state, string? statusMessage = null)
    {
        bool changed;
        lock (_sync)
        {
            changed = _state != state || _statusMessage != statusMessage;
            _state = state;
            _statusMessage = statusMessage;
            if (state != RemoteStatusConnectionState.Error
                && state != RemoteStatusConnectionState.ConfigurationError)
            {
                _lastError = null;
            }
            else if (statusMessage is not null)
            {
                _lastError = statusMessage;
            }
        }

        if (changed)
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Vermerkt einen erfolgreichen Snapshot-Push.</summary>
    /// <param name="utc">Der Push-Zeitpunkt (UTC).</param>
    public void ReportSnapshotPushed(DateTimeOffset utc)
    {
        lock (_sync)
            _lastSnapshotUtc = utc;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Vermerkt einen Fehler ohne Zustandswechsel.</summary>
    /// <param name="message">Die Fehlermeldung.</param>
    public void ReportError(string message)
    {
        lock (_sync)
            _lastError = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Baut eine verständliche Statuszeile für die Anzeige in den Einstellungen.</summary>
    /// <returns>Deutscher Statustext inkl. Detailmeldung und letztem Snapshot-Zeitpunkt.</returns>
    public string BuildStatusText()
    {
        string stateText;
        string? message;
        DateTimeOffset? lastSnapshot;
        string? lastError;
        lock (_sync)
        {
            stateText = _state switch
            {
                RemoteStatusConnectionState.Disabled => "deaktiviert",
                RemoteStatusConnectionState.ConfigurationError => "Konfigurationsfehler",
                RemoteStatusConnectionState.Connecting => "verbinde…",
                RemoteStatusConnectionState.Connected => "verbunden",
                RemoteStatusConnectionState.Reconnecting => "verbinde erneut…",
                RemoteStatusConnectionState.Error => "Fehler",
                _ => _state.ToString(),
            };
            message = _statusMessage;
            lastSnapshot = _lastSnapshotUtc;
            lastError = _lastError;
        }

        var text = stateText;
        if (!string.IsNullOrEmpty(message))
            text += $": {message}";
        else if (!string.IsNullOrEmpty(lastError))
            text += $": {lastError}";
        if (lastSnapshot is { } snapshot)
            text += $" · letzter Snapshot {snapshot:HH:mm:ss} UTC";
        return text;
    }
}
