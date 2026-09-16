using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests zur Optionsweitergabe und zum defensiven Prerelease-Ausschluss in <see cref="UpdateService"/>.</summary>
public sealed class UpdateServiceTests_Options
{
    /// <summary>Ein Optionswechsel führt zu einem neuen Releaseabruf mit den aktuellen Optionen; ein früherer RC-Fund wird nicht wiederverwendet.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_PropagatesCurrentOptions()
    {
        var stable = new UpdateInfo("1.2.1", "v1.2.1", "release.zip", new Uri("https://example.invalid/stable.zip"), null, IsPrerelease: false);
        var prerelease = new UpdateInfo("1.3.0-rc.1", "v1.3.0-rc.1", "release.zip", new Uri("https://example.invalid/rc.zip"), null, IsPrerelease: true);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.0", "v1.2.0", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.IncludePrereleases), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prerelease);
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => !o.IncludePrereleases), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stable);
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var withPrereleases = await sut.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: true));
        var withoutPrereleases = await sut.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: false));

        withPrereleases.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        withPrereleases.Update.Should().Be(prerelease);
        withoutPrereleases.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        withoutPrereleases.Update.Should().Be(stable);
        withoutPrereleases.Update!.IsPrerelease.Should().BeFalse();
        releaseClient.Verify(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => o.IncludePrereleases), It.IsAny<CancellationToken>()), Times.Once);
        releaseClient.Verify(c => c.GetLatestReleaseAsync(It.Is<UpdateCheckOptions>(o => !o.IncludePrereleases), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein als Prerelease klassifiziertes Client-Ergebnis wird bei deaktivierten Prereleases defensiv nicht angeboten.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_ExcludesPrereleaseResult_WhenOptionsDisallowPrereleases()
    {
        var prerelease = new UpdateInfo("1.3.0-rc.1", "v1.3.0-rc.1", "release.zip", new Uri("https://example.invalid/rc.zip"), null, IsPrerelease: true);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.0", "v1.2.0", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prerelease);
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(IncludePrereleases: false));

        result.Status.Should().Be(UpdateCheckStatus.NichtPruefbar);
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
