using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den SCM-Kontomodus (<see cref="AppSettingService.ScmAccountModeKey"/>).</summary>
public sealed class AppSettingServiceTests_AccountMode : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppSettingServiceTests_AccountMode"/>
    public AppSettingServiceTests_AccountMode()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag überwacht die Anwendung unter dem Benutzerkonto.</summary>
    [Fact]
    public async Task GetScmAccountModeAsync_WithoutEntry_DefaultsToUseraccount()
        => (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.UserAccount);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Benutzerkonto“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmAccountModeAsync_InvalidStoredValue_IfBackToUseraccount(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmAccountModeKey, stored, TestContext.Current.CancellationToken);
        (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmAccountMode.UserAccount);
    }

    /// <summary>Der persistierte Modus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmAccountMode.UserAccount)]
    [InlineData(ScmAccountMode.SeparateAccount)]
    public async Task SetScmAccountModeAsync_PersistedValue_IsHonored(ScmAccountMode mode)
    {
        await _sut.SetScmAccountModeAsync(mode, TestContext.Current.CancellationToken);
        (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(mode);
    }

    /// <summary>Ein nicht definierter Modus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmAccountModeAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmAccountModeAsync((ScmAccountMode)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
}
