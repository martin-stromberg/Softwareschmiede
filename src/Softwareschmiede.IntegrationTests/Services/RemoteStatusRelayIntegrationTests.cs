using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.Security;

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
    private static readonly Regex TokenFieldRegex = new(
        "name=\"(?<field>[^\"]*VerificationToken[^\"]*)\"\\s+value=\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled);

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
    /// mit isolierter SQLite-Datenbank (Temp-Pfad, wird beim Dispose aufgeräumt).</summary>
    public sealed class StatusServerFixture : WebApplicationFactory<Program>
    {
        private readonly string _ownedDirectory =
            Path.Combine(Path.GetTempPath(), $"statusserver-relay-it-{Guid.NewGuid():N}");

        /// <inheritdoc/>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Data:DatabasePath", Path.Combine(_ownedDirectory, "statusserver.db"));
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_ownedDirectory, recursive: true);
            }
            catch (IOException)
            {
                // SQLite-Handles können kurz nach dem Dispose noch offen sein —
                // Temp-Rest ist unkritisch und wird vom OS aufgeräumt.
            }
        }
    }

    /// <summary>Der App-seitige Relay-Client registriert die Instanz, pusht einen Snapshot und
    /// dieser ist danach über /status.json des Servers abrufbar.</summary>
    [Fact]
    public async Task ConnectRegisterAndPush_SnapshotIsVisibleOnStatusJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = CreateClient();
        await client.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, "relay"),
            await SeedRelayTokenAsync(ct),
            ct);
        client.IsConnected.Should().BeTrue();

        var registerResult = await client.RegisterInstanceAsync(
            new RegisterInstanceRequest("it-inst-1", "Integrations-PC", RelayProtocolVersion.Current),
            ct);
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
        }, ct);

        using var http = await CreateAuthenticatedClientAsync(ct);
        var json = await http.GetStringAsync("/status.json", ct);
        json.Should().Contain("it-inst-1");
        json.Should().Contain("Integrations-PC");
        json.Should().Contain("Integrationsaufgabe");
        json.Should().Contain("\"connected\":true");

        await client.DisconnectAsync(ct);
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

    private async Task<string> SeedRelayTokenAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider
            .GetRequiredService<RelayTokenService>()
            .CreateAsync("it-token", cancellationToken)
            .ConfigureAwait(false);
        return created.Plaintext;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<UserAdminService>()
            .CreateUserAsync("it-admin", "Geheim-123", StatusRoles.Administration, cancellationToken)
            .ConfigureAwait(false);
        result.Succeeded.Should().BeTrue(result.Error);

        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        var loginHtml = await http.GetStringAsync("/login", cancellationToken);
        var match = TokenFieldRegex.Match(loginHtml);
        match.Success.Should().BeTrue("die Login-Seite muss ein Antiforgery-Feld rendern");

        var response = await http.PostAsync("/login", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", "it-admin"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>(match.Groups["field"].Value, match.Groups["value"].Value),
        ]), cancellationToken);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        return http;
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
