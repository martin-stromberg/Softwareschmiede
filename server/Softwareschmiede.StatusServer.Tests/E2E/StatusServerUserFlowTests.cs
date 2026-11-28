using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.E2E;

/// <summary>E2E-Nachweise der Benutzerflüsse, die die <c>Auth/*</c>-Testklassen nicht
/// abdecken: der kombinierte Happy Path mit SignalR-Relay, die Weiterleitung
/// unauthentifizierter Aufrufe und die Neustart-Persistenz.</summary>
public sealed class StatusServerUserFlowTests
{
    private static HubConnection CreateRelayConnection(StatusWebApplicationFactory factory, string token)
        => new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "relay"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static string ExtractPlaintextToken(string createdPageHtml)
    {
        createdPageHtml.Should().Contain("nur jetzt");
        return Regex.Match(createdPageHtml, "<code>(?<tok>[A-Za-z0-9_-]{43})</code>").Groups["tok"].Value;
    }

    /// <summary>Happy Path: Registrierung → Admin legt Überwacher und Token an →
    /// App verbindet sich per Token am Relay → Überwacher sieht die Instanz.</summary>
    [Fact]
    public async Task HappyPath_FullSetupToRelayFlow()
    {
        await using var factory = new StatusWebApplicationFactory();
        var ct = TestContext.Current.CancellationToken;
        using var admin = AuthTestClient.Create(factory);

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Überwacher über die Admin-UI anlegen.
        await admin.RefreshAntiforgeryAsync("/admin/users", ct);
        (await admin.PostFormAsync("/admin/users",
        [
            new KeyValuePair<string, string>("username", "watcher"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher),
        ], cancellationToken: ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Token über die Admin-UI anlegen → Klartext einmalig (PRG: Redirect,
        // Klartext erscheint auf der ?created=-Zielseite).
        await admin.RefreshAntiforgeryAsync("/admin/tokens", ct);
        var created = await admin.PostFormAsync("/admin/tokens",
            [new KeyValuePair<string, string>("label", "Martin-PC")], cancellationToken: ct);
        created.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var createdHtml = await admin.FollowRedirectAsync(created, ct);
        var plaintext = ExtractPlaintextToken(createdHtml);

        // App-Instanz verbindet sich mit dem Token und pusht einen Snapshot.
        await using var relay = CreateRelayConnection(factory, plaintext);
        await relay.StartAsync(ct);
        var registered = await relay.InvokeAsync<RegisterInstanceResult>(
            "RegisterInstance",
            new RegisterInstanceRequest("inst-e2e", "Martin-PC", RelayProtocolVersion.Current), ct);
        registered.Accepted.Should().BeTrue();
        await relay.InvokeAsync("PushSnapshot", new StatusSnapshot
        {
            InstanceId = "inst-e2e",
            InstanceName = "Martin-PC",
            CreatedUtc = DateTimeOffset.UtcNow,
            ProtocolVersion = RelayProtocolVersion.Current,
        }, ct);

        // Überwacher meldet sich an und sieht die verbundene Instanz.
        using var watcher = AuthTestClient.Create(factory);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var page = await watcher.Http.GetStringAsync("/", ct);
        page.Should().Contain("Martin-PC");
        page.Should().Contain("badge online");
    }

    /// <summary>Unauthentifizierte Zugriffe werden auf /login bzw. /register
    /// umgeleitet; nach dem Setup ist die Registrierung geschlossen.</summary>
    [Fact]
    public async Task RegistrationClosed_AndUnauthenticatedRedirect()
    {
        await using var factory = new StatusWebApplicationFactory();
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthTestClient.Create(factory);

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var anonymous = AuthTestClient.Create(factory);
        var root = await anonymous.Http.GetAsync("/", ct);
        root.Headers.Location!.OriginalString.Should().Contain("/login");
        var json = await anonymous.Http.GetAsync("/status.json", ct);
        json.Headers.Location!.OriginalString.Should().Contain("/login");
        var register = await anonymous.Http.GetAsync("/register", ct);
        register.Headers.Location!.OriginalString.Should().Be("/");
        var admin = await anonymous.Http.GetAsync("/admin/users", ct);
        admin.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    /// <summary>Neustart-Persistenz über alle Elemente: Benutzer + Rollen,
    /// Tokens (gültig und widerrufen) sowie Server-Einstellungen.</summary>
    [Fact]
    public async Task Restart_SameDatabase_AllStateSurvives()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"statusserver-e2e-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(dir, "statusserver.db");
        try
        {
            string validToken;
            string revokedToken;
            await using (var factory = new StatusWebApplicationFactory { DatabasePathOverride = dbPath })
            {
                var seeder = new StatusServerDbSeeder(factory);
                await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
                await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);
                validToken = (await seeder.SeedRelayTokenAsync("gültig")).Plaintext;
                var revoked = await seeder.SeedRelayTokenAsync("widerrufen");
                revokedToken = revoked.Plaintext;
                (await seeder.RevokeRelayTokenAsync(revoked.Token.Id)).Should().BeTrue();
                await seeder.SetSessionDurationAsync(60);
            }

            await using (var factory = new StatusWebApplicationFactory { DatabasePathOverride = dbPath })
            {
                var seeder = new StatusServerDbSeeder(factory);
                var ct = TestContext.Current.CancellationToken;
                using var client = AuthTestClient.Create(factory);
                (await client.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
                (await seeder.IsRelayTokenValidAsync(validToken)).Should().BeTrue();
                (await seeder.IsRelayTokenValidAsync(revokedToken)).Should().BeFalse();
                (await seeder.GetSessionDurationAsync()).Should().Be(60);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Die Theme-Einstellung ist pro Benutzer persistiert: ein auf Dark
    /// gestelltes Theme überlebt einen Server-Neustart auf derselben Datenbank
    /// (E2E-Nachweis der AspNetUsers.Theme-Spalte über den Factory-Neustart).</summary>
    [Fact]
    public async Task Theme_SurvivesServerRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"statusserver-e2e-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(dir, "statusserver.db");
        try
        {
            await using (var factory = new StatusWebApplicationFactory { DatabasePathOverride = dbPath })
            {
                var seeder = new StatusServerDbSeeder(factory);
                var ct = TestContext.Current.CancellationToken;
                await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);

                using var client = AuthTestClient.Create(factory);
                (await client.LoginAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
                await client.RefreshAntiforgeryAsync("/profile", ct);
                (await client.PostFormAsync("/profile/theme",
                    [new KeyValuePair<string, string>("theme", "dark")], cancellationToken: ct))
                    .StatusCode.Should().Be(HttpStatusCode.Redirect);
                (await seeder.GetUserThemeAsync("admin")).Should().Be(ThemePreference.Dark);
            }

            await using (var factory = new StatusWebApplicationFactory { DatabasePathOverride = dbPath })
            {
                var seeder = new StatusServerDbSeeder(factory);
                var ct = TestContext.Current.CancellationToken;
                using var client = AuthTestClient.Create(factory);
                (await client.LoginAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
                (await seeder.GetUserThemeAsync("admin")).Should().Be(ThemePreference.Dark);
                (await client.Http.GetStringAsync("/", ct)).Should().Contain("data-theme=\"dark\"");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
