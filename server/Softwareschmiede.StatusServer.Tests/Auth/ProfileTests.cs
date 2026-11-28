using System.Net;
using FluentAssertions;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für das Benutzerprofil (Passwort ändern, Anzeige-Einstellung).</summary>
public sealed class ProfileTests
{
    /// <summary>Passwortänderung im Profil: danach gilt das neue Passwort, das alte nicht;
    /// ein falsches altes Passwort wird mit Fehlermeldung abgelehnt.</summary>
    [Fact]
    public async Task Profile_ChangePassword_Works_AndOldPasswordFails()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Falsches altes Passwort → Fehlermeldung, keine Änderung.
        await client.RefreshAntiforgeryAsync("/profile", ct);
        var wrongCurrent = await client.PostFormAsync("/profile/password",
        [
            new KeyValuePair<string, string>("currentPassword", "nicht-das-alte"),
            new KeyValuePair<string, string>("newPassword", "Neu-Geheim-1"),
            new KeyValuePair<string, string>("confirmPassword", "Neu-Geheim-1"),
        ], cancellationToken: ct);
        (await wrongCurrent.Content.ReadAsStringAsync(ct)).Should().Contain("falsch");

        // Korrekte Änderung → Erfolgsmeldung, Session bleibt bestehen.
        client.ExtractAntiforgery(await wrongCurrent.Content.ReadAsStringAsync(ct));
        var changed = await client.PostFormAsync("/profile/password",
        [
            new KeyValuePair<string, string>("currentPassword", "Geheim-123"),
            new KeyValuePair<string, string>("newPassword", "Neu-Geheim-1"),
            new KeyValuePair<string, string>("confirmPassword", "Neu-Geheim-1"),
        ], cancellationToken: ct);
        changed.StatusCode.Should().Be(HttpStatusCode.Redirect);
        changed.Headers.Location!.OriginalString.Should().Be("/profile?msg=password-changed");
        (await client.FollowRedirectAsync(changed, ct)).Should().Contain("Passwort wurde ge&#228;ndert");
        (await client.Http.GetAsync("/", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Logout → altes Passwort schlägt fehl, neues funktioniert.
        await client.LogoutAsync(ct);

        using var retry = AuthTestClient.Create(factory);
        var oldLogin = await retry.LoginAsync("admin", "Geheim-123", ct);
        (await oldLogin.Content.ReadAsStringAsync(ct)).Should().Contain("falsch");

        var newLogin = await retry.LoginAsync("admin", "Neu-Geheim-1", ct);
        newLogin.StatusCode.Should().Be(HttpStatusCode.Redirect);
        newLogin.Headers.Location!.OriginalString.Should().Be("/");
    }

    /// <summary>Theme-Auswahl speichern (HTTP-Flow, E2E-Nachweis): POST /profile/theme
    /// mit „dark" → PRG-Redirect mit Erfolgsmeldung und vorselektierter Option →
    /// alle Folgeseiten (Status, Profil, Admin) liefern data-theme="dark" aus;
    /// der Wert ist in AspNetUsers persistiert.</summary>
    [Fact]
    public async Task Profile_SaveTheme_PersistsAndAppliesToAllPages()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.GetUserThemeAsync("admin")).Should().Be(ThemePreference.Auto);

        await client.RefreshAntiforgeryAsync("/profile", ct);
        var saved = await client.PostFormAsync("/profile/theme",
            [new KeyValuePair<string, string>("theme", "dark")], cancellationToken: ct);
        saved.StatusCode.Should().Be(HttpStatusCode.Redirect);
        saved.Headers.Location!.OriginalString.Should().Be("/profile?msg=theme-saved");

        var profileHtml = await client.FollowRedirectAsync(saved, ct);
        profileHtml.Should().Contain("Anzeigeeinstellung wurde gespeichert");
        profileHtml.Should().Contain("<option value=\"dark\" selected>");

        (await client.Http.GetStringAsync("/", ct)).Should().Contain("data-theme=\"dark\"");
        (await client.Http.GetStringAsync("/admin/users", ct)).Should().Contain("data-theme=\"dark\"");
        profileHtml.Should().Contain("data-theme=\"dark\"");

        (await seeder.GetUserThemeAsync("admin")).Should().Be(ThemePreference.Dark);
    }

    /// <summary>Ungültiger Theme-Wert: die Profilseite wird direkt mit
    /// Fehlermeldung neu gerendert (kein Redirect), der gespeicherte Wert
    /// bleibt unverändert.</summary>
    [Fact]
    public async Task Profile_SaveTheme_InvalidValue_ShowsError()
    {
        await using var factory = new StatusWebApplicationFactory();
        var seeder = new StatusServerDbSeeder(factory);
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        await client.RefreshAntiforgeryAsync("/profile", ct);
        var response = await client.PostFormAsync("/profile/theme",
            [new KeyValuePair<string, string>("theme", "neon")], cancellationToken: ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(ct);
        html.Should().Contain("class=\"msg err\"");
        html.Should().Contain("Unbekannte Anzeigeeinstellung.");
        (await seeder.GetUserThemeAsync("admin")).Should().Be(ThemePreference.Auto);
    }

    /// <summary>CSRF-Negativtest: POST /profile/theme ohne Antiforgery-Token
    /// wird mit der Formular-abgelaufen-Seite (HTTP 400) abgelehnt.</summary>
    [Fact]
    public async Task Profile_SaveTheme_WithoutAntiforgery_IsRejected()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = AuthTestClient.Create(factory);
        var ct = TestContext.Current.CancellationToken;

        (await client.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await client.Http.PostAsync("/profile/theme",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["theme"] = "dark",
            }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("abgelaufen");
    }
}
