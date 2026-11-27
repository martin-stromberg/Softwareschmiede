using System.Net;
using FluentAssertions;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für die Admin-Benutzerverwaltung
/// (Anlegen, Stufenänderung, Deaktivieren/Aktivieren, Löschen, Freischalten, Letzter-Admin-Schutz).</summary>
public sealed class AdminUserTests
{
    /// <summary>Admin legt einen Überwacher an; Login als Überwacher;
    /// Deaktivieren/Aktivieren, Sperrung zurücksetzen und Löschen über die UI.</summary>
    [Fact]
    public async Task AdminUsers_Create_Deactivate_Delete_Unlock()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Überwacher über die Admin-UI anlegen (Erfolg → Redirect auf die Liste).
        await admin.RefreshAntiforgeryAsync("/admin/users", ct);
        var created = await admin.PostFormAsync("/admin/users",
        [
            new KeyValuePair<string, string>("username", "watcher"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher),
        ], cancellationToken: ct);
        created.StatusCode.Should().Be(HttpStatusCode.Redirect);
        created.Headers.Location!.OriginalString.Should().Be("/admin/users?msg=user-created");
        (await admin.FollowRedirectAsync(created, ct)).Should().Contain("watcher");
        (await seeder.GetRolesAsync("watcher")).Should().Contain(StatusRoles.Ueberwacher);

        var watcherId = (await seeder.GetUserIdAsync("watcher"))!;

