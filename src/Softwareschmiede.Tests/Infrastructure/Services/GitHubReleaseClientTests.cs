using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für <see cref="GitHubReleaseClient"/>.</summary>
public sealed class GitHubReleaseClientTests
{
    private const string AssetApiUrl = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001";
    private const string ManifestApiUrl = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91011";

    /// <summary>Ein stabiles Release mit release.zip und update.json wird als UpdateInfo zurückgegeben.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, $$"""
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      {
        "name": "release.zip",
        "browser_download_url": "https://example.invalid/release.zip",
        "url": "{{AssetApiUrl}}",
        "id": 91001
      },
      {
        "name": "update.json",
        "browser_download_url": "https://example.invalid/update.json",
        "url": "{{ManifestApiUrl}}",
        "id": 91011
      }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be("1.2.3");
        result.Release.IsPrerelease.Should().BeFalse();
        result.Release.PaketDownload.Primary.Should().Be("https://example.invalid/release.zip");
        result.Release.PaketDownload.Fallback.Should().Be(AssetApiUrl);
        result.Release.ManifestDownload!.Primary.Should().Be("https://example.invalid/update.json");
        result.Release.ManifestDownload.Fallback.Should().Be(ManifestApiUrl);
    }

    /// <summary>Das update.json-Asset liefert Manifest-Download-URL (browser_download_url) und Manifest-Fallback-URL (aus url/id abgeleitet).</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldReturnManifestUrls_WhenManifestAssetExists()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, $$"""
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      {
        "name": "release.zip",
        "browser_download_url": "https://example.invalid/release.zip",
        "url": "{{AssetApiUrl}}",
        "id": 91001
      },
      {
        "name": "update.json",
        "browser_download_url": "https://example.invalid/updates/update.json",
        "id": 91011
      }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.ManifestDownload!.Primary.Should().Be("https://example.invalid/updates/update.json");
        result.Release.ManifestDownload.Fallback.Should().Be(ManifestApiUrl);
    }

    /// <summary>Fehlt das update.json-Asset oder seine Download-URL ist unbrauchbar, bleibt das Release Kandidat und die Manifest-URLs sind null - der Abbruch erfolgt erst in der Vorbereitung.</summary>
    [Theory]
    [MemberData(nameof(UnusableManifestAssets))]
    public async Task GetLatestReleaseAsync_ShouldKeepCandidate_WhenManifestAssetIsMissingOrInvalid(string manifestAssetJson)
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, $$"""
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      {
        "name": "release.zip",
        "browser_download_url": "https://example.invalid/release.zip",
        "url": "{{AssetApiUrl}}",
        "id": 91001
      }{{manifestAssetJson}}
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.ManifestDownload.Should().BeNull();
    }

    /// <summary>Manifest-Asset-Varianten ohne verwertbare HTTPS-Download-URL.</summary>
    public static TheoryData<string> UnusableManifestAssets()
        => new()
        {
            "",
            """, { "name": "update.json", "browser_download_url": "keine-url", "id": 91011 }""",
            """, { "name": "update.json", "browser_download_url": "http://example.invalid/update.json", "id": 91011 }""",
            """, { "name": "update.json", "browser_download_url": "ftp://example.invalid/update.json", "id": 91011 }""",
            """, { "name": "update.json", "id": 91011 }"""
        };

    /// <summary>Fehlt die API-Asset-URL, wird die Fallback-Download-URL aus der Asset-ID auf dem GitHub-API-Host abgeleitet.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release.zip", "id": 91001 }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.PaketDownload.Fallback.Should().Be(AssetApiUrl);
    }

    /// <summary>Eine ungültige oder fremde API-Asset-URL führt zur ID-Ableitung; ohne ID entfällt der Fallback, das Release bleibt nutzbar.</summary>
    [Theory]
    [InlineData("https://evil.example/assets/91001", 91001, AssetApiUrl)]
    [InlineData("keine-url", 91001, AssetApiUrl)]
    [InlineData("https://evil.example/assets/91001", 0, null)]
    public async Task GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid(string assetUrl, long assetId, string? expectedFallbackUrl)
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, $$"""
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release.zip", "url": "{{assetUrl}}", "id": {{assetId}} }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        if (expectedFallbackUrl is null)
            result.Release!.PaketDownload.Fallback.Should().BeNull();
        else
            result.Release!.PaketDownload.Fallback.Should().Be(expectedFallbackUrl);
    }

    /// <summary>Leere Listen und unbrauchbare Einträge gelten als erfolgreiche Abfrage ohne Kandidaten; HTTP-Fehler als fehlgeschlagene Abfrage.</summary>
    [Theory]
    [InlineData(HttpStatusCode.OK, "[]", false)]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3\", \"prerelease\": true, \"assets\": [] } ]", false)]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3\", \"prerelease\": false, \"assets\": [] } ]", false)]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3-beta.1\", \"prerelease\": false, \"assets\": [{ \"name\": \"release.zip\", \"browser_download_url\": \"https://example.invalid/release.zip\" }] } ]", false)]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"ungueltig\", \"prerelease\": false, \"assets\": [{ \"name\": \"release.zip\", \"browser_download_url\": \"https://example.invalid/release.zip\" }] } ]", false)]
    [InlineData(HttpStatusCode.InternalServerError, "[]", true)]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenReleaseIsNotUsable(HttpStatusCode statusCode, string body, bool expectFailure)
    {
        var sut = CreateSut(new HttpClient(new StaticHttpHandler(statusCode, body)));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().Be(!expectFailure);
        result.Release.Should().BeNull();
    }

    /// <summary>Antworten ohne JSON-Liste (Einzelelement oder ungültiges JSON) gelten als nicht prüfbar.</summary>
    [Theory]
    [InlineData("{ \"tag_name\": \"v1.2.3\", \"prerelease\": false, \"assets\": [] }")]
    [InlineData("not json")]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenResponseIsNotAReleaseList(string body)
    {
        var sut = CreateSut(new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, body)));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeFalse();
        result.Release.Should().BeNull();
    }

    /// <summary>Ein Timeout der Release-Abfrage liefert kein Teilergebnis.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenRequestTimesOut()
    {
        var httpClient = new HttpClient(new DelayingHttpHandler());
        var sut = CreateSut(httpClient, new UpdateOptions { CheckTimeout = TimeSpan.FromMilliseconds(50) });

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Erfolg.Should().BeFalse();
        result.Release.Should().BeNull();
    }

    private static GitHubReleaseClient CreateSut(HttpClient httpClient, UpdateOptions? options = null)
    {
        return new GitHubReleaseClient(
            httpClient,
            Options.Create(options ?? new UpdateOptions()),
            NullLogger<GitHubReleaseClient>.Instance);
    }

    private sealed class DelayingHttpHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            };
        }
    }
}
