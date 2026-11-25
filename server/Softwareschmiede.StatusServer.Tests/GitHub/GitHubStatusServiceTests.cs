using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.GitHub;
using Softwareschmiede.StatusServer.Options;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.GitHub;

/// <summary>Tests für <see cref="GitHubStatusService"/> inkl. ETag-/TTL-Cache und Fehlerpfad.</summary>
public sealed class GitHubStatusServiceTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    private static GitHubStatusService CreateSut(
        HttpMessageHandler handler,
        TestTimeProvider time,
        IEnumerable<string>? repositories = null,
        int cacheTtlSeconds = 60)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new StatusServerOptions
        {
            GitHub = new GitHubOptions
            {
                Repositories = repositories?.ToList() ?? ["owner/repo"],
                CacheTtlSeconds = cacheTtlSeconds,
            },
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        return new GitHubStatusService(httpClient, options, time, NullLogger<GitHubStatusService>.Instance);
    }

    private static HttpResponseMessage Json(string content, string? etag = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
        };
        if (etag is not null)
            response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    }

    /// <summary>Ein erfolgreicher Abruf liefert PRs, Releases und Issue-Anzahl.</summary>
    [Fact]
    public async Task GetRepositoryStatusesAsync_Success_ReturnsAggregatedData()
    {
        var time = new TestTimeProvider();
        var handler = new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsoluteUri;
            if (path.Contains("/pulls", StringComparison.Ordinal))
                return Json("""[{"number":42,"title":"Fix","html_url":"https://github.com/owner/repo/pull/42"}]""");
            if (path.Contains("/releases", StringComparison.Ordinal))
                return Json("""[{"name":"Rel","tag_name":"v1.0","html_url":"https://github.com/owner/repo/releases/v1.0","published_at":"2026-01-01T00:00:00Z"}]""");
            if (path.Contains("/issues", StringComparison.Ordinal))
                return Json("""[{"number":1},{"number":2,"pull_request":{}}]""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var sut = CreateSut(handler, time);

        var result = await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);

        var status = result.Should().ContainSingle().Subject;
        status.DisplayName.Should().Be("owner/repo");
        status.OpenPullRequests.Should().ContainSingle().Which.Number.Should().Be(42);
        status.Releases.Should().ContainSingle().Which.TagName.Should().Be("v1.0");
        status.OpenIssuesCount.Should().Be(1);
        status.Error.Should().BeNull();
        status.FetchedUtc.Should().Be(time.Now);
        handler.CallCount.Should().Be(3);
    }

    /// <summary>Innerhalb der TTL werden Ergebnisse aus dem Cache geliefert, ohne neue HTTP-Aufrufe.</summary>
    [Fact]
    public async Task GetRepositoryStatusesAsync_WithinTtl_UsesCache()
    {
        var time = new TestTimeProvider();
        var handler = new FakeHandler(_ => Json("[]", etag: "\"e1\""));
        var sut = CreateSut(handler, time, cacheTtlSeconds: 60);

        await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(30));
        await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);

        handler.CallCount.Should().Be(3);
    }

    /// <summary>Nach Ablauf der TTL wird mit If-None-Match erneut abgefragt; 304 nutzt den Cache-Payload.</summary>
    [Fact]
    public async Task GetRepositoryStatusesAsync_ExpiredAnd304_RevalidatesWithETag()
    {
        var time = new TestTimeProvider();
        var call = 0;
        var handler = new FakeHandler(request =>
        {
            call++;
            if (call <= 3)
                return Json("""[{"number":7,"title":"PR","html_url":"u"}]""", etag: "\"e7\"");
            request.Headers.IfNoneMatch.Should().Contain(e => e.Tag == "\"e7\"");
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        var sut = CreateSut(handler, time, cacheTtlSeconds: 10);

        await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(11));
        var result = await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);

        handler.CallCount.Should().Be(6);
        result.Single().OpenPullRequests.Should().ContainSingle().Which.Number.Should().Be(7);
        result.Single().Error.Should().BeNull();
    }

    /// <summary>HTTP-Fehler werden als Fehler mit Zeitstempel sichtbar statt still zu veralten.</summary>
    [Fact]
    public async Task GetRepositoryStatusesAsync_HttpError_ReportsErrorWithTimestamp()
    {
        var time = new TestTimeProvider();
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var sut = CreateSut(handler, time);

        var result = await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);

        var status = result.Single();
        status.Error.Should().NotBeNullOrEmpty();
        status.ErrorUtc.Should().Be(time.Now);
        status.OpenIssuesCount.Should().BeNull();
    }

    /// <summary>Ungültige Repository-Einträge werden übersprungen.</summary>
    [Fact]
    public async Task GetRepositoryStatusesAsync_InvalidRepositoryEntry_IsSkipped()
    {
        var time = new TestTimeProvider();
        var handler = new FakeHandler(_ => Json("[]"));
        var sut = CreateSut(handler, time, repositories: ["kein-slash-format", "owner/repo"]);

        var result = await sut.GetRepositoryStatusesAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.DisplayName.Should().Be("owner/repo");
        handler.CallCount.Should().Be(3);
    }
}
