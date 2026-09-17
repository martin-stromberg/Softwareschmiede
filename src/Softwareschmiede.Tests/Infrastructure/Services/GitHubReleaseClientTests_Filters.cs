using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>Filter- und Klassifikationstests für <see cref="GitHubReleaseClient"/>.</summary>
public sealed class GitHubReleaseClientTests_Filters
{
    /// <summary>Bei deaktivierten Prereleases gewinnt die höchste stabile Version, bei aktivierten der Prerelease-Kandidat.</summary>
    [Theory]
    [InlineData(false, "1.2.1", false)]
    [InlineData(true, "1.3.0-rc.1", true)]
    public async Task GetLatestReleaseAsync_FiltersByOptions(bool includePrereleases, string expectedVersion, bool expectedIsPrerelease)
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.2.1",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.2.1.zip" }
    ]
  },
  {
    "tag_name": "v1.3.0-rc.1",
    "prerelease": true,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0-rc.1.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(includePrereleases));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be(expectedVersion);
        result.Release.IsPrerelease.Should().Be(expectedIsPrerelease);
    }

    /// <summary>Das GitHub-Flag prerelease klassifiziert auch einen stabilen Tag als Prerelease.</summary>
    [Theory]
    [InlineData(false, "1.2.1", false)]
    [InlineData(true, "1.3.0", true)]
    public async Task GetLatestReleaseAsync_FiltersGitHubPrereleaseFlag(bool includePrereleases, string expectedVersion, bool expectedIsPrerelease)
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.3.0",
    "prerelease": true,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0.zip" }
    ]
  },
  {
    "tag_name": "v1.2.1",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.2.1.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(includePrereleases));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be(expectedVersion);
        result.Release.IsPrerelease.Should().Be(expectedIsPrerelease);
    }

    /// <summary>Das SemVer-Prerelease-Suffix klassifiziert auch ein Release ohne GitHub-Flag als Prerelease.</summary>
    [Theory]
    [InlineData(false, "1.2.1", false)]
    [InlineData(true, "1.3.0-rc.1", true)]
    public async Task GetLatestReleaseAsync_FiltersSemVerPrereleaseSuffix(bool includePrereleases, string expectedVersion, bool expectedIsPrerelease)
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.3.0-rc.1",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0-rc.1.zip" }
    ]
  },
  {
    "tag_name": "v1.2.1",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.2.1.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(includePrereleases));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be(expectedVersion);
        result.Release.IsPrerelease.Should().Be(expectedIsPrerelease);
    }

    /// <summary>Drafts, ungültige Tags und Einträge ohne passendes oder ohne absolute HTTP(S)-Asset-URL werden pro Eintrag übersprungen.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_SkipsDraftInvalidTagAndUnusableAssetEntries()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v9.0.0",
    "prerelease": false,
    "draft": true,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-9.0.0.zip" }
    ]
  },
  {
    "tag_name": "keine-version",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-keine.zip" }
    ]
  },
  {
    "tag_name": "v8.0.0",
    "prerelease": false,
    "draft": false,
    "assets": []
  },
  {
    "tag_name": "v7.0.0",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "anderes.zip", "browser_download_url": "https://example.invalid/anderes.zip" }
    ]
  },
  {
    "tag_name": "v6.0.0",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "release.zip" }
    ]
  },
  {
    "tag_name": "v5.0.0",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "release.zip", "browser_download_url": "ftp://example.invalid/release-5.0.0.zip" }
    ]
  },
  {
    "tag_name": "v1.5.0",
    "prerelease": false,
    "draft": false,
    "assets": [
      { "name": "RELEASE.ZIP", "browser_download_url": "https://example.invalid/release-1.5.0.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be("1.5.0");
        result.Release.AssetName.Should().Be("RELEASE.ZIP");
    }

    /// <summary>Der Tag v1.3.0-rc.1 ergibt die normalisierte Version 1.3.0-rc.1 mit korrekter Klassifikation und Asset.</summary>
    [Fact]
    public async Task GetLatestReleaseAsync_PreservesPrereleaseVersion()
    {
        var httpClient = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, """
[
  {
    "tag_name": "v1.3.0-rc.1",
    "prerelease": true,
    "draft": false,
    "published_at": "2026-07-15T00:00:00Z",
    "assets": [
      { "name": "release.zip", "browser_download_url": "https://example.invalid/release-1.3.0-rc.1.zip" }
    ]
  }
]
"""));
        var sut = CreateSut(httpClient);

        var result = await sut.GetLatestReleaseAsync(new UpdateCheckOptions(IncludePrereleases: true));

        result.Erfolg.Should().BeTrue();
        result.Release.Should().NotBeNull();
        result.Release!.Version.Should().Be("1.3.0-rc.1");
        result.Release.TagName.Should().Be("v1.3.0-rc.1");
        result.Release.IsPrerelease.Should().BeTrue();
        result.Release.DownloadUrl.Should().Be("https://example.invalid/release-1.3.0-rc.1.zip");
    }

    private static GitHubReleaseClient CreateSut(HttpClient httpClient)
    {
        return new GitHubReleaseClient(
            httpClient,
            Options.Create(new UpdateOptions()),
            NullLogger<GitHubReleaseClient>.Instance);
    }
}
