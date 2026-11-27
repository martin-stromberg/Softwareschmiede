using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Moq;
using msTools.Updater;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Update;

/// <summary>Tests für <see cref="StatusServerUpdateSourceFactory"/>: Quellwahl,
/// benannter HttpClient (Token-Träger), Kanal- und Manifest-Verdrahtung.
/// Der Netzwerkverkehr wird über einen aufzeichnenden <see cref="HttpMessageHandler"/>
/// abgefangen und mit gecachten GitHub-Antworten beantwortet.</summary>
public sealed class StatusServerUpdateSourceFactoryTests
{
    private static readonly StatusServerUpdateSettings s_stableSettings =
        new(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.Off, null);

    private static readonly StatusServerUpdateSettings s_prereleaseSettings =
        new(StatusServerUpdateChannel.ReleaseCandidate, StatusServerAutomationMode.Off, null);

    /// <summary>Bei <c>SourceType=LocalFolder</c> wird eine
    /// <see cref="AutoUpdateLocalFolderSource"/> erzeugt — ohne HttpClient.</summary>
    [Fact]
    public void Create_LocalFolder_ReturnsLocalFolderSource()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var sut = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(),
            new StatusServerUpdateOptions { SourceType = "LocalFolder", LocalFolderPath = "some/dir" });

        var source = sut.Create(s_stableSettings);

        source.Should().BeOfType<AutoUpdateLocalFolderSource>();
        httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    /// <summary>Bei <c>SourceType=Github</c> wird eine <see cref="AutoUpdateGithubSource"/>
    /// über den benannten Client <c>StatusServerUpdate</c> erzeugt (der in
    /// <c>Program.cs</c> den Bearer-Token aus <c>GitHub:Token</c> trägt).</summary>
    [Fact]
    public void Create_Github_UsesNamedHttpClient()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var sut = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(),
            new StatusServerUpdateOptions());

        var source = sut.Create(s_stableSettings);

        source.Should().BeOfType<AutoUpdateGithubSource>();
        httpClientFactory.Verify(
            f => f.CreateClient(StatusServerUpdateSourceFactory.HttpClientName), Times.Once);
        (source as AutoUpdateGithubSource)?.Dispose();
    }

    /// <summary>Die Quelle sendet die GitHub-Anfragen über den übergebenen Client —
    /// inklusive dessen Bearer-Header (wie ihn <c>Program.cs</c> aus
    /// <c>GitHub:Token</c> konfiguriert). Der aufgezeichnete Request geht an die
    /// Releases-API des konfigurierten Repositories.</summary>
    [Fact]
    public async Task Create_GithubSource_CheckAsync_SendsClientsAuthHeader()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var sut = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(),
            new StatusServerUpdateOptions());

        var source = sut.Create(s_stableSettings);
        try
        {
            await source.CheckAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            (source as AutoUpdateGithubSource)?.Dispose();
        }

        handler.Requests.Should().NotBeEmpty();
        handler.Requests.Should().OnlyContain(r =>
            r.Headers.Authorization != null
            && r.Headers.Authorization.Scheme == "Bearer"
            && r.Headers.Authorization.Parameter == "test-token");
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Contain("martin-stromberg/Softwareschmiede");
    }

    /// <summary>Ohne konfiguriertes Token läuft derselbe Request ohne
    /// Authorization-Header.</summary>
    [Fact]
    public async Task Create_GithubSource_CheckAsync_WithoutToken_SendsNoAuthHeader()
    {
        var handler = new RecordingHandler();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var sut = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(),
            new StatusServerUpdateOptions());

        var source = sut.Create(s_stableSettings);
        try
        {
            await source.CheckAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            (source as AutoUpdateGithubSource)?.Dispose();
        }

        handler.Requests.Should().NotBeEmpty();
        handler.Requests.Should().OnlyContain(r => r.Headers.Authorization == null);
    }

    /// <summary>Der Kanal steuert <c>includePrereleases</c>: Mit
    /// <c>ReleaseCandidate</c> wird die Vorabversion v9.9.9-rc.1 gefunden, mit
    /// <c>Stable</c> die stabile v8.0.0 — jeweils über das Asset
    /// <c>statusserver-update.json</c> (nicht das Desktop-<c>update.json</c>).</summary>
    [Fact]
    public async Task Create_GithubSource_RespectsChannelAndManifestAssetName()
    {
        var handler = new RecordingHandler();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var sut = new StatusServerUpdateSourceFactory(
            httpClientFactory.Object, new AutoUpdatePlatformResolver(),
            new StatusServerUpdateOptions());
        var ct = TestContext.Current.CancellationToken;

        var prereleaseSource = sut.Create(s_prereleaseSettings);
        AutoUpdateCheckResult? prereleaseResult;
        try
        {
            prereleaseResult = await prereleaseSource.CheckAsync(ct);
        }
        finally
        {
            (prereleaseSource as AutoUpdateGithubSource)?.Dispose();
        }

        var stableSource = sut.Create(s_stableSettings);
        AutoUpdateCheckResult? stableResult;
        try
        {
            stableResult = await stableSource.CheckAsync(ct);
        }
        finally
        {
            (stableSource as AutoUpdateGithubSource)?.Dispose();
        }

        prereleaseResult?.Package.Should().NotBeNull();
        prereleaseResult!.AvailableVersion.Should().Be("9.9.9-rc.1");
        stableResult?.Package.Should().NotBeNull();
        stableResult!.AvailableVersion.Should().Be("8.0.0");

        // Das Manifest wurde unter dem StatusServer-Namen geholt, nicht als update.json.
        handler.Requests.Should().Contain(r =>
            r.RequestUri!.AbsoluteUri.Contains("statusserver-update.json"));
        handler.Requests.Should().NotContain(r =>
            r.RequestUri!.AbsoluteUri.EndsWith("/update.json", StringComparison.Ordinal));
    }

    /// <summary>Beantwortet GitHub-Releases-API-Anfragen mit einer festen
    /// Release-Liste (stabile v8.0.0 + Prerelease v9.9.9-rc.1) und
    /// Manifest-Anfragen mit dem Rezepte-Format; zeichnet alle Requests auf.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (uri.EndsWith("/releases/latest", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(StableReleaseJson()));
            }

            if (uri.Contains("/releases", StringComparison.Ordinal)
                && !uri.EndsWith(".json", StringComparison.Ordinal))
            {
                // GitHub-Ordnung: neueste Releases zuerst — die Vorabversion steht
                // vor dem älteren stabilen Release.
                return Task.FromResult(JsonResponse($"[{PrereleaseReleaseJson()},{StableReleaseJson()}]"));
            }

            var version = uri.Contains("v8.0.0", StringComparison.Ordinal) ? "8.0.0" : "9.9.9-rc.1";
            return Task.FromResult(JsonResponse(ManifestJson(version)));
        }

        private static HttpResponseMessage JsonResponse(string json)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };

        private static string ReleaseJson(string tag, bool prerelease)
            => $$"""
            {
              "tag_name": "{{tag}}",
              "name": "{{tag}}",
              "draft": false,
              "prerelease": {{(prerelease ? "true" : "false")}},
              "published_at": "2026-01-01T00:00:00Z",
              "assets": [
                { "name": "statusserver-update.json",
                  "browser_download_url": "https://downloads.test/{{tag}}/statusserver-update.json",
                  "url": "https://api.github.com/assets/statusserver-update.json",
                  "size": 512 },
                { "name": "update.json",
                  "browser_download_url": "https://downloads.test/{{tag}}/update.json",
                  "url": "https://api.github.com/assets/update.json",
                  "size": 128 }
              ]
            }
            """;

        private static string StableReleaseJson() => ReleaseJson("v8.0.0", prerelease: false);

        private static string PrereleaseReleaseJson() => ReleaseJson("v9.9.9-rc.1", prerelease: true);

        private static string ManifestJson(string version)
            => $$"""
            {
              "version": "{{version}}",
              "releaseNotes": "Test",
              "publishedAt": "2026-01-01T00:00:00+00:00",
              "assets": [
                { "platform": "windows", "runtimeIdentifier": "win-x64",
                  "assetName": "statusserver-win-x64.zip",
                  "assetUrl": "https://downloads.test/{{version}}/statusserver-win-x64.zip",
                  "sha256": "{{new string('0', 64)}}", "sizeBytes": 1024 }
              ]
            }
            """;
    }
}
