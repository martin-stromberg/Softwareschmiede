using System.Net;
using FluentAssertions;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für Setup, Anmeldung, Abmeldung, Sperrung und CSRF-Schutz
/// (faktische E2E-Nachweise über <see cref="StatusWebApplicationFactory"/>).</summary>
public sealed class SetupAndAuthTests
{
    /// <summary>Setup-Happy-Path: ohne Benutzer leitet / auf /register, der erste
    /// Benutzer wird Administrator und ist direkt angemeldet.</summary>
    [Fact]
    public async Task Register_WithoutUsers_CreatesFirstAdmin_AndSignsIn()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        var root = await client.Http.GetAsync("/", ct);
        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Be("/register");

        var response = await client.RegisterFirstUserAsync("admin", "Geheim-123", ct);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");

        (await seeder.GetRolesAsync("admin")).Should().Contain(StatusRoles.Administration);
        var page = await client.Http.GetStringAsync("/", ct);
        page.Should().Contain("Softwareschmiede-Status");
    }

    /// <summary>Mit vorhandenem Benutzer ist die Registrierung dauerhaft geschlossen.</summary>
    [Fact]
    public async Task Register_WithExistingUsers_IsClosed()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        (await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration)).Succeeded.Should().BeTrue();

        var get = await client.Http.GetAsync("/register", ct);
        get.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Antiforgery-Token über die (anonyme) Login-Seite holen und POST trotzdem probieren.
        await client.RefreshAntiforgeryAsync("/login", ct);
        var post = await client.PostFormAsync("/register",
        [
            new KeyValuePair<string, string>("username", "zweiter"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>("confirmPassword", "Geheim-123"),
        ], cancellationToken: ct);
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        post.Headers.Location!.OriginalString.Should().Be("/");
        (await seeder.CountUsersAsync()).Should().Be(1);
    }

    /// <summary>Ein Benutzername mit Umlauten/Leerzeichen wird abgelehnt — die
    /// Fehlermeldung nennt den erlaubten Zeichensatz, und die Registrierung
    /// bleibt offen.</summary>
    [Fact]
    public async Task Register_InvalidUserName_ExplainsAllowedCharacters()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        var response = await client.RegisterFirstUserAsync("müller a", "Geheim-123", ct);
        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("Zeichen");
        html.Should().Contain("-._@+");

        // Kein Konto angelegt — Setup bleibt offen.
        (await seeder.CountUsersAsync()).Should().Be(0);
        (await client.Http.GetAsync("/register", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Gültige Zugangsdaten liefern eine Session für / und /status.json.</summary>
    [Fact]
    public async Task Login_ValidCredentials_GrantsAccess()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);

        var response = await client.LoginAsync("admin", "Geheim-123", ct);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");

        var html = await client.Http.GetStringAsync("/", ct);
        html.Should().Contain("Softwareschmiede-Status");
        var json = await client.Http.GetStringAsync("/status.json", ct);
        json.Should().Contain("instances");
    }

    /// <summary>Falsches Passwort wird mit generischem Fehler abgelehnt, kein Cookie.</summary>
    [Fact]
    public async Task Login_WrongPassword_Denied()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);

        var response = await client.LoginAsync("admin", "falsch-falsch", ct);
        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("Benutzername oder Passwort ist falsch");

        var root = await client.Http.GetAsync("/", ct);
        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    /// <summary>Drei Fehlversuche sperren für 1 Stunde (LockoutEnd ≈ now + 1 h);
    /// korrektes Passwort bleibt gesperrt; Admin-Freischaltung stellt den Login sofort wieder her.</summary>
    [Fact]
    public async Task Login_ThreeFailures_LocksUser_AdminUnlockRestores()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
        await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);

        for (var i = 0; i < 3; i++)
            await client.LoginAsync("watcher", "falsches-pw", ct);

        var lockoutEnd = await seeder.GetLockoutEndAsync("watcher");
        lockoutEnd.Should().NotBeNull();
        lockoutEnd!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(1), TimeSpan.FromMinutes(2));

        var locked = await client.LoginAsync("watcher", "Geheim-123", ct);
        (await locked.Content.ReadAsStringAsync(ct)).Should().Contain("gesperrt");

        // Admin-Freischaltung über die UI.
        using var adminClient = AuthTestClient.Create(factory);
        (await adminClient.LoginAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var watcherId = (await seeder.GetUserIdAsync("watcher"))!;
        await adminClient.RefreshAntiforgeryAsync("/admin/users", ct);
        var unlock = await adminClient.PostFormAsync($"/admin/users/{watcherId}/unlock", cancellationToken: ct);
        unlock.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.GetLockoutEndAsync("watcher")).Should().BeNull();

        var login = await client.LoginAsync("watcher", "Geheim-123", ct);
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.OriginalString.Should().Be("/");
    }

    /// <summary>Ein deaktivierter Benutzer kann sich nicht anmelden.</summary>
    [Fact]
    public async Task Login_DeactivatedUser_IsRejected()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
        await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);
        (await seeder.SetDeactivatedAsync("watcher", true)).Succeeded.Should().BeTrue();

        var response = await client.LoginAsync("watcher", "Geheim-123", ct);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("deaktiviert");

        var root = await client.Http.GetAsync("/", ct);
        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    /// <summary>Ein deaktivierter Benutzer verliert seine bestehende Session
    /// sofort beim nächsten Request (OnValidatePrincipal).</summary>
    [Fact]
    public async Task DeactivatedUser_LosesExistingSession()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
        await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);

        (await client.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await client.Http.GetAsync("/", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await seeder.SetDeactivatedAsync("watcher", true)).Succeeded.Should().BeTrue();

        var root = await client.Http.GetAsync("/", ct);
        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    /// <summary>POST /logout beendet die Session.</summary>
    [Fact]
    public async Task Logout_ClearsSession()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var logout = await client.LogoutAsync(ct);
        logout.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var root = await client.Http.GetAsync("/", ct);
        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Contain("/login");
    }

    /// <summary>CSRF-Negativtest: POST ohne oder mit ungültigem Antiforgery-Token
    /// wird mit 400 abgelehnt; mit gültigem Token nicht (HTTP-Flow, E2E-Nachweis).</summary>
    [Fact]
    public async Task Post_WithoutAntiforgery_IsRejected()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        // Ohne Token.
        var anonymous = await client.Http.PostAsync("/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "x",
                ["password"] = "y",
            }), ct);
        anonymous.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Mit gefälschtem Token.
        var forged = await client.Http.PostAsync("/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "x",
                ["password"] = "y",
                ["__RequestVerificationToken"] = "gefälscht",
            }), ct);
        forged.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await forged.Content.ReadAsStringAsync(ct)).Should().Contain("abgelaufen");

        // Gegenprobe: mit aus dem Formular extrahiertem Token kein 400.
        await client.RefreshAntiforgeryAsync("/login", ct);
        var valid = await client.PostFormAsync("/login",
        [
            new KeyValuePair<string, string>("username", "x"),
            new KeyValuePair<string, string>("password", "y"),
        ], cancellationToken: ct);
        valid.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);

        // Authentifizierter POST ohne Token ebenfalls 400.
        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var logoutWithoutToken = await client.Http.PostAsync("/logout", new FormUrlEncodedContent([]), ct);
        logoutWithoutToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await logoutWithoutToken.Content.ReadAsStringAsync(ct)).Should().Contain("abgelaufen");
    }

    /// <summary>Zwei parallele Registrierungs-POSTs können nicht beide ein
    /// Administratorkonto erzeugen — der Setup-Guard ist atomar (TOCTOU).
    /// Die Antwort des Verlierers ist je nach Timing 200 (Fehlerseite) oder ein
    /// Redirect (Vorfilter sieht den Benutzer bereits) — die tragende Invariante
    /// ist genau ein Konto und mindestens ein erfolgreicher Durchlauf.</summary>
    [Fact]
    public async Task Register_ParallelPosts_CreateExactlyOneAdmin()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;
        using var first = AuthTestClient.Create(factory);
        using var second = AuthTestClient.Create(factory);

        // Antiforgery-Tokens sequenziell holen, bevor die POSTs parallel laufen —
        // sonst kann das GET /register des Verlierers schon ins geschlossene Setup
        // laufen (Redirect statt Formular) und die Token-Extraktion platzen.
        await first.RefreshAntiforgeryAsync("/register", ct);
        await second.RefreshAntiforgeryAsync("/register", ct);

        static KeyValuePair<string, string>[] Fields(string name, string password) =>
        [
            new KeyValuePair<string, string>("username", name),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("confirmPassword", password),
        ];

        var responses = await Task.WhenAll(
            first.PostFormAsync("/register", Fields("admin-a", "Geheim-123"), cancellationToken: ct),
            second.PostFormAsync("/register", Fields("admin-b", "Geheim-123"), cancellationToken: ct));

        (await seeder.CountUsersAsync()).Should().Be(1);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.Redirect || r.StatusCode == HttpStatusCode.OK);
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.Redirect);
    }

    /// <summary>Innerhalb der ersten halben Ticket-Laufzeit stellt der Server das
    /// Anmelde-Cookie nicht neu aus (klassisches Sliding-Renewal statt Erneuerung
    /// auf jeder Antwort).</summary>
    [Fact]
    public async Task AuthenticatedRequest_YoungTicket_DoesNotRenewCookie()
    {
        await using var factory = new StatusWebApplicationFactory();
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthTestClient.Create(factory);

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await client.Http.GetAsync("/", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies);
        (cookies ?? []).Should().NotContain(v => v.Contains("Identity.Application"));
    }
}
