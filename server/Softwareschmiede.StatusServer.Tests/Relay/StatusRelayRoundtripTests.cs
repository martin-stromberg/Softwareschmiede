using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.StatusServer.Tests.Relay;

/// <summary>End-to-end-Roundtrip: echte <see cref="HubConnection"/> gegen den in-memory
/// <c>TestServer</c> der <see cref="StatusWebApplicationFactory"/> (Long-Polling-Transport).</summary>
public sealed class StatusRelayRoundtripTests : IClassFixture<StatusWebApplicationFactory>, IAsyncDisposable
{
    private readonly StatusWebApplicationFactory _factory;
    private readonly List<HubConnection> _connections = [];

    /// <summary>Erstellt die Testklasse.</summary>
    /// <param name="factory">Die Server-Factory.</param>
    public StatusRelayRoundtripTests(StatusWebApplicationFactory factory) => _factory = factory;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
            await connection.DisposeAsync();
        GC.SuppressFinalize(this);
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
        var connection = CreateConnection("test-token");
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var result = await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-1", "Mein PC", RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);
        result.Accepted.Should().BeTrue();

        await connection.InvokeAsync("PushSnapshot", new StatusSnapshot
        {
            InstanceId = "inst-1",
            InstanceName = "Mein PC",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
            AppVersion = "2.4.0",
            RunningAutomationsCount = 2,
        }, TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var json = await client.GetStringAsync("/status.json", TestContext.Current.CancellationToken);
        json.Should().Contain("inst-1");
        json.Should().Contain("Mein PC");
        json.Should().Contain("\"connected\":true");
        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        html.Should().Contain("Mein PC");
        html.Should().Contain("Laufende Aufgaben");
    }

    /// <summary>Eine falsche Protokollversion wird mit verständlichem Grund abgelehnt.</summary>
    [Fact]
    public async Task RegisterInstance_WrongProtocolVersion_IsRejected()
    {
        var connection = CreateConnection("test-token");
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var result = await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-2", null, 999),
            TestContext.Current.CancellationToken);

        result.Accepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("999");
    }

    /// <summary>PushSnapshot ohne vorherige Registrierung schlägt mit HubException fehl.</summary>
    [Fact]
    public async Task PushSnapshot_WithoutRegistration_Fails()
    {
        var connection = CreateConnection("test-token");
        await connection.StartAsync(TestContext.Current.CancellationToken);

        Func<Task> act = () => connection.InvokeAsync("PushSnapshot", new StatusSnapshot
        {
            InstanceId = "inst-3",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
        }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HubException>();
    }

    /// <summary>Ein ungültiges Token lässt die Instanz-Registrierung scheitern
    /// (die Verbindung wird serverseitig sofort abgebrochen).</summary>
    [Fact]
    public async Task Connect_InvalidToken_RegistrationFails()
    {
        var connection = CreateConnection("falsches-token");
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-x", null, RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>Ohne Token wird die Registrierung ebenfalls abgelehnt.</summary>
    [Fact]
    public async Task Connect_MissingToken_RegistrationFails()
    {
        var connection = CreateConnection(null);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-y", null, RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>Nach dem Trennen der Verbindung wird die Instanz als offline/veraltet markiert.</summary>
    [Fact]
    public async Task Disconnect_InstanceMarkedOffline()
    {
        var connection = CreateConnection("test-token");
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-off", "Offline-PC", RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);

        await connection.StopAsync(TestContext.Current.CancellationToken);

        // Der Server verarbeitet den Disconnect asynchron – kurz pollen bis der Zustand sichtbar ist.
        using var client = _factory.CreateClient();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        string json;
        do
        {
            json = await client.GetStringAsync("/status.json", TestContext.Current.CancellationToken);
            if (json.Contains("\"connected\":false"))
                break;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        json.Should().Contain("\"connected\":false");
        json.Should().Contain("\"isStale\":true");
    }
}
