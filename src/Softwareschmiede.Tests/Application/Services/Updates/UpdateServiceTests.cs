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
        var update = new UpdateInfo("1.2.4", "v1.2.4", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/release.zip")), null, IsPrerelease: false);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Gefunden(update));
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        result.Update.Should().Be(update);
    }

    /// <summary>Eine erfolgreiche Release-Abfrage ohne passenden Kandidaten meldet KeinUpdate.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_ShouldReturnNoUpdate_WhenLookupFindsNoCandidate()
    {
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.KeinTreffer);
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Status.Should().Be(UpdateCheckStatus.KeinUpdate);
        result.Update.Should().BeNull();
    }

    /// <summary>Ein technisch fehlgeschlagener Release-Abruf meldet NichtPruefbar statt KeinUpdate.</summary>
    [Fact]
    public async Task CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLookupFails()
    {
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Fehlgeschlagen);
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Status.Should().Be(UpdateCheckStatus.NichtPruefbar);
        result.Update.Should().BeNull();
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

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(UpdatePfadKanalResolver.ProduktivversionenPfad));

        result.Status.Should().Be(UpdateCheckStatus.NichtPruefbar);
        releaseClient.Verify(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Bei Konflikt zwischen Installationskanal und gewähltem Kanal bleibt das Update verfügbar
    /// und trägt eine nicht-blockierende Warnung; bei passendem Kanal ist die Meldung leer.
    /// </summary>
    [Theory]
    [InlineData("v1.2.0-rc.1", UpdatePfadKanalResolver.ProduktivversionenPfad, true)]
    [InlineData("v1.2.0", UpdatePfadKanalResolver.VorabversionenPfad, true)]
    [InlineData("v1.2.0", UpdatePfadKanalResolver.ProduktivversionenPfad, false)]
    [InlineData("v1.2.0-rc.1", UpdatePfadKanalResolver.VorabversionenPfad, false)]
    public async Task CheckForUpdateAsync_WarnsOnInstallationChannelConflict(
        string installierterTag, string updatePfad, bool erwarteWarnung)
    {
        var istRcKanal = updatePfad == UpdatePfadKanalResolver.VorabversionenPfad;
        var update = istRcKanal
            ? new UpdateInfo("1.3.0-rc.1", "v1.3.0-rc.1", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/rc.zip")), null, IsPrerelease: true)
            : new UpdateInfo("1.3.0", "v1.3.0", "release.zip", new UpdateDownloadRoute(new Uri("https://example.invalid/stable.zip")), null, IsPrerelease: false);
        var versionProvider = new Mock<IApplicationVersionProvider>();
        versionProvider.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.0", installierterTag, null, null));
        var releaseClient = new Mock<IUpdateReleaseClient>();
        releaseClient.Setup(c => c.GetLatestReleaseAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseLookupResult.Gefunden(update));
        var sut = CreateSut(versionProvider.Object, releaseClient.Object);

        var result = await sut.CheckForUpdateAsync(new UpdateCheckOptions(updatePfad));

        result.Status.Should().Be(UpdateCheckStatus.UpdateVerfuegbar);
        result.Update.Should().Be(update);
        if (erwarteWarnung)
            result.Message.Should().NotBeNullOrEmpty("ein Kanalkonflikt muss als nicht-blockierende Warnung mitreisen");
        else
            result.Message.Should().BeNull("bei passendem Kanal darf keine Warnung mitreisen");
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