        // Login als Überwacher funktioniert.
        using var watcher = AuthTestClient.Create(factory);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Deaktivieren killt die laufende Session.
        var deactivated = await admin.PostFormAsync($"/admin/users/{watcherId}/deactivate", cancellationToken: ct);
        deactivated.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await watcher.Http.GetAsync("/", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Aktivieren → Login wieder möglich.
        (await admin.PostFormAsync($"/admin/users/{watcherId}/activate", cancellationToken: ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Sperrung aufbauen und per UI zurücksetzen.
        using var brute = AuthTestClient.Create(factory);
        for (var i = 0; i < 3; i++)
            await brute.LoginAsync("watcher", "falsches-pw", ct);
        (await seeder.GetLockoutEndAsync("watcher")).Should().NotBeNull();
        var usersPage = await admin.RefreshAntiforgeryAsync("/admin/users", ct);
        usersPage.Should().Contain("gesperrt");
        (await admin.PostFormAsync($"/admin/users/{watcherId}/unlock", cancellationToken: ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.GetLockoutEndAsync("watcher")).Should().BeNull();

        // Löschen läuft über die Bestätigungsseite (GET) + Bestätigungs-POST.
        var confirm = await admin.Http.GetAsync($"/admin/users/{watcherId}/delete", ct);
        var confirmHtml = await confirm.Content.ReadAsStringAsync(ct);
        confirmHtml.Should().Contain("unwiderruflich");
        confirmHtml.Should().Contain($"action=\"/admin/users/{watcherId}/delete\"");
        admin.ExtractAntiforgery(confirmHtml, "Lösch-Bestätigungsseite");
        (await admin.PostFormAsync($"/admin/users/{watcherId}/delete", cancellationToken: ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.GetUserIdAsync("watcher")).Should().BeNull();
        var login = await watcher.LoginAsync("watcher", "Geheim-123", ct);
        (await login.Content.ReadAsStringAsync(ct)).Should().Contain("falsch");
    }

    /// <summary>Der letzte aktive Administrator ist geschützt: Deaktivieren, Löschen
    /// und Herabstufen werden abgelehnt — für das eigene wie für ein fremdes Konto
    /// (Fremd-Fall am Service belegt, da er über die UI unerreichbar ist:
    /// der letzte aktive Admin ist zwangsläufig der Akteur selbst).</summary>
    [Fact]
    public async Task AdminUsers_LastActiveAdmin_IsProtected()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var adminId = (await seeder.GetUserIdAsync("admin"))!;

        await admin.RefreshAntiforgeryAsync("/admin/users", ct);
        foreach (var action in new[] { "deactivate", "delete" })
        {
            var response = await admin.PostFormAsync($"/admin/users/{adminId}/{action}", cancellationToken: ct);
            var html = await response.Content.ReadAsStringAsync(ct);
            html.Should().Contain("letzte aktive Administrator");
        }

        var demote = await admin.PostFormAsync($"/admin/users/{adminId}/role",
            [new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher)], cancellationToken: ct);
        (await demote.Content.ReadAsStringAsync(ct)).Should().Contain("letzte aktive Administrator");

        // Kein Effekt: Konto weiterhin aktiv mit Stufe Administration.
        (await seeder.GetRolesAsync("admin")).Should().Contain(StatusRoles.Administration);
        (await admin.Http.GetAsync("/admin/users", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Fremd-Fall: ein zweiter Admin wird angelegt und deaktiviert — jetzt ist
        // "admin" letzter aktiver Admin; der Schutz greift auch bei Service-Aufruf
        // mit fremdem Kontext (Seeder) — über die UI unerreichbar.
        await seeder.CreateUserAsync("admin2", "Geheim-123", StatusRoles.Administration);
        var admin2Id = (await seeder.GetUserIdAsync("admin2"))!;
        (await admin.PostFormAsync($"/admin/users/{admin2Id}/deactivate", cancellationToken: ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var foreign = await seeder.SetDeactivatedAsync("admin", true);
        foreign.Succeeded.Should().BeFalse();
        foreign.Error.Should().Contain("letzte aktive Administrator");
    }

    /// <summary>Ein Benutzername außerhalb des erlaubten Zeichensatzes wird über
    /// die Admin-UI mit erklärender Fehlermeldung abgelehnt (kein Konto, kein Redirect).</summary>
    [Fact]
    public async Task AdminUsers_InvalidUserName_ExplainsAllowedCharacters()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await admin.RefreshAntiforgeryAsync("/admin/users", ct);

        var response = await admin.PostFormAsync("/admin/users",
        [
            new KeyValuePair<string, string>("username", "Sabine Maier"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher),
        ], cancellationToken: ct);

        // Fachlicher Fehlschlag → direkte Fehlerseite (kein PRG-Redirect).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("Zeichen");
        html.Should().Contain("-._@+");
    }

    /// <summary>POST /admin/users/{id}/role ändert die Stufe; die neue Stufe wirkt
    /// auf die bestehende Session des betroffenen Benutzers ohne erneute Anmeldung;
    /// ungültige Stufen werden abgelehnt.</summary>
    [Fact]
    public async Task AdminUsers_SetRole_ChangesAccess()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);
        var watcherId = (await seeder.GetUserIdAsync("watcher"))!;

        using var watcher = AuthTestClient.Create(factory);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Überwacher → Administration: Admin-Endpunkte danach ohne Re-Login erreichbar.
        await admin.RefreshAntiforgeryAsync("/admin/users", ct);
        var promote = await admin.PostFormAsync($"/admin/users/{watcherId}/role",
            [new KeyValuePair<string, string>("role", StatusRoles.Administration)], cancellationToken: ct);
        promote.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await watcher.Http.GetAsync("/admin/users", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Administration → Überwacher: Admin-Zugriff entfällt, / bleibt erreichbar.
        var demote = await admin.PostFormAsync($"/admin/users/{watcherId}/role",
            [new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher)], cancellationToken: ct);
        demote.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var denied = await watcher.Http.GetAsync("/admin/users", ct);
        denied.StatusCode.Should().Be(HttpStatusCode.Redirect);
        denied.Headers.Location!.OriginalString.Should().Contain("/access-denied");
        (await watcher.Http.GetAsync("/", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Ungültige Stufe → Ablehnung.
        var invalid = await admin.PostFormAsync($"/admin/users/{watcherId}/role",
            [new KeyValuePair<string, string>("role", "SuperUser")], cancellationToken: ct);
        (await invalid.Content.ReadAsStringAsync(ct)).Should().Contain("Unbekannte Berechtigungsstufe");
    }

    /// <summary>Mutationen auf einen nicht existierenden Benutzer leiten per
    /// <c>?err=</c>-Schlüssel auf die Liste (Post/Redirect/Get auch im
    /// Fehlerfall) — die Zielseite zeigt die feste Fehlermeldung in der Fehlerbox.</summary>
    [Fact]
    public async Task AdminUsers_MutationOnMissingUser_RedirectsWithErr()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await admin.RefreshAntiforgeryAsync("/admin/users", ct);

        var missingId = Guid.NewGuid().ToString("N");
        foreach (var action in new[] { "deactivate", "activate", "unlock", "delete" })
        {
            var response = await admin.PostFormAsync($"/admin/users/{missingId}/{action}", cancellationToken: ct);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.OriginalString.Should().Be("/admin/users?err=user-not-found");
        }

        var role = await admin.PostFormAsync($"/admin/users/{missingId}/role",
            [new KeyValuePair<string, string>("role", StatusRoles.Ueberwacher)], cancellationToken: ct);
        role.StatusCode.Should().Be(HttpStatusCode.Redirect);
        role.Headers.Location!.OriginalString.Should().Be("/admin/users?err=user-not-found");

        var page = await admin.FollowRedirectAsync(role, ct);
        page.Should().Contain("msg err");
        page.Should().Contain("nicht gefunden");
    }

    /// <summary>Freitext im <c>?msg=</c>-/<c>?err=</c>-Query wird nicht reflektiert:
    /// Die GET-Handler lösen nur die festen Schlüssel auf — ein präparierter Link
    /// rendert keine fremde Statusmeldung (bekannte Schlüssel dagegen schon).</summary>
    [Fact]
    public async Task AdminPages_UnknownMessageKeys_RenderNoStatusBox()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var users = await admin.Http.GetStringAsync("/admin/users?msg=ACHTUNG-SPAM&err=ACHTUNG-SPAM", ct);
        users.Should().NotContain("ACHTUNG-SPAM");
        users.Should().NotContain("class=\"msg");

        var tokens = await admin.Http.GetStringAsync("/admin/tokens?err=ACHTUNG-SPAM", ct);
        tokens.Should().NotContain("ACHTUNG-SPAM");
        tokens.Should().NotContain("class=\"msg");

        var settings = await admin.Http.GetStringAsync("/admin/settings?err=ACHTUNG-SPAM&msg=ACHTUNG-SPAM", ct);
        settings.Should().NotContain("ACHTUNG-SPAM");
        settings.Should().NotContain("class=\"msg");

        // Gegenprobe: ein bekannter Schlüssel rendert den zugeordneten festen Text.
        var withKey = await admin.Http.GetStringAsync("/admin/users?msg=user-created", ct);
        withKey.Should().Contain("class=\"msg");
        withKey.Should().Contain("wurde angelegt");
        var withErr = await admin.Http.GetStringAsync("/admin/users?err=user-not-found", ct);
        withErr.Should().Contain("msg err");
    }

    /// <summary>Ein Überwacher wird auf allen Admin-Endpunkten nach /access-denied
    /// umgeleitet; die Status-Seite zeigt keine Admin-Links.</summary>
    [Fact]
    public async Task AdminEndpoints_WatcherRole_IsForbidden()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var watcher = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        await seeder.CreateUserAsync("admin", "Geheim-123", StatusRoles.Administration);
        await seeder.CreateUserAsync("watcher", "Geheim-123", StatusRoles.Ueberwacher);
        (await watcher.LoginAsync("watcher", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var get = await watcher.Http.GetAsync("/admin/users", ct);
        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        get.Headers.Location!.OriginalString.Should().Contain("/access-denied");
        var deniedPage = await watcher.Http.GetStringAsync("/access-denied", ct);
        deniedPage.Should().Contain("Zugriff verweigert");

        // POST ebenfalls verweigert (Token von der Status-Seite).
        await watcher.RefreshAntiforgeryAsync("/", ct);
        var post = await watcher.PostFormAsync("/admin/users",
        [
            new KeyValuePair<string, string>("username", "x"),
            new KeyValuePair<string, string>("password", "Geheim-123"),
            new KeyValuePair<string, string>("role", StatusRoles.Administration),
        ], cancellationToken: ct);
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        post.Headers.Location!.OriginalString.Should().Contain("/access-denied");

        // Status-Seite ohne Admin-Links, aber mit Daten.
        var page = await watcher.Http.GetStringAsync("/", ct);
        page.Should().NotContain("/admin/users");
        (await watcher.Http.GetAsync("/status.json", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
