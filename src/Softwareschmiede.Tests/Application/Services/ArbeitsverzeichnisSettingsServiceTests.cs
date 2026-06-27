using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>ArbeitsverzeichnisSettingsServiceTests.</summary>
public sealed class ArbeitsverzeichnisSettingsServiceTests
{
    private static SoftwareschmiededDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SoftwareschmiededDbContext(options);
    }

    /// <summary><summary>SaveArbeitsverzeichnisAsync_ShouldPersistValueInAppEinstellung.</summary>.</summary>
    [Fact]
    /// <summary>SaveArbeitsverzeichnisAsync_ShouldPersistValueInAppEinstellung.</summary>
    public async Task SaveArbeitsverzeichnisAsync_ShouldPersistValueInAppEinstellung()
    {
        await using var db = CreateDb();
        var sut = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), "unit-workdir");

        await sut.SaveArbeitsverzeichnisAsync(configuredPath);

        var reloaded = await sut.GetArbeitsverzeichnisAsync();
        reloaded.Should().Be(Path.GetFullPath(configuredPath));

        var persisted = await db.AppEinstellungen.FirstOrDefaultAsync(a => a.Schluessel == ArbeitsverzeichnisSettingsService.RepositoriesWorkdirKey);
        persisted.Should().NotBeNull();
        persisted!.Wert.Should().Be(Path.GetFullPath(configuredPath));
    }

    /// <summary><summary>SaveArbeitsverzeichnisAsync_ShouldAllowEmptyValue_ForFallbackUsage.</summary>.</summary>
    [Fact]
    /// <summary>SaveArbeitsverzeichnisAsync_ShouldAllowEmptyValue_ForFallbackUsage.</summary>
    public async Task SaveArbeitsverzeichnisAsync_ShouldAllowEmptyValue_ForFallbackUsage()
    {
        await using var db = CreateDb();
        var sut = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);

        await sut.SaveArbeitsverzeichnisAsync(" ");

        var reloaded = await sut.GetArbeitsverzeichnisAsync();
        reloaded.Should().BeNull();
    }

    /// <summary><summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenPathIsNotAbsolute.</summary>.</summary>
    [Fact]
    /// <summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenPathIsNotAbsolute.</summary>
    public async Task SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenPathIsNotAbsolute()
    {
        await using var db = CreateDb();
        var sut = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);

        var act = () => sut.SaveArbeitsverzeichnisAsync("relative\\path");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary><summary>ValidatePathForConfiguration_ShouldThrowArgumentException_WhenPathContainsInvalidCharacter.</summary>.</summary>
    [Fact]
    /// <summary>ValidatePathForConfiguration_ShouldThrowArgumentException_WhenPathContainsInvalidCharacter.</summary>
    public void ValidatePathForConfiguration_ShouldThrowArgumentException_WhenPathContainsInvalidCharacter()
    {
        var invalidPath = $"C:\\invalid{'\0'}path";

        var act = () => ArbeitsverzeichnisSettingsService.ValidatePathForConfiguration(invalidPath);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ungültige Zeichen*");
    }

    /// <summary><summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenDirectoryCannotBeCreated.</summary>.</summary>
    [Fact]
    /// <summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenDirectoryCannotBeCreated.</summary>
    public async Task SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenDirectoryCannotBeCreated()
    {
        await using var db = CreateDb();
        var sut = new ArbeitsverzeichnisSettingsService(db, NullLogger<ArbeitsverzeichnisSettingsService>.Instance);
        var parentFile = Path.Combine(Path.GetTempPath(), $"workdir-parent-file-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(parentFile, "occupied");
        var invalidDirectoryPath = Path.Combine(parentFile, "child");

        var act = () => sut.SaveArbeitsverzeichnisAsync(invalidDirectoryPath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Verzeichnis kann nicht erstellt oder erreicht werden*");

        if (File.Exists(parentFile))
        {
            File.Delete(parentFile);
        }
    }
}
