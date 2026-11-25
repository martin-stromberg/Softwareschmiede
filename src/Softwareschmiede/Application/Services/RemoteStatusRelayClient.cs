using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.Application.Services;

/// <summary>Austauschbare Abstraktion des Relay-Clients für den <see cref="RemoteStatusRelayService"/>.</summary>
public interface IRemoteStatusRelayClient : IAsyncDisposable
{
    /// <summary><see langword="true"/>, solange die Verbindung besteht.</summary>
    bool IsConnected { get; }

    /// <summary>Die Verbindung wurde geschlossen (inkl. erschöpfter Auto-Reconnects).</summary>
    event Func<Exception?, Task>? Closed;

    /// <summary>Die Verbindung wurde unterbrochen, Wiederverbindung läuft.</summary>
    event Func<Exception?, Task>? Reconnecting;

    /// <summary>Die Verbindung wurde wiederhergestellt.</summary>
    event Func<string?, Task>? Reconnected;

    /// <summary>Baut die Verbindung zum Relay-Hub auf.</summary>
    /// <param name="hubUrl">Die Hub-URL (inkl. Pfad <c>/relay</c>).</param>
    /// <param name="token">Das Relay-Zugangstoken.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    Task ConnectAsync(Uri hubUrl, string token, CancellationToken cancellationToken);

    /// <summary>Registriert die Instanz am Hub.</summary>
    /// <param name="request">Die Registrierungsanfrage.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Das Registrierungsergebnis.</returns>
    Task<RegisterInstanceResult> RegisterInstanceAsync(
        RegisterInstanceRequest request, CancellationToken cancellationToken);

    /// <summary>Überträgt einen Status-Snapshot an den Server.</summary>
    /// <param name="snapshot">Der zu sendende Snapshot.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    Task PushSnapshotAsync(StatusSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>Trennt die Verbindung kontrolliert.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    Task DisconnectAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Produktiver <see cref="IRemoteStatusRelayClient"/> als dünner Wrapper um
/// <see cref="HubConnection"/> (<see cref="HttpConnectionOptions.AccessTokenProvider"/>,
/// <see cref="HubConnectionBuilderExtensions.WithAutomaticReconnect(IHubConnectionBuilder)"/>).
/// Über <see cref="ConfigureOptions"/> lässt sich die Verbindung in Tests auf einen
/// in-memory <c>TestServer</c> lenken (Message-Handler-/WebSocket-Factory oder Transportwahl).
/// </summary>
public sealed class RemoteStatusRelayClient : IRemoteStatusRelayClient
{
    private readonly ILogger<RemoteStatusRelayClient> _logger;
    private HubConnection? _connection;

    /// <summary>Erstellt den Client.</summary>
    /// <param name="logger">Der Logger.</param>
    public RemoteStatusRelayClient(ILogger<RemoteStatusRelayClient> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Test-Hook: wird beim Verbindungsaufbau auf die <see cref="HttpConnectionOptions"/> angewendet
    /// (z. B. <c>HttpMessageHandlerFactory</c>, <c>WebSocketFactory</c> oder <c>Transports</c>).</summary>
    public Action<HttpConnectionOptions>? ConfigureOptions { get; set; }

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public event Func<Exception?, Task>? Closed;

    /// <inheritdoc/>
    public event Func<Exception?, Task>? Reconnecting;

    /// <inheritdoc/>
    public event Func<string?, Task>? Reconnected;

    /// <inheritdoc/>
    public async Task ConnectAsync(Uri hubUrl, string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hubUrl);
        ArgumentException.ThrowIfNullOrEmpty(token);

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        var configureOptions = ConfigureOptions;
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                configureOptions?.Invoke(options);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.Closed += error => Closed?.Invoke(error) ?? Task.CompletedTask;
        connection.Reconnecting += error => Reconnecting?.Invoke(error) ?? Task.CompletedTask;
        connection.Reconnected += connectionId => Reconnected?.Invoke(connectionId) ?? Task.CompletedTask;

        _connection = connection;
        _logger.LogInformation("Remote-Übersicht: Verbindungsaufbau zu {HubUrl}.", hubUrl);
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<RegisterInstanceResult> RegisterInstanceAsync(
        RegisterInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var connection = _connection
            ?? throw new InvalidOperationException("Der Relay-Client ist nicht verbunden.");
        return connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance", request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PushSnapshotAsync(StatusSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var connection = _connection
            ?? throw new InvalidOperationException("Der Relay-Client ist nicht verbunden.");
        return connection.InvokeAsync("PushSnapshot", snapshot, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is { } connection)
            await connection.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_connection is { } connection)
        {
            _connection = null;
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
