using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace Softwareschmiede.StatusServer.Tests;

/// <summary>Tests für die Seiten-Endpunkte und die optionale Basic-Auth-<c>PageAccessMiddleware</c>.</summary>
public sealed class PageAccessTests
{
    /// <summary>Ohne konfigurierte Zugangsdaten sind Seite und JSON frei erreichbar (Dev-Modus).</summary>
    [Fact]
    public async Task Page_WithoutCredentials_ReturnsOk()
    {
        await using var factory = new StatusWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var json = await client.GetStringAsync("/status.json", TestContext.Current.CancellationToken);

        html.Should().Contain("Softwareschmiede-Status");
        html.Should().Contain("Keine App-Instanz hat bisher Daten geliefert");
        json.Should().Contain("instances");
    }

    /// <summary>Mit konfiguriertem Basic-Auth werden Seiten-Anfragen ohne Credentials mit 401 abgewiesen.</summary>
    [Fact]
    public async Task Page_WithBasicAuth_UnauthorizedWithoutCredentials()
    {
        await using var factory = new StatusWebApplicationFactory
        {
            PageUsername = "admin",
            PagePassword = "geheim",
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("Basic");
    }

    /// <summary>Mit korrekten Basic-Auth-Credentials wird die Seite ausgeliefert.</summary>
    [Fact]
    public async Task Page_WithBasicAuth_AuthorizedReturnsOk()
    {
        await using var factory = new StatusWebApplicationFactory
        {
            PageUsername = "admin",
            PagePassword = "geheim",
        };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:geheim")));

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Mit falschem Passwort bleibt die Seite gesperrt.</summary>
    [Fact]
    public async Task Page_WithBasicAuth_WrongPasswordUnauthorized()
    {
        await using var factory = new StatusWebApplicationFactory
        {
            PageUsername = "admin",
            PagePassword = "geheim",
        };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:falsch")));

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Der Relay-Endpunkt ist vom Basic-Auth-Schutz ausgenommen
    /// (kein 401 der Seiten-Middleware; die Hub-Token-Prüfung gilt separat).</summary>
    [Fact]
    public async Task Relay_IsNotProtectedByPageBasicAuth()
    {
        await using var factory = new StatusWebApplicationFactory
        {
            PageUsername = "admin",
            PagePassword = "geheim",
        };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/relay", TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
