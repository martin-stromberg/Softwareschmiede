using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="ApplicationVersionProvider"/>.</summary>
public sealed class ApplicationVersionProviderTests
{
    /// <summary>Eine gültige version.json wird gelesen und normalisiert.</summary>
    [Fact]
    public async Task GetInstalledVersionAsync_ShouldReadValidVersionJson()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "version.json"), """
{
  "version": "v1.2.3",
  "tagName": "v1.2.3",
  "commit": "abc",
  "createdAtUtc": "2026-07-14T00:00:00Z"
}
""");
        var sut = new ApplicationVersionProvider(temp.Path, NullLogger<ApplicationVersionProvider>.Instance);

        var result = await sut.GetInstalledVersionAsync();

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.2.3");
        result.TagName.Should().Be("v1.2.3");
    }

    /// <summary>Fehlende und ungültige version.json-Dateien werden als nicht prüfbar behandelt.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("{ \"version\": \"keine-version\" }")]
    [InlineData("{ \"tagName\": \"v1.2.3\" }")]
    public async Task GetInstalledVersionAsync_ShouldReturnNull_WhenVersionJsonIsInvalid(string json)
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "version.json"), json);
        var sut = new ApplicationVersionProvider(temp.Path, NullLogger<ApplicationVersionProvider>.Instance);

        var result = await sut.GetInstalledVersionAsync();

        result.Should().BeNull();
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
