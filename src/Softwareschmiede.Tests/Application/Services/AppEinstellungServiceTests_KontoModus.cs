using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den SCM-Kontomodus (<see cref="AppSettingService.ScmAccountModeKey"/>).</summary>
public sealed class AppEinstellungServiceTests_KontoModus : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_KontoModus"/>
    public AppEinstellungServiceTests_KontoModus()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag überwacht die Anwendung unter dem Benutzerkonto.</summary>
    [Fact]
    public async Task GetScmKontoModusAsync_WithoutEntry_DefaultsToBenutzerkonto()
        => (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmKontoModus.Benutzerkonto);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Benutzerkonto“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmKontoModusAsync_InvalidStoredValue_FallsBackToBenutzerkonto(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmAccountModeKey, stored, TestContext.Current.CancellationToken);
        (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmKontoModus.Benutzerkonto);
    }

    /// <summary>Der persistierte Modus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmKontoModus.Benutzerkonto)]
    [InlineData(ScmKontoModus.SeparatesKonto)]
    public async Task SetScmKontoModusAsync_PersistedValue_IsHonored(ScmKontoModus mode)
    {
        await _sut.SetScmAccountModeAsync(mode, TestContext.Current.CancellationToken);
        (await _sut.GetScmAccountModeAsync(TestContext.Current.CancellationToken)).Should().Be(mode);
    }

    /// <summary>Ein nicht definierter Modus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmKontoModusAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmAccountModeAsync((ScmKontoModus)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
}
