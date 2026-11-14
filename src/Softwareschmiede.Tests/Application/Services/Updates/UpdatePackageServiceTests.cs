using System.IO.Compression;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Tests.E2E;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdatePackageService"/>.</summary>
public sealed class UpdatePackageServiceTests
{
    private static readonly Uri PrimaryDownloadUrl = new("https://example.invalid/release.zip");
    private static readonly Uri FallbackAssetApiUrl = new("https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001");
    private static readonly Uri ManifestDownloadUrl = new("https://example.invalid/update.json");
    private static readonly Uri ManifestFallbackAssetApiUrl = new("https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91011");

    /// <summary>Ein gültiges Release-ZIP wird heruntergeladen, verifiziert, entpackt und validiert.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldDownloadExtractAndCreateScript_WhenZipIsValid()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes);
        var sut = CreateSut(temp.Path, handler);
        var progressItems = new List<UpdatePreparationProgress>();

        var result = await sut.PreparePackageAsync(CreateUpdateInfo(), new Progress<UpdatePreparationProgress>(progressItems.Add));

        File.Exists(result.ZipPath).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "Softwareschmiede.exe")).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "version.json")).Should().BeTrue();
        File.Exists(result.ScriptPath).Should().BeTrue();
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.Download);
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.Verifizieren);
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.Entpacken);
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.UpdateVorbereiten);
        handler.RequestedUrls.Should().Equal(PrimaryDownloadUrl.AbsoluteUri, ManifestDownloadUrl.AbsoluteUri);
    }

    /// <summary>Ein Paket, dessen SHA-256 nicht zum Manifest passt, wird vor dem Entpacken verworfen und aufgeräumt.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldThrowInvalidDataAndClean_WhenHashMismatches()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes, manifestJson: UpdateE2EFixture.ErzeugeManifestJson("1.2.3", PrimaryDownloadUrl.AbsoluteUri, zipBytes, sha256Override: new string('0', 64)));
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip.download")).Should().BeFalse();
        Directory.Exists(Path.Combine(temp.Path, "updates", "extracted", "1.2.3")).Should().BeFalse();
    }

    /// <summary>Ein Update ohne Manifest-Referenz wird vor dem Entpacken verworfen.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldThrow_WhenManifestUrlIsMissing()
    {
        using var temp = new TempDirectory();
        var handler = CreateRoutingHandler(CreateZip(includeExe: true, includeVersion: true));
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo() with { ManifestDownload = null }, null);

        await act.Should().ThrowAsync<InvalidDataException>();
        handler.RequestedUrls.Should().Equal(PrimaryDownloadUrl.AbsoluteUri);
        Directory.Exists(Path.Combine(temp.Path, "updates", "extracted", "1.2.3")).Should().BeFalse();
    }

    /// <summary>Ein fehlschlagender Manifest-Abruf ohne Fallback scheitert mit dem HTTP-Fehler und räumt auf.</summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task PreparePackageAsync_ShouldThrow_WhenManifestDownloadFails(HttpStatusCode statusCode)
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes);
        RegistriereFehler(handler, ManifestDownloadUrl, statusCode);
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        Directory.Exists(Path.Combine(temp.Path, "updates", "extracted", "1.2.3")).Should().BeFalse();
    }

    /// <summary>Ein 5xx der primären Manifest-URL löst genau einen Fallback-Request mit octet-stream-Accept auf die API-Asset-URL aus; die Verifikation läuft danach regulär.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldUseManifestFallback_WhenManifestReturns5xx()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes);
        RegistriereFehler(handler, ManifestDownloadUrl, HttpStatusCode.InternalServerError);
        handler.AddHandler(ManifestFallbackAssetApiUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UpdateE2EFixture.ErzeugeManifestJson("1.2.3", PrimaryDownloadUrl.AbsoluteUri, zipBytes))
            }));
        var sut = CreateSut(temp.Path, handler);

        var result = await sut.PreparePackageAsync(
            CreateUpdateInfo(manifestFallbackDownloadUrl: ManifestFallbackAssetApiUrl), null);

        File.Exists(Path.Combine(result.ExtractedDirectory, "Softwareschmiede.exe")).Should().BeTrue();
        handler.RequestedUrls.Should().Equal(
            PrimaryDownloadUrl.AbsoluteUri,
            ManifestDownloadUrl.AbsoluteUri,
            ManifestFallbackAssetApiUrl.AbsoluteUri);
        handler.RequestedAcceptHeaders.Should().HaveCount(3);
        handler.RequestedAcceptHeaders[2].Should().Contain("application/octet-stream");
    }

    /// <summary>Unbrauchbare Manifeste (kein JSON, ohne verwertbaren release.zip-Eintrag oder ungültigen Hash) werden vor dem Entpacken verworfen.</summary>
    [Theory]
    [MemberData(nameof(UnusableManifests))]
    public async Task PreparePackageAsync_ShouldThrowInvalidData_WhenManifestIsUnusable(string manifestJson)
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes, manifestJson: manifestJson);
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        Directory.Exists(Path.Combine(temp.Path, "updates", "extracted", "1.2.3")).Should().BeFalse();
    }

    /// <summary>Unbrauchbare Manifest-Varianten: kein JSON, ohne assets, ohne release.zip-Eintrag, leerer oder nicht-64-Hex-sha256.</summary>
    public static TheoryData<string> UnusableManifests()
        => new()
        {
            "definitiv kein gueltiges json {{{",
            """{"version":"1.2.3"}""",
            """{"assets":null}""",
            """{"assets":[]}""",
            """{"assets":[{"assetName":"other.zip","sha256":"0000000000000000000000000000000000000000000000000000000000000000"}]}""",
            """{"assets":[{"assetName":"release.zip","sha256":""}]}""",
            """{"assets":[{"assetName":"release.zip","sha256":"abc"}]}""",
            """{"assets":[{"assetName":"release.zip","sha256":"zzzz000000000000000000000000000000000000000000000000000000000000"}]}"""
        };

    /// <summary>Ein Prerelease-Paket wird unter seinem vollständigen SemVer-Pfad (inkl. Suffix) entpackt.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldUsePrereleaseVersionPath_WhenPackageIsValid()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true, version: "1.4.0-rc.1");
        var sut = CreateSut(temp.Path, zipBytes);

        var result = await sut.PreparePackageAsync(CreateUpdateInfo("1.4.0-rc.1", isPrerelease: true), null);

        result.ExtractedDirectory.Should().Be(Path.Combine(temp.Path, "updates", "extracted", "1.4.0-rc.1"));
        File.Exists(Path.Combine(result.ExtractedDirectory, "version.json")).Should().BeTrue();
        File.Exists(result.ScriptPath).Should().BeTrue();
    }

    /// <summary>Pfade mit Leerzeichen werden ohne Shell- oder Pfadverkürzung vorbereitet.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldHandleBaseDirectoryWithSpaces()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"Softwareschmiede Test {Guid.NewGuid():N}");
        using var temp = new TempDirectory(basePath);
        var sut = CreateSut(temp.Path, CreateZip(includeExe: true, includeVersion: true));

        var result = await sut.PreparePackageAsync(CreateUpdateInfo(), null);

        result.ExtractedDirectory.Should().StartWith(temp.Path);
        File.Exists(result.ScriptPath).Should().BeTrue();
    }

    /// <summary>ZIPs ohne erwartete Dateien werden kontrolliert abgelehnt.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PreparePackageAsync_ShouldThrow_WhenPackageIsMissingRequiredRootFiles(bool includeExe, bool includeVersion)
    {
        using var temp = new TempDirectory();
        var sut = CreateSut(temp.Path, CreateZip(includeExe, includeVersion));

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Leere Downloads, leere ZIPs und beschädigte ZIPs werden kontrolliert abgelehnt.</summary>
    [Theory]
    [MemberData(nameof(InvalidPackagePayloads))]
    public async Task PreparePackageAsync_ShouldThrowInvalidDataAndCleanExtraction_WhenPackagePayloadIsInvalid(byte[] payload)
    {
        using var temp = new TempDirectory();
        var sut = CreateSut(temp.Path, payload);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip.download")).Should().BeFalse();
        Directory.Exists(Path.Combine(temp.Path, "updates", "extracted", "1.2.3")).Should().BeFalse();
    }

    /// <summary>Downloadfehler schreiben kein finales release.zip und räumen partielle Downloads auf.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldKeepDownloadAtomic_WhenDownloadFails()
    {
        using var temp = new TempDirectory();
        var sut = CreateSut(temp.Path, [1, 2, 3], HttpStatusCode.InternalServerError);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip.download")).Should().BeFalse();
    }

    /// <summary>Ein 5xx der primären Download-URL löst genau einen Fallback-Request mit octet-stream-Accept auf die API-Asset-URL aus; das Paket wird regulär vorbereitet.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = CreateRoutingHandler(zipBytes);
        RegistriereFehler(handler, PrimaryDownloadUrl, HttpStatusCode.InternalServerError);
        handler.AddHandler(FallbackAssetApiUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            }));
        var sut = CreateSut(temp.Path, handler);

        var result = await sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: FallbackAssetApiUrl), null);

        File.Exists(result.ZipPath).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "Softwareschmiede.exe")).Should().BeTrue();
        File.Exists(result.ScriptPath).Should().BeTrue();
        handler.RequestedUrls.Should().Equal(PrimaryDownloadUrl.AbsoluteUri, FallbackAssetApiUrl.AbsoluteUri, ManifestDownloadUrl.AbsoluteUri);
        handler.RequestedAcceptHeaders.Should().HaveCount(3);
        handler.RequestedAcceptHeaders[1].Should().Contain("application/octet-stream");
    }

    /// <summary>Scheitern primäre URL und Fallback mit 5xx, bleibt der bisherige Fehlerpfad ohne Artefakte erhalten.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx()
    {
        using var temp = new TempDirectory();
        var handler = new RoutingHttpHandler();
        RegistriereFehler(handler, PrimaryDownloadUrl, HttpStatusCode.InternalServerError);
        RegistriereFehler(handler, FallbackAssetApiUrl, HttpStatusCode.BadGateway);
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: FallbackAssetApiUrl), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip.download")).Should().BeFalse();
        handler.RequestedUrls.Should().Equal(PrimaryDownloadUrl.AbsoluteUri, FallbackAssetApiUrl.AbsoluteUri);
    }

    /// <summary>Ein Nicht-5xx-Fehler der primären URL wirft sofort - ohne Fallback-Request.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var handler = new RoutingHttpHandler();
        RegistriereFehler(handler, PrimaryDownloadUrl, HttpStatusCode.NotFound);
        handler.AddHandler(FallbackAssetApiUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            }));
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: FallbackAssetApiUrl), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.RequestedUrls.Should().Equal(PrimaryDownloadUrl.AbsoluteUri);
    }

    /// <summary>Bereinigung und Vorbereitung dürfen nicht über eine Verzeichnisgrenze aus dem Programmverzeichnis ausbrechen.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldRejectUpdateDirectoryOutsideBaseEvenWithSharedPrefix()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(parent, "app");
        var outsideDirectory = Path.Combine(parent, "app2");
        using var temp = new TempDirectory(parent);
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(outsideDirectory);
        var sentinel = Path.Combine(outsideDirectory, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        var options = new UpdateOptions { UpdateDirectoryName = "..\\app2" };
        var sut = CreateSut(baseDirectory, CreateZip(includeExe: true, includeVersion: true), options: options);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(), null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(sentinel).Should().BeTrue();
    }

    /// <summary>Ungültige Paket-Nutzdaten für Download- und ZIP-Fehlerpfade.</summary>
    public static TheoryData<byte[]> InvalidPackagePayloads()
        => new()
        {
            Array.Empty<byte>(),
            new byte[] { 1, 2, 3 },
            CreateZip(includeExe: false, includeVersion: false)
        };

    private static UpdatePackageService CreateSut(
        string baseDirectory,
        byte[] zipBytes,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        UpdateOptions? options = null)
        => CreateSut(baseDirectory, CreateRoutingHandler(zipBytes, statusCode), options);

    private static RoutingHttpHandler CreateRoutingHandler(
        byte[] zipBytes,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? manifestJson = null)
    {
        var handler = new RoutingHttpHandler();
        handler.AddHandler(PrimaryDownloadUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(zipBytes)
            }));
        handler.AddHandler(ManifestDownloadUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(manifestJson ?? UpdateE2EFixture.ErzeugeManifestJson("1.2.3", PrimaryDownloadUrl.AbsoluteUri, zipBytes))
            }));
        return handler;
    }

    private static UpdatePackageService CreateSut(
        string baseDirectory,
        HttpMessageHandler httpMessageHandler,
        UpdateOptions? options = null)
    {
        var httpClient = new HttpClient(httpMessageHandler);
        return new UpdatePackageService(
            httpClient,
            new FakeScriptService(),
            Options.Create(options ?? new UpdateOptions()),
            NullLogger<UpdatePackageService>.Instance,
            baseDirectory);
    }

    private static void RegistriereFehler(RoutingHttpHandler handler, Uri url, HttpStatusCode statusCode)
        => handler.AddHandler(url.AbsoluteUri, _ => Task.FromResult(new HttpResponseMessage(statusCode)));

    private static UpdateInfo CreateUpdateInfo(
        string version = "1.2.3",
        bool isPrerelease = false,
        Uri? fallbackDownloadUrl = null,
        Uri? manifestFallbackDownloadUrl = null)
        => new(version, $"v{version}", "release.zip",
            new UpdateDownloadRoute(PrimaryDownloadUrl, fallbackDownloadUrl), DateTimeOffset.UtcNow,
            IsPrerelease: isPrerelease, new UpdateDownloadRoute(ManifestDownloadUrl, manifestFallbackDownloadUrl));

    private static byte[] CreateZip(bool includeExe, bool includeVersion, string version = "1.2.3")
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (includeExe)
            {
                var entry = archive.CreateEntry("Softwareschmiede.exe");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("dummy");
            }

            if (includeVersion)
            {
                var entry = archive.CreateEntry("version.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write($"{{\"version\":\"{version}\"}}");
            }
        }

        return memory.ToArray();
    }

    private sealed class FakeScriptService : IUpdateScriptService
    {
        public Task<string> CreateScriptAsync(string targetDirectory, string extractedDirectory, string executableName, string logPath, CancellationToken ct = default)
        {
            var scriptPath = Path.Combine(targetDirectory, "updates", "update.ps1");
            File.WriteAllText(scriptPath, "# test");
            return Task.FromResult(scriptPath);
        }

        public Task StartScriptAsync(UpdatePreparationResult preparation, CancellationToken ct = default) => Task.CompletedTask;
    }
}
