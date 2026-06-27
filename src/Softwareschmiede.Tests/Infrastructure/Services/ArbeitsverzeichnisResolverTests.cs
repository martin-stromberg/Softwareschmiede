using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Services;

/// <summary>ArbeitsverzeichnisResolverTests.</summary>
public sealed class ArbeitsverzeichnisResolverTests
{
    /// <summary><summary>ResolveAsync_ShouldReturnConfiguredPath_WhenPathIsWritable.</summary>.</summary>
    [Fact]
    /// <summary>ResolveAsync_ShouldReturnConfiguredPath_WhenPathIsWritable.</summary>
    public async Task ResolveAsync_ShouldReturnConfiguredPath_WhenPathIsWritable()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), $"resolver-ok-{Guid.NewGuid():N}");
        await settings.SaveArbeitsverzeichnisAsync(configuredPath);

        var sut = new ArbeitsverzeichnisResolver(settings, NullLogger<ArbeitsverzeichnisResolver>.Instance);

        var result = await sut.ResolveAsync();

        result.UsedFallback.Should().BeFalse();
        result.ResolvedPath.Should().Be(Path.GetFullPath(configuredPath));

        if (Directory.Exists(configuredPath))
        {
            Directory.Delete(configuredPath, recursive: true);
        }
    }

    /// <summary><summary>ResolveAsync_ShouldFallbackToTemp_WhenNoConfigExists.</summary>.</summary>
    [Fact]
    /// <summary>ResolveAsync_ShouldFallbackToTemp_WhenNoConfigExists.</summary>
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenNoConfigExists()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var sut = new ArbeitsverzeichnisResolver(settings, NullLogger<ArbeitsverzeichnisResolver>.Instance);

        var result = await sut.ResolveAsync();

        result.UsedFallback.Should().BeTrue();
        result.ReasonCode.Should().Be("no-configured-path");
        result.ResolvedPath.Should().Be(Path.GetTempPath());
    }

    /// <summary><summary>ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsFile.</summary>.</summary>
    [Fact]
    /// <summary>ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsFile.</summary>
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsFile()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var filePath = Path.Combine(Path.GetTempPath(), $"resolver-file-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(filePath, "file");
        db.AppEinstellungen.Add(new AppEinstellung
        {
            Id = Guid.NewGuid(),
            Schluessel = ArbeitsverzeichnisSettingsService.RepositoriesWorkdirKey,
            Wert = filePath,
            AktualisiertAm = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new ArbeitsverzeichnisResolver(settings, NullLogger<ArbeitsverzeichnisResolver>.Instance);

        var result = await sut.ResolveAsync();

        result.UsedFallback.Should().BeTrue();
        result.ReasonCode.Should().Be("not-writable-or-unavailable");
        result.ResolvedPath.Should().Be(Path.GetTempPath());

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary><summary>ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsInvalid.</summary>.</summary>
    [Fact]
    /// <summary>ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsInvalid.</summary>
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsInvalid()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var invalidPath = $"invalid{Path.DirectorySeparatorChar}\0path";
        db.AppEinstellungen.Add(new AppEinstellung
        {
            Id = Guid.NewGuid(),
            Schluessel = ArbeitsverzeichnisSettingsService.RepositoriesWorkdirKey,
            Wert = invalidPath,
            AktualisiertAm = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new ArbeitsverzeichnisResolver(settings, NullLogger<ArbeitsverzeichnisResolver>.Instance);

        var result = await sut.ResolveAsync();

        result.UsedFallback.Should().BeTrue();
        result.ReasonCode.Should().Be("invalid-path");
        result.ResolvedPath.Should().Be(Path.GetTempPath());
    }
}
