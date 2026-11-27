using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Auth;

/// <summary>HTTP-Flow-Tests für die Admin-Token-Verwaltung (Erstellen, Listen, Widerrufen).</summary>
public sealed class AdminTokenTests
{
    /// <summary>Ein neues Token wird genau einmal im Klartext angezeigt; die DB
    /// speichert nur den Hash; das Token ist gültig; nach dem Widerruf (über die
    /// Bestätigungsseite) zeigt die Liste „widerrufen" und der Validator lehnt es ab.</summary>
    [Fact]
    public async Task AdminTokens_CreateShowsOnce_RevokeBlocks()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var admin = AuthTestClient.Create(factory);
        var seeder = new StatusServerDbSeeder(factory);
        var ct = TestContext.Current.CancellationToken;

        (await admin.RegisterFirstUserAsync("admin", "Geheim-123", ct)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var list = await admin.RefreshAntiforgeryAsync("/admin/tokens", ct);
        list.Should().Contain("Noch keine Relay-Tokens vorhanden");

        // Ungültiges Label → Fehlermeldung in der Fehler-Box (nicht als Erfolg).
        var invalidLabel = await admin.PostFormAsync("/admin/tokens",
            [new KeyValuePair<string, string>("label", new string('x', RelayTokenService.MaxLabelLength + 1))],
            cancellationToken: ct);
        var invalidHtml = await invalidLabel.Content.ReadAsStringAsync(ct);
        invalidHtml.Should().Contain("msg err");
        invalidHtml.Should().Contain("Label muss");

        // Erfolg → Redirect auf die Liste mit einmaligem ?created=-Schlüssel.
        var created = await admin.PostFormAsync("/admin/tokens",
            [new KeyValuePair<string, string>("label", "Instanz-A")], cancellationToken: ct);
        created.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var createdLocation = created.Headers.Location!.OriginalString;
        createdLocation.Should().StartWith("/admin/tokens?created=");
        var html = await admin.Http.GetStringAsync(createdLocation, ct);
        html.Should().Contain("nur jetzt");
        var plaintext = Regex.Match(html, "<code>(?<tok>[A-Za-z0-9_-]{40,50})</code>").Groups["tok"].Value;
        plaintext.Should().HaveLength(43);

        // DB enthält nur den Hash, niemals den Klartext.
        var entity = await seeder.GetRelayTokenAsync("Instanz-A");
        entity.Should().NotBeNull();
        entity!.TokenHash.Should().Be(RelayTokenService.HashToken(plaintext));
        entity.TokenHash.Should().NotBe(plaintext);
        entity.RevokedUtc.Should().BeNull();
        (await seeder.IsRelayTokenValidAsync(plaintext)).Should().BeTrue();

        // Refresh des ?created=-Links (F5): kein Klartext mehr, kein zweites Token —
        // nur der Hinweis, dass die Einmal-Anzeige verbraucht ist.
        var refreshed = await admin.Http.GetStringAsync(createdLocation, ct);
        refreshed.Should().NotContain(plaintext);
        refreshed.Should().Contain("nicht mehr abrufbar");
        (await seeder.CountRelayTokensAsync()).Should().Be(1);

        // Zweiter Aufruf der Liste: Klartext nicht mehr enthalten.
        var relisted = await admin.RefreshAntiforgeryAsync("/admin/tokens", ct);
        relisted.Should().NotContain(plaintext);

        // Widerruf läuft über die Bestätigungsseite (GET) + Bestätigungs-POST.
        var confirmHtml = await admin.Http.GetStringAsync($"/admin/tokens/{entity.Id}/revoke", ct);
        confirmHtml.Should().Contain("unwiderruflich");
        confirmHtml.Should().Contain($"action=\"/admin/tokens/{entity.Id}/revoke\"");
        admin.ExtractAntiforgery(confirmHtml, "Widerruf-Bestätigungsseite");
        var revoked = await admin.PostFormAsync($"/admin/tokens/{entity.Id}/revoke", cancellationToken: ct);
        revoked.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await seeder.GetRelayTokenAsync("Instanz-A"))!.RevokedUtc.Should().NotBeNull();
        (await seeder.IsRelayTokenValidAsync(plaintext)).Should().BeFalse();

        // Widerruf eines unbekannten Tokens → Redirect mit err-Schlüssel (PRG);
        // die Zielseite zeigt die feste Fehlermeldung in der Fehler-Box.
        var unknown = await admin.PostFormAsync("/admin/tokens/99999/revoke", cancellationToken: ct);
        unknown.StatusCode.Should().Be(HttpStatusCode.Redirect);
        unknown.Headers.Location!.OriginalString.Should().Be("/admin/tokens?err=token-not-found");
        var unknownPage = await admin.FollowRedirectAsync(unknown, ct);
        unknownPage.Should().Contain("msg err");
        unknownPage.Should().Contain("nicht gefunden");
    }
}
