using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests der Versionskette aus echtem <see cref="ApplicationVersionProvider"/> und echtem <see cref="GitHubReleaseClient"/>.</summary>
public sealed class UpdateServiceTests_PrereleaseChain
{
    private const string Page1Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100";
    private const string Page2Url = "https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100&page=2";

    /// <summary>
    /// Nach installiertem 1.3.0-rc.1 gilt im RC-Kanal nur rc.2+ als Update — das stabile 1.3.0
    /// ist kanalfremd und wird nicht angeboten. Im Produktiv-Kanal wird das stabile 1.3.0
    /// angeboten, allerdings mit nicht-blockierender Konfliktwarnung (RC-Installation).
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

        var rcKanal = await serviceWithRc2.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.VorabversionenPfad));
        rcKanal.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        rcKanal.Update!.Version.Should().Be("1.3.0-rc.2");
        rcKanal.Update.IsPrerelease.Should().BeTrue();
        rcKanal.Message.Should().BeNull("RC-Installation und RC-Kanal passen zusammen");

        var produktivKanal = await serviceWithRc2.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));
        produktivKanal.Status.Should().Be(UpdateCheckStatus.KeinUpdate);
        produktivKanal.Update.Should().BeNull();

        var serviceWithStable = CreateService(versionProvider, ReleasesJson("v1.3.0-rc.1", "v1.3.0-beta.2", "v1.2.9", "v1.3.0"));

        // RC-Kanal: das stabile 1.3.0 ist kanalfremd und gilt nicht als Update.
        var rcKanalMitStable = await serviceWithStable.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.VorabversionenPfad));
        rcKanalMitStable.Status.Should().Be(UpdateCheckStatus.KeinUpdate);
        rcKanalMitStable.Update.Should().BeNull();

        // Produktiv-Kanal: das stabile 1.3.0 wird angeboten — mit Konfliktwarnung (RC-Installation).
        var produktivKanalMitStable = await serviceWithStable.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));
        produktivKanalMitStable.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        produktivKanalMitStable.Update!.Version.Should().Be("1.3.0");
        produktivKanalMitStable.Update.IsPrerelease.Should().BeFalse();
        produktivKanalMitStable.Message.Should().NotBeNullOrEmpty(
            "eine RC-Installation im Produktiv-Kanal muss die nicht-blockierende Konfliktwarnung tragen");

        var serviceWithoutNewer = CreateService(versionProvider, ReleasesJson("v1.3.0-rc.1", "v1.3.0-beta.2", "v1.2.9"));
        var noUpdate = await serviceWithoutNewer.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.VorabversionenPfad));
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

        var result = await service.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.VorabversionenPfad));

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
}
