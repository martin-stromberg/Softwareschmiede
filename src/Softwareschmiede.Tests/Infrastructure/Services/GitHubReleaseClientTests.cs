using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Tests für <see cref="GitHubReleaseClient"/>.</summary>
public sealed class GitHubReleaseClientTests
{
    /// <summary>Ein stabiles Release mit release.zip wird als UpdateInfo zurückgegeben.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.2.3",
    "prerelease": false,
    "draft": false,
    "published_at": "2026-07-14T00:00:00Z",
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: false));

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.2.3");
        result.IsPrerelease.Should().BeFalse();
        result.DownloadUrl.Should().Be("https://example.invalid/release.zip");
    }

    /// <summary>Leere Listen, unbrauchbare Einträge und HTTP-Fehler werden ignoriert.</summary>
    [Theory]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3\", \"prerelease\": true, \"assets\": [] } ]")]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3\", \"prerelease\": false, \"assets\": [] } ]")]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"v1.2.3-beta.1\", \"prerelease\": false, \"assets\": [{ \"name\": \"release.zip\", \"browser_download_url\": \"https://example.invalid/release.zip\" }] } ]")]
    [InlineData(HttpStatusCode.OK, "[ { \"tag_name\": \"ungueltig\", \"prerelease\": false, \"assets\": [{ \"name\": \"release.zip\", \"browser_download_url\": \"https://example.invalid/release.zip\" }] } ]")]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenReleaseIsNotUsable(HttpStatusCode statusCode, string body)
    {
        var sut = CreateSut(new HttpClient(new StaticHttpHandler(statusCode, body)));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: false));

        result.Should().BeNull();
    }

    /// <summary>Antworten ohne JSON-Liste (Einzelelement oder ungültiges JSON) gelten als nicht prüfbar.</summary>
    [Theory]
    [InlineData("{ \"tag_name\": \"v1.2.3\", \"prerelease\": false, \"assets\": [] }")]
    [InlineData("not json")]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenResponseIsNotAReleaseList(string body)
    {
        var sut = CreateSut(new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, body)));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: false));

        result.Should().BeNull();
    }

    /// <summary>Ein Timeout der Release-Abfrage liefert kein Teilergebnis.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_ShouldReturnNull_WhenRequestTimesOut()
    {
        var httpClient = new HttpClient(new DelayingHttpHandler());
        var sut = CreateSut(httpClient, new UpdateOptions { CheckTimeout = TimeSpan.FromMilliseconds(50) });

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: false));

        result.Should().BeNull();
    }

    private static GitHubReleaseClient CreateSut(HttpClient httpClient, UpdateOptions? options = null)
    {
        return new GitHubReleaseClient(
            httpClient,
            Options.Create(options ?? new UpdateOptions()),
            NullLogger<GitHubReleaseClient>.Instance);
    }

    private sealed class StaticHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StaticHttpHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            });
        }
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
