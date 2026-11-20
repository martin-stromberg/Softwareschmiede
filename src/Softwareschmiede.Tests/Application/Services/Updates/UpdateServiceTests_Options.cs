using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests zur Optionsweitergabe und zum defensiven Kanal-Ausschluss in <see cref="UpdateService"/>.</summary>
public sealed class UpdateServiceTests_Options
{
    /// <summary>Ein Pfadwechsel führt zu einem neuen Releaseabruf mit den aktuellen Optionen; ein früherer Fund wird nicht wiederverwendet.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_PropagatesCurrentOptions()
    {
        var stable = new UpdateInfo("1.2.1", "v1.2.1", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/stable.zip")), null, IsPrerelease: false);
        var prerelease = new UpdateInfo("1.3.0-rc.1", "v1.3.0-rc.1", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/rc.zip")), null, IsPrerelease: true);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.0", "v1.2.0-rc.1", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.Channel == UpdateChannel.ReleaseCandidate), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Found(prerelease));
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.Channel == UpdateChannel.Stable), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Found(stable));
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var rcKanal = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePathChannelResolver.PrereleaseVersionsPath), TestContext.Current.CancellationToken);
        var produktivKanal = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePathChannelResolver.StableVersionsPath), TestContext.Current.CancellationToken);

        rcKanal.Status.Should().Be(UpdateCheckStatus.UpdateAvailable);
        rcKanal.Update.Should().Be(prerelease);
        rcKanal.Message.Should().BeNull("RC-Installation und RC-Kanal passen zusammen");
        produktivKanal.Status.Should().Be(UpdateCheckStatus.UpdateAvailable);
        produktivKanal.Update.Should().Be(stable);
        produktivKanal.Update!.IsPrerelease.Should().BeFalse();
        releaseClient.Verify(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.UpdatePath == UpdatePathChannelResolver.PrereleaseVersionsPath), It.IsAny<CancellationToken>()), Times.Once);
        releaseClient.Verify(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.UpdatePath == UpdatePathChannelResolver.StableVersionsPath), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein Client-Ergebnis, das nicht zum gewählten Kanal passt, wird defensiv in beiden Richtungen nicht angeboten.</summary>
    [Theory]
    [InlineData(true, UpdatePathChannelResolver.StableVersionsPath)]
    [InlineData(false, UpdatePathChannelResolver.PrereleaseVersionsPath)]
    public async Task CheckForUpdateAsync_RejectsChannelMismatch(bool isPrereleaseResult, string updatePath)
    {
        var gefundenes = isPrereleaseResult
            ? new UpdateInfo("1.3.0-rc.1", "v1.3.0-rc.1", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/rc.zip")), null, IsPrerelease: true)
            : new UpdateInfo("1.3.0", "v1.3.0", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/stable.zip")), null, IsPrerelease: false);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.0", "v1.2.0", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Found(gefundenes));
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(updatePath), TestContext.Current.CancellationToken);

        result.Status.Should().Be(UpdateCheckStatus.NotCheckable);
        result.Update.Should().BeNull();
    }

    private static UpdateService CreateSut(
        IApplicationVersionProvider versionProvider,
        IUpdateReleaseClient releaseClient)
    {
        return new UpdateService(
            versionProvider,
            releaseClient,
            Mock.Of<IUpdatePackageService>(),
            Mock.Of<IUpdateScriptService>(),
            Mock.Of<IApplicationShutdownService>(),
            NullLogger<UpdateService>.Instance);
    }
}
