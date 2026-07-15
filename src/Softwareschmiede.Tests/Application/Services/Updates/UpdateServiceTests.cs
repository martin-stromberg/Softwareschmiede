using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdateService"/>.</summary>
public sealed class UpdateServiceTests
{
    /// <summary>Eine neuere Remote-Version wird als verfügbar gemeldet.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_ShouldReturnAvailable_WhenRemoteVersionIsNewer()
    {
        var update = new UpdateInfo("1.2.4", "v1.2.4", "release.zip", new Uri("https://example.invalid/release.zip"), null);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestStableReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(update);
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        result.Update.Should().Be(update);
    }

    /// <summary>Gleiche oder nicht prüfbare Versionen zeigen kein verfügbares Update an.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLocalVersionIsMissing()
    {
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstalledVersionInfo?)null);
        var releaseClient = new Mock<IUpdateReleaseClient>();
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync();

        result.Status.Should().Be(UpdateCheckStatus.NichtPruefbar);
        releaseClient.Verify(c => c.GetLatestStableReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Wenn der externe Skriptstart fehlschlägt, wird die App nicht beendet.</summary>
    [Fact]
    public async Task StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails()
    {
        var scriptService = new Mock<IUpdateScriptService>();
        scriptService.Setup(s => s.StartScriptAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start fehlgeschlagen"));
        var shutdownService = new Mock<IApplicationShutdownService>();
        var sut = CreateSut(
            Mock.Of<IApplicationVersionProvider>(),
            Mock.Of<IUpdateReleaseClient>(),
            scriptService.Object,
            shutdownService.Object);
        var preparation = new UpdatePreparationResult("release.zip", "extracted", "update.ps1", "update.log", false);

        var act = () => sut.StartPreparedUpdateAsync(preparation);

        await act.Should().ThrowAsync<InvalidOperationException>();
        shutdownService.Verify(s => s.Shutdown(), Times.Never);
    }

    private static UpdateService CreateSut(
        IApplicationVersionProvider versionProvider,
        IUpdateReleaseClient releaseClient,
        IUpdateScriptService? scriptService = null,
        IApplicationShutdownService? shutdownService = null)
    {
        return new UpdateService(
            versionProvider,
            releaseClient,
            Mock.Of<IUpdatePackageService>(),
            scriptService ?? Mock.Of<IUpdateScriptService>(),
            shutdownService ?? Mock.Of<IApplicationShutdownService>(),
            NullLogger<UpdateService>.Instance);
    }
}
