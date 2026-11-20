using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>ArbeitsverzeichnisSettingsServiceTests.</summary>
public sealed class ArbeitsverzeichnisSettingsServiceTests
{
    private static SoftwareschmiedeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiedeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SoftwareschmiedeDbContext(options);
    }

    /// <summary>SaveArbeitsverzeichnisAsync_ShouldPersistValueInAppEinstellung.</summary>
    [Fact]
    public async Task SaveArbeitsverzeichnisAsync_ShouldPersistValueInAppEinstellung()
    {
        await using var db = CreateDb();
        var sut = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var configuredPath = Path.Combine(Path.GetTempPath(), "unit-workdir");

        await sut.SaveWorkingDirectoryAsync(configuredPath, TestContext.Current.CancellationToken);

        var reloaded = await sut.GetWorkingDirectoryAsync(TestContext.Current.CancellationToken);
        reloaded.Should().Be(Path.GetFullPath(configuredPath));

        var persisted = await db.AppSettings.FirstOrDefaultAsync(a => a.Key == WorkingDirectorySettingsService.RepositoriesWorkdirKey, cancellationToken: TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(Path.GetFullPath(configuredPath));
    }

    /// <summary>SaveArbeitsverzeichnisAsync_ShouldAllowEmptyValue_ForFallbackUsage.</summary>
    [Fact]
    public async Task SaveArbeitsverzeichnisAsync_ShouldAllowEmptyValue_ForFallbackUsage()
    {
        await using var db = CreateDb();
        var sut = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);

        await sut.SaveWorkingDirectoryAsync(" ", TestContext.Current.CancellationToken);

        var reloaded = await sut.GetWorkingDirectoryAsync(TestContext.Current.CancellationToken);
        reloaded.Should().BeNull();
    }

    /// <summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenPathIsNotAbsolute.</summary>
    [Fact]
    public async Task SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenPathIsNotAbsolute()
    {
        await using var db = CreateDb();
        var sut = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);

        var act = () => sut.SaveWorkingDirectoryAsync("relative\\path");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>ValidatePathForConfiguration_ShouldThrowArgumentException_WhenPathContainsInvalidCharacter.</summary>
    [Fact]
    public void ValidatePathForConfiguration_ShouldThrowArgumentException_WhenPathContainsInvalidCharacter()
    {
        var invalidPath = $"C:\\invalid{'\0'}path";

        var act = () => WorkingDirectorySettingsService.ValidatePathForConfiguration(invalidPath);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ungültige Zeichen*");
    }

    /// <summary>SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenDirectoryCannotBeCreated.</summary>
    [Fact]
    public async Task SaveArbeitsverzeichnisAsync_ShouldThrowArgumentException_WhenDirectoryCannotBeCreated()
    {
        await using var db = CreateDb();
        var sut = new WorkingDirectorySettingsService(db, NullLogger<WorkingDirectorySettingsService>.Instance);
        var parentFile = Path.Combine(Path.GetTempPath(), $"workdir-parent-file-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(parentFile, "occupied", TestContext.Current.CancellationToken);
        var invalidDirectoryPath = Path.Combine(parentFile, "child");

        var act = () => sut.SaveWorkingDirectoryAsync(invalidDirectoryPath);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Verzeichnis kann nicht erstellt oder erreicht werden*");

        if (File.Exists(parentFile))
        {
            File.Delete(parentFile);
        }
    }
}
