using System.IO.Compression;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Infrastructure.Services.Updates;

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
    {
        var httpClient = new HttpClient(new BytesHttpHandler(zipBytes, statusCode));
        return new UpdatePackageService(
            httpClient,
            new FakeScriptService(),
            Options.Create(options ?? new UpdateOptions()),
            NullLogger<UpdatePackageService>.Instance,
            baseDirectory);
    }

    private static UpdateInfo CreateUpdateInfo()
        => new("1.2.3", "v1.2.3", "release.zip", new Uri("https://example.invalid/release.zip"), DateTimeOffset.UtcNow);

    private static byte[] CreateZip(bool includeExe, bool includeVersion)
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
                writer.Write("{\"version\":\"1.2.3\"}");
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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
            : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")))
        {
        }

        public TempDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(Path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
