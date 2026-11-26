using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>
/// End-to-end-Roundtrip des produktiven <see cref="RemoteStatusRelayClient"/> gegen den
/// in-memory <c>TestServer</c> des Status-Servers (Issue 478): Connect mit Token →
/// RegisterInstance → PushSnapshot → der Snapshot ist über <c>/status.json</c> abrufbar;
/// außerdem Ablehnung bei falschem Token.
/// </summary>
public sealed class RemoteStatusRelayIntegrationTests
    : IClassFixture<RemoteStatusRelayIntegrationTests.StatusServerFixture>, IAsyncDisposable
{
    private readonly StatusServerFixture _factory;
    private RemoteStatusRelayClient? _client;

    /// <summary>Erstellt die Testklasse.</summary>
    /// <param name="factory">Die Server-Factory.</param>
    public RemoteStatusRelayIntegrationTests(StatusServerFixture factory) => _factory = factory;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary><see cref="WebApplicationFactory{TEntryPoint}"/> für den Status-Server
    /// mit festem Relay-Testtoken und deaktivierter Seiten-Auth.</summary>
    public sealed class StatusServerFixture : WebApplicationFactory<Program>
    {
        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Relay:Tokens:0", "it-token");
        }
    }

    /// <summary>Der App-seitige Relay-Client registriert die Instanz, pusht einen Snapshot und
    /// dieser ist danach über /status.json des Servers abrufbar.</summary>
    [Fact]
    public async Task ConnectRegisterAndPush_SnapshotIsVisibleOnStatusJson()
    {
        var client = CreateClient();
        await client.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, "relay"),
            "it-token",
            TestContext.Current.CancellationToken);
        client.IsConnected.Should().BeTrue();

        var registerResult = await client.RegisterInstanceAsync(
            new RegisterInstanceRequest("it-inst-1", "Integrations-PC", RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);
        registerResult.Accepted.Should().BeTrue();

        await client.PushSnapshotAsync(new StatusSnapshot
        {
            InstanceId = "it-inst-1",
            InstanceName = "Integrations-PC",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
            AppVersion = "9.9.9-test",
            RunningAutomationsCount = 1,
            WorkItems =
            [
                new StatusSnapshotWorkItem
                {
                    Title = "Integrationsaufgabe",
                    Status = "Started",
                    RepositoryName = "it-repo",
                },
            ],
        }, TestContext.Current.CancellationToken);

        using var http = _factory.CreateClient();
        var json = await http.GetStringAsync("/status.json", TestContext.Current.CancellationToken);
        json.Should().Contain("it-inst-1");
        json.Should().Contain("Integrations-PC");
        json.Should().Contain("Integrationsaufgabe");
        json.Should().Contain("\"connected\":true");

        await client.DisconnectAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Mit einem unbekannten Token schlägt der Verbindungsaufbau oder die Registrierung fehl.</summary>
    [Fact]
    public async Task Connect_WrongToken_Fails()
    {
        var client = CreateClient();
        try
        {
            await client.ConnectAsync(
                new Uri(_factory.Server.BaseAddress, "relay"),
                "falsches-token",
                TestContext.Current.CancellationToken);
        }
        catch
        {
            return; // Der Server kann die Verbindung bereits beim Aufbau abbrechen.
        }

        Func<Task> act = () => client.RegisterInstanceAsync(
            new RegisterInstanceRequest("it-inst-2", null, RelayProtocolVersion.Current),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Exception>();
    }

    private RemoteStatusRelayClient CreateClient()
    {
        _client = new RemoteStatusRelayClient(NullLogger<RemoteStatusRelayClient>.Instance)
        {
            ConfigureOptions = options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            },
        };
        return _client;
    }
}
