using System.Net;
using FluentAssertions;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für die Admin-Einstellung „Sitzungsdauer".</summary>
public sealed class AdminSettingsTests
{
    /// <summary>Eine geänderte Sitzungsdauer wird persistent gespeichert und für
    /// nachfolgende Anmeldungen als Set-Cookie-Expires übernommen; ungültige
    /// Werte werden mit Fehlermeldung abgelehnt.</summary>
    [Fact]
    public async Task AdminSettings_SessionDuration_IsApplied()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Ungültiger Wert → Fehlermeldung, kein Effekt.
        await admin.RefreshAntiforgeryAsync("/admin/settings", ct);
        var invalid = await admin.PostFormAsync("/admin/settings/session-duration",
            [new KeyValuePair<string, string>("minutes", "0")], cancellationToken: ct);
        (await invalid.Content.ReadAsStringAsync(ct)).Should().Contain("zwischen");
        (await seeder.GetSessionDurationAsync()).Should().Be(480);

        // Gültiger Wert → persistiert; Erfolgs-POST leitet per PRG auf die Seite um.
        var saved = await admin.PostFormAsync("/admin/settings/session-duration",
            [new KeyValuePair<string, string>("minutes", "60")], cancellationToken: ct);
        saved.StatusCode.Should().Be(HttpStatusCode.Redirect);
        saved.Headers.Location!.OriginalString.Should().Be("/admin/settings?msg=settings-saved");
        (await admin.FollowRedirectAsync(saved, ct)).Should().Contain("gespeichert");
        (await seeder.GetSessionDurationAsync()).Should().Be(60);

        // Neue Anmeldung erhält das Expiry aus der Einstellung.
        using var login = AuthTestClient.Create(factory);
        var response = await login.LoginAsync("admin", "Geheim-123", ct);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .First(v => v.Contains("Identity.Application"));
        var expiresIndex = setCookie.IndexOf("expires=", StringComparison.OrdinalIgnoreCase);
        expiresIndex.Should().BeGreaterThanOrEqualTo(0);
        var expiresText = setCookie[(expiresIndex + "expires=".Length)..].Split(';')[0];
        var expires = DateTimeOffset.Parse(expiresText, System.Globalization.CultureInfo.InvariantCulture);
        expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(2));
    }
}
