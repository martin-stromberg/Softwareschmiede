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
    public async Task ResolveAsync_ShouldReturnConfiguredPath_WhenPathIsWritable()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), $"resolver-ok-{Guid.NewGuid():N}");
        await settings.SaveWorkingDirectoryAsync(configuredPath, TestContext.Current.CancellationToken);

        var sut = new RuntimeWorkingDirectoryResolver(settings, NullLogger<RuntimeWorkingDirectoryResolver>.Instance);

        var result = await sut.ResolveAsync(TestContext.Current.CancellationToken);

        result.UsedFallback.Should().BeFalse();
        result.ResolvedPath.Should().Be(Path.GetFullPath(configuredPath));

        if (Directory.Exists(configuredPath))
        {
            Directory.Delete(configuredPath, recursive: true);
        }
    }

    /// <summary><summary>ResolveAsync_ShouldFallbackToTemp_WhenNoConfigExists.</summary>.</summary>
    [Fact]
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenNoConfigExists()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var sut = new RuntimeWorkingDirectoryResolver(settings, NullLogger<RuntimeWorkingDirectoryResolver>.Instance);

        var result = await sut.ResolveAsync(TestContext.Current.CancellationToken);

        result.UsedFallback.Should().BeTrue();
        result.ReasonCode.Should().Be("no-configured-path");
        result.ResolvedPath.Should().Be(Path.GetTempPath());
    }

    /// <summary><summary>ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsFile.</summary>.</summary>
    [Fact]
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsFile()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var filePath = Path.Combine(Path.GetTempPath(), $"resolver-file-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(filePath, "file", TestContext.Current.CancellationToken);
        db.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = WorkingDirectorySettingsService.RepositoriesWorkdirKey,
            Value = filePath,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new RuntimeWorkingDirectoryResolver(settings, NullLogger<RuntimeWorkingDirectoryResolver>.Instance);

        var result = await sut.ResolveAsync(TestContext.Current.CancellationToken);

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
    public async Task ResolveAsync_ShouldFallbackToTemp_WhenConfiguredPathIsInvalid()
    {
        await using var db = TestDbContextFactory.Create();
        var settings = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var invalidPath = $"invalid{Path.DirectorySeparatorChar}\0path";
        db.AppSettings.Add(new AppSetting
        {
            Id = Guid.NewGuid(),
            Key = WorkingDirectorySettingsService.RepositoriesWorkdirKey,
            Value = invalidPath,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new RuntimeWorkingDirectoryResolver(settings, NullLogger<RuntimeWorkingDirectoryResolver>.Instance);

        var result = await sut.ResolveAsync(TestContext.Current.CancellationToken);

        result.UsedFallback.Should().BeTrue();
        result.ReasonCode.Should().Be("invalid-path");
        result.ResolvedPath.Should().Be(Path.GetTempPath());
    }
}
