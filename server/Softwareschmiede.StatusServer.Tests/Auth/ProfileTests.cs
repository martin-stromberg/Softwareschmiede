using System.Net;
using FluentAssertions;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für das Benutzerprofil (Passwort ändern).</summary>
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
}
