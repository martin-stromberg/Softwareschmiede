using Microsoft.AspNetCore.SignalR;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Status;

namespace Softwareschmiede.StatusServer.Relay;

/// <summary>SignalR-Hub, über den App-Instanzen Status-Snapshots an den Server liefern.</summary>
public sealed class StatusRelayHub : Hub
{
    /// <summary>Claim-/Item-Schlüssel, unter dem die Instanz-ID pro Verbindung abgelegt wird.</summary>
    public const string InstanceIdItemKey = "status.instanceId";

    private readonly RelayTokenValidator _tokenValidator;
    private readonly InstanceSnapshotCache _cache;
    private readonly ILogger<StatusRelayHub> _logger;

    /// <summary>Erstellt den Hub.</summary>
    /// <param name="tokenValidator">Der Token-Validator für den Verbindungsaufbau.</param>
    /// <param name="cache">Der Snapshot-Cache.</param>
    /// <param name="logger">Der Logger.</param>
    public StatusRelayHub(
        RelayTokenValidator tokenValidator,
        InstanceSnapshotCache cache,
        ILogger<StatusRelayHub> logger)
    {
        ArgumentNullException.ThrowIfNull(tokenValidator);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);

        _tokenValidator = tokenValidator;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["access_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            // Nicht-WebSocket-Transporte liefern das Token als Bearer-Header.
            var authorization = httpContext?.Request.Headers.Authorization.ToString();
            if (authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                token = authorization[7..].Trim();
        }
        if (!await _tokenValidator.IsValidAsync(token, Context.ConnectionAborted).ConfigureAwait(false))
        {
            _logger.LogWarning("Relay-Verbindung {ConnectionId} abgelehnt: ungültiges Token.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Registriert die Instanz der aktuellen Verbindung.</summary>
    /// <param name="request">Die Registrierungsanfrage inkl. Protokollversion.</param>
    /// <returns>Das Registrierungsergebnis.</returns>
    public Task<RegisterInstanceResult> RegisterInstance(RegisterInstanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.InstanceId))
            return Task.FromResult(RegisterInstanceResult.Rejected("InstanceId darf nicht leer sein."));

        if (request.ProtocolVersion != RelayProtocolVersion.Current)
            return Task.FromResult(RegisterInstanceResult.Rejected(
                $"Protokollversion {request.ProtocolVersion} wird nicht unterstützt (erwartet: {RelayProtocolVersion.Current})."));

        Context.Items[InstanceIdItemKey] = request.InstanceId;
        _cache.MarkConnected(Context.ConnectionId, request.InstanceId, request.InstanceName);
        _logger.LogInformation("Instanz {InstanceId} ({InstanceName}) über {ConnectionId} registriert.",
            request.InstanceId, request.InstanceName ?? "(ohne Namen)", Context.ConnectionId);

        return Task.FromResult(RegisterInstanceResult.Ok);
    }

    /// <summary>Empfängt einen Status-Snapshot der registrierten Instanz.</summary>
    /// <param name="snapshot">Der übermittelte Snapshot.</param>
    /// <returns>Eine abgeschlossene Aufgabe.</returns>
    public Task PushSnapshot(StatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Context.Items[InstanceIdItemKey] is not string instanceId)
            throw new HubException("Instanz ist nicht registriert.");

        _cache.UpdateSnapshot(instanceId, snapshot);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _cache.MarkDisconnected(Context.ConnectionId);
        if (exception is not null)
            _logger.LogWarning(exception, "Relay-Verbindung {ConnectionId} mit Fehler getrennt.", Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }
}
