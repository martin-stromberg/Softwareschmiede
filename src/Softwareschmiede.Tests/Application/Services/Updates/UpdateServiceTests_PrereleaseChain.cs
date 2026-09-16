using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests der Versionskette aus echtem <see cref="ApplicationVersionProvider"/> und echtem <see cref="GitHubReleaseClient"/>.</summary>
public sealed class UpdateServiceTests_PrereleaseChain
{
    private const string Page1Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100";
    private const string Page2Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100&page=2";

    /// <summary>
    /// Nach installiertem 1.3.0-rc.1 sind rc.2 und das stabile 1.3.0 neuer, rc.1, beta.2 und 1.2.9 nicht;
    /// rc.2 wird nur mit aktivierten Prereleases angeboten, die stabile Version auch ohne.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_FromInstalledPrerelease()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "version.json"), """
{
  "version": "v1.3.0-rc.1",
  "tagName": "v1.3.0-rc.1"
}
""");
        var versionProvider = new ApplicationVersionProvider(temp.Path, NullLogger<ApplicationVersionProvider>.Instance);

        var releasesWithRc2 = ReleasesJson("v1.3.0-rc.1", "v1.3.0-beta.2", "v1.2.9", "v1.3.0-rc.2");
        var serviceWithRc2 = CreateService(versionProvider, releasesWithRc2);

        var rcIncluded = await serviceWithRc2.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: true));
        rcIncluded.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        rcIncluded.Update!.Version.Should().Be("1.3.0-rc.2");
        rcIncluded.Update.IsPrerelease.Should().BeTrue();

        var rcExcluded = await serviceWithRc2.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: false));
        rcExcluded.Status.Should().Be(UpdateCheckStatus.KeinUpdate);
        rcExcluded.Update.Should().BeNull();

        var serviceWithStable = CreateService(versionProvider, ReleasesJson("v1.3.0-rc.1", "v1.3.0-beta.2", "v1.2.9", "v1.3.0"));
        var stableResult = await serviceWithStable.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: false));
        stableResult.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        stableResult.Update!.Version.Should().Be("1.3.0");
        stableResult.Update.IsPrerelease.Should().BeFalse();

        var serviceWithoutNewer = CreateService(versionProvider, ReleasesJson("v1.3.0-rc.1", "v1.3.0-beta.2", "v1.2.9"));
        var noUpdate = await serviceWithoutNewer.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: true));
        noUpdate.Status.Should().Be(UpdateCheckStatus.KeinUpdate);
        noUpdate.Update.Should().BeNull();
    }

    /// <summary>Ein Fehler auf einer Folgeseite liefert keinen Teiltreffer; der Service meldet NichtPruefbar.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "version.json"), "{ \"version\": \"v1.2.0\" }");
        var versionProvider = new ApplicationVersionProvider(temp.Path, NullLogger<ApplicationVersionProvider>.Instance);
        var handler = new RoutingHttpHandler();
        handler.AddPage(Page1Url, ReleasesJson("v1.3.0"), Page2Url);
        handler.AddPage(Page2Url, "[]", statusCode: HttpStatusCode.InternalServerError);
        var service = CreateService(versionProvider, new HttpClient(handler));

        var result = await service.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Status.Should().Be(UpdateCheckStatus.NichtPruefbar);
        result.Update.Should().BeNull();
        handler.RequestedUrls.Should().Equal(Page1Url, Page2Url);
    }

    private static UpdateService CreateService(ApplicationVersionProvider versionProvider, string releasesJson)
        => CreateService(versionProvider, new HttpClient(new RoutingHttpHandler(Page1Url, releasesJson)));

    private static UpdateService CreateService(ApplicationVersionProvider versionProvider, HttpClient httpClient)
    {
        var releaseClient = new GitHubReleaseClient(
            httpClient,
            Options.Create(new UpdateOptions()),
            NullLogger<GitHubReleaseClient>.Instance);
        return new UpdateService(
            versionProvider,
            releaseClient,
            Mock.Of<IUpdatePackageService>(),
            Mock.Of<IUpdateScriptService>(),
            Mock.Of<IApplicationShutdownService>(),
            NullLogger<UpdateService>.Instance);
    }

    private static string ReleasesJson(params string[] tags)
    {
        var entries = tags.Select(tag => $$"""
{
  "tag_name": "{{tag}}",
  "prerelease": {{(tag.Contains('-') ? "true" : "false")}},
  "draft": false,
  "assets": [ { "name": "release.zip", "browser_download_url": "https://example.invalid/{{tag}}.zip" } ]
}
""");
        return $"[{string.Join(",", entries)}]";
    }

    private sealed class RoutingHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _routes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _requestedUrls = [];

        public RoutingHttpHandler()
        {
        }

        public RoutingHttpHandler(string url, string body)
        {
            AddPage(url, body);
        }

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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
