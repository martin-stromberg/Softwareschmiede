using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den SCM-Kontomodus (<see cref="AppEinstellungService.ScmKontoModusKey"/>).</summary>
public sealed class AppEinstellungServiceTests_KontoModus : IDisposable
{
    private readonly SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_KontoModus"/>
    public AppEinstellungServiceTests_KontoModus()
        => _sut = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag überwacht die Anwendung unter dem Benutzerkonto.</summary>
    [Fact]
    public async Task GetScmKontoModusAsync_WithoutEntry_DefaultsToBenutzerkonto()
        => (await _sut.GetScmKontoModusAsync()).Should().Be(ScmKontoModus.Benutzerkonto);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Benutzerkonto“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmKontoModusAsync_InvalidStoredValue_FallsBackToBenutzerkonto(string stored)
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmKontoModusKey, stored);
        (await _sut.GetScmKontoModusAsync()).Should().Be(ScmKontoModus.Benutzerkonto);
    }

    /// <summary>Der persistierte Modus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmKontoModus.Benutzerkonto)]
    [InlineData(ScmKontoModus.SeparatesKonto)]
    public async Task SetScmKontoModusAsync_PersistedValue_IsHonored(ScmKontoModus modus)
    {
        await _sut.SetScmKontoModusAsync(modus);
        (await _sut.GetScmKontoModusAsync()).Should().Be(modus);
    }

    /// <summary>Ein nicht definierter Modus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmKontoModusAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmKontoModusAsync((ScmKontoModus)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
}
