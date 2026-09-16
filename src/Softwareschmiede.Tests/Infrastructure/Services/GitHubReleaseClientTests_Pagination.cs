using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Paginations-Tests für <see cref="GitHubReleaseClient"/>.</summary>
public sealed class GitHubReleaseClientTests_Pagination
{
    private const string Page1Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100";
    private const string Page2Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100&page=2";

    /// <summary>Die höchste zulässige SemVer über alle Seiten gewinnt; beide Seiten werden abgerufen.</summary>
    [Theory]
    [InlineData(true, "1.4.0-rc.1", true)]
    [InlineData(false, "1.3.0", false)]
    public async Task GetLatestReleaseAsync_SelectsHighestAcrossPages(bool includePrereleases, string expectedVersion, bool expectedIsPrerelease)
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, """
[
  {
    "tag_name": "v9.0.0",
    "prerelease": false,
    "draft": true,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-9.0.0.zip" } ]
  },
  {
    "tag_name": "keine-version",
    "prerelease": false,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-keine.zip" } ]
  },
  {
    "tag_name": "v8.0.0",
    "prerelease": false,
    "draft": false,
    "assets": []
  },
  {
    "tag_name": "v1.3.0-rc.1",
    "prerelease": true,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0-rc.1.zip" } ]
  },
  {
    "tag_name": "v1.2.1",
    "prerelease": false,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.2.1.zip" } ]
  }
]
""", Page2Url);
        handler.AddPage(Page2Url, """
[
  {
    "tag_name": "v1.4.0-rc.1",
    "prerelease": true,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.4.0-rc.1.zip" } ]
  },
  {
    "tag_name": "v1.2.0",
    "prerelease": false,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.2.0.zip" } ]
  },
  {
    "tag_name": "v1.3.0",
    "prerelease": false,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0.zip" } ]
  }
]
""");
        var sut = CreateSut(new HttpClient(handler));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(includePrereleases));

        result.Should().NotBeNull();
        result!.Version.Should().Be(expectedVersion);
        result.IsPrerelease.Should().Be(expectedIsPrerelease);
        handler.RequestedUrls.Should().Equal(Page1Url, Page2Url);
    }

    /// <summary>Ein HTTP-Fehler auf einer Folgeseite verwirft auch gültige frühere Funde.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate()
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody(), Page2Url);
        handler.AddPage(Page2Url, "[]", statusCode: HttpStatusCode.InternalServerError);
        var sut = CreateSut(new HttpClient(handler));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Should().BeNull();
        handler.RequestedUrls.Should().Equal(Page1Url, Page2Url);
    }

    /// <summary>Ungültiges JSON auf einer Folgeseite verwirft auch gültige frühere Funde.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_LaterPageJsonFailureDiscardsCandidate()
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody(), Page2Url);
        handler.AddPage(Page2Url, "{ \"unexpected\": \"object\" }");
        var sut = CreateSut(new HttpClient(handler));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Should().BeNull();
    }

    /// <summary>Ein Timeout auf einer Folgeseite verwirft auch gültige frühere Funde.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_LaterPageTimeoutDiscardsCandidate()
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody(), Page2Url);
        handler.AddHandler(Page2Url, async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        });
        var sut = CreateSut(new HttpClient(handler), new UpdateOptions { CheckTimeout = TimeSpan.FromMilliseconds(50) });

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Should().BeNull();
    }

    /// <summary>Eine Folge-URL zurück auf eine bereits besuchte Seite (Pagination-Zyklus) ist ein Fehler.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_RejectsPaginationCycle()
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody(), Page2Url);
        handler.AddPage(Page2Url, "[]", nextUrl: Page1Url);
        var sut = CreateSut(new HttpClient(handler));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Should().BeNull();
    }

    /// <summary>Folge-URLs außerhalb der GitHub-API oder des Repository-Releases-Pfades sind ein Fehler.</summary>
    [Theory]
    [InlineData("https://evil.example.com/repos/martin-stromberg/Softwareschmiede/releases?page=2")]
    [InlineData("https://api.github.com/repos/anderer-owner/anderes-repo/releases?page=2")]
    [InlineData("https://api.github.com/repositories/12345/releases?page=2")]
    [InlineData("http://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?page=2")]
    public async Task GetLatestReleaseAsync_RejectsDisallowedFollowUpUrls(string nextUrl)
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody(), nextUrl);
        var sut = CreateSut(new HttpClient(handler));

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Should().BeNull();
        handler.RequestedUrls.Should().Equal(Page1Url);
    }

    /// <summary>Caller-Cancellation propagiert als OperationCanceledException statt als null-Ergebnis.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_PropagatesCallerCancellation()
    {
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ValidPageBody());
        var sut = CreateSut(new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string ValidPageBody()
        => """
[
  {
    "tag_name": "v1.3.0",
    "prerelease": false,
    "draft": false,
    "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0.zip" } ]
  }
]
""";

    private static GitHubReleaseClient CreateSut(HttpClient httpClient, UpdateOptions? options = null)
    {
        return new GitHubReleaseClient(
            httpClient,
            Options.Create(options ?? new UpdateOptions()),
            NullLogger<GitHubReleaseClient>.Instance);
    }

    private sealed class RoutingHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _routes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _requestedUrls = [];

        public IReadOnlyList<string> RequestedUrls => _requestedUrls;

        public void AddPage(string url, string body, string? nextUrl = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _routes[url] = _ =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body)
                };
                if (nextUrl is not null)
                    response.Headers.TryAddWithoutValidation("Link", $"<{nextUrl}>; rel=\"next\"");

                return Task.FromResult(response);
            };
        }

        public void AddHandler(string url, Func<CancellationToken, Task<HttpResponseMessage>> handler)
            => _routes[url] = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return _routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var handler)
                ? handler(cancellationToken)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("[]")
                });
        }
    }
}
