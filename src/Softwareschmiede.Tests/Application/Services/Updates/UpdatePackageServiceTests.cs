using System.IO.Compression;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="UpdatePackageService"/>.</summary>
public sealed class UpdatePackageServiceTests
{
    /// <summary>Ein gültiges Release-ZIP wird heruntergeladen, entpackt und validiert.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldDownloadExtractAndCreateScript_WhenZipIsValid()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var sut = CreateSut(temp.Path, zipBytes);
        var progressItems = new List<UpdatePreparationProgress>();

        var result = await sut.PreparePackageAsync(CreateUpdateInfo(), new Progress<UpdatePreparationProgress>(progressItems.Add));

        File.Exists(result.ZipPath).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "Softwareschmiede.exe")).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "version.json")).Should().BeTrue();
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.Download);
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.Entpacken);
        progressItems.Should().Contain(p => p.Phase == UpdatePreparationPhase.UpdateVorbereiten);
    }

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
        var primaryUrl = new Uri("https://example.invalid/release.zip");
        var fallbackUrl = new Uri("https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001");
        var handler = new RoutingHttpHandler();
        handler.AddHandler(primaryUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        handler.AddHandler(fallbackUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            }));
        var sut = CreateSut(temp.Path, handler);

        var result = await sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: fallbackUrl), null);

        File.Exists(result.ZipPath).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractedDirectory, "Softwareschmiede.exe")).Should().BeTrue();
        File.Exists(result.ScriptPath).Should().BeTrue();
        handler.RequestedUrls.Should().Equal(primaryUrl.AbsoluteUri, fallbackUrl.AbsoluteUri);
        handler.RequestedAcceptHeaders.Should().HaveCount(2);
        handler.RequestedAcceptHeaders[1].Should().Contain("application/octet-stream");
    }

    /// <summary>Scheitern primäre URL und Fallback mit 5xx, bleibt der bisherige Fehlerpfad ohne Artefakte erhalten.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx()
    {
        using var temp = new TempDirectory();
        var primaryUrl = new Uri("https://example.invalid/release.zip");
        var fallbackUrl = new Uri("https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001");
        var handler = new RoutingHttpHandler();
        handler.AddHandler(primaryUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        handler.AddHandler(fallbackUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: fallbackUrl), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "updates", "download", "release.zip.download")).Should().BeFalse();
        handler.RequestedUrls.Should().Equal(primaryUrl.AbsoluteUri, fallbackUrl.AbsoluteUri);
    }

    /// <summary>Ein Nicht-5xx-Fehler der primären URL wirft sofort - ohne Fallback-Request.</summary>
    [Fact]
    public async Task PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError()
    {
        using var temp = new TempDirectory();
        var zipBytes = CreateZip(includeExe: true, includeVersion: true);
        var primaryUrl = new Uri("https://example.invalid/release.zip");
        var fallbackUrl = new Uri("https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001");
        var handler = new RoutingHttpHandler();
        handler.AddHandler(primaryUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        handler.AddHandler(fallbackUrl.AbsoluteUri,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            }));
        var sut = CreateSut(temp.Path, handler);

        var act = () => sut.PreparePackageAsync(CreateUpdateInfo(fallbackDownloadUrl: fallbackUrl), null);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.RequestedUrls.Should().Equal(primaryUrl.AbsoluteUri);
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
        => CreateSut(baseDirectory, new BytesHttpHandler(zipBytes, statusCode), options);

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

    private static UpdateInfo CreateUpdateInfo(string version = "1.2.3", bool isPrerelease = false, Uri? fallbackDownloadUrl = null)
        => new(version, $"v{version}", "release.zip", new Uri("https://example.invalid/release.zip"), DateTimeOffset.UtcNow, IsPrerelease: isPrerelease, fallbackDownloadUrl);

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

    private sealed class BytesHttpHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        private readonly HttpStatusCode _statusCode;

        public BytesHttpHandler(byte[] bytes, HttpStatusCode statusCode)
        {
            _bytes = bytes;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_bytes)
            });
        }
    }
}
