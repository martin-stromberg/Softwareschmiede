using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Relay;

/// <summary>End-to-end-Roundtrip: echte <see cref="HubConnection"/> gegen den in-memory
/// <c>TestServer</c> der <see cref="StatusWebApplicationFactory"/> (Long-Polling-Transport)
/// mit datenbankbasierten Relay-Tokens und Cookie-Anmeldung für die Status-Endpunkte.</summary>
public sealed class StatusRelayRoundtripTests : IClassFixture<StatusWebApplicationFactory>, IAsyncDisposable
{
    private readonly StatusWebApplicationFactory _factory;
    private readonly List<HubConnection> _connections = [];
    private AuthTestClient? _web;

    /// <summary>Erstellt die Testklasse.</summary>
    /// <param name="factory">Die Server-Factory.</param>
    public StatusRelayRoundtripTests(StatusWebApplicationFactory factory) => _factory = factory;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
            await connection.DisposeAsync();
        _web?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<string> SeedTokenAsync()
    {
        var created = await new StatusServerDbSeeder(_factory)
            .SeedRelayTokenAsync($"inst-{Guid.NewGuid():N}");
        return created.Plaintext;
    }

    private async Task<AuthTestClient> GetWebClientAsync()
    {
        if (_web is not null)
            return _web;

        var seeder = new StatusServerDbSeeder(_factory);
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
        _web = AuthTestClient.Create(_factory);
        await _web.LoginAsync("admin", "Geheim-123", TestContext.Current.CancellationToken);
        return _web;
    }

    private HubConnection CreateConnection(string? token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "relay"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        _connections.Add(connection);
        return connection;
    }

    /// <summary>Register + PushSnapshot: die Instanz erscheint verbunden mit Snapshot auf der Seite und in JSON.</summary>
    [Fact]
    public async Task RegisterAndPushSnapshot_InstanceAppearsOnline()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = CreateConnection(await SeedTokenAsync());
        await connection.StartAsync(ct);

        var result = await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-1", "Mein PC", RelayProtocolVersion.Current),
            ct);
        result.Accepted.Should().BeTrue();

        await connection.InvokeAsync("PushSnapshot", new StatusSnapshot
        {
            InstanceId = "inst-1",
            InstanceName = "Mein PC",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
            AppVersion = "2.4.0",
            RunningAutomationsCount = 2,
        }, ct);

        var web = await GetWebClientAsync();
        var json = await web.Http.GetStringAsync("/status.json", ct);
        json.Should().Contain("inst-1");
        json.Should().Contain("Mein PC");
        json.Should().Contain("\"connected\":true");
        var html = await web.Http.GetStringAsync("/", ct);
        html.Should().Contain("Mein PC");
        html.Should().Contain("Laufende Aufgaben");
    }

    /// <summary>Eine falsche Protokollversion wird mit verständlichem Grund abgelehnt.</summary>
    [Fact]
    public async Task RegisterInstance_WrongProtocolVersion_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = CreateConnection(await SeedTokenAsync());
        await connection.StartAsync(ct);

        var result = await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-2", null, 999),
            ct);

        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("999");
    }

    /// <summary>PushSnapshot ohne vorherige Registrierung schlägt mit HubException fehl.</summary>
    [Fact]
    public async Task PushSnapshot_WithoutRegistration_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = CreateConnection(await SeedTokenAsync());
        await connection.StartAsync(ct);

        Func<Task> act = () => connection.InvokeAsync("PushSnapshot", new StatusSnapshot
        {
            InstanceId = "inst-3",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
        }, ct);

        await act.Should().ThrowAsync<HubException>();
    }

    /// <summary>Ein ungültiges Token lässt die Instanz-Registrierung scheitern
    /// (die Verbindung wird serverseitig sofort abgebrochen).</summary>
    [Fact]
    public async Task Connect_InvalidToken_RegistrationFails()
    {
        var connection = CreateConnection("falsches-token");
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await connection.StartAsync(ct);
        }
        catch
        {
            // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-x", null, RelayProtocolVersion.Current),
            ct);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>Ein widerrufenes Token wird ebenfalls abgelehnt.</summary>
    [Fact]
    public async Task Connect_RevokedToken_RegistrationFails()
    {
        var seeder = new StatusServerDbSeeder(_factory);
        var created = await seeder.SeedRelayTokenAsync($"revoked-{Guid.NewGuid():N}");
        (await seeder.RevokeRelayTokenAsync(created.Token.Id)).Should().BeTrue();

        var connection = CreateConnection(created.Plaintext);
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await connection.StartAsync(ct);
        }
        catch
        {
            // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-r", null, RelayProtocolVersion.Current),
            ct);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>Ohne Token wird die Registrierung ebenfalls abgelehnt.</summary>
    [Fact]
    public async Task Connect_MissingToken_RegistrationFails()
    {
        var connection = CreateConnection(null);
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await connection.StartAsync(ct);
        }
        catch
        {
            // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-y", null, RelayProtocolVersion.Current),
            ct);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>Nach dem Trennen der Verbindung wird die Instanz als offline/veraltet markiert.</summary>
    [Fact]
    public async Task Disconnect_InstanceMarkedOffline()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = CreateConnection(await SeedTokenAsync());
        await connection.StartAsync(ct);
        await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-off", "Offline-PC", RelayProtocolVersion.Current),
            ct);

        await connection.StopAsync(ct);

        // Der Server verarbeitet den Disconnect asynchron – kurz pollen bis der Zustand sichtbar ist.
        var web = await GetWebClientAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        string json;
        do
        {
            json = await web.Http.GetStringAsync("/status.json", ct);
            if (json.Contains("\"connected\":false"))
                break;
            await Task.Delay(50, ct);
        }
        while (DateTime.UtcNow < deadline);

        json.Should().Contain("\"connected\":false");
        json.Should().Contain("\"isStale\":true");
    }
}
