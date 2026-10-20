using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Backmerge-Einstellungen (Modus und Projekt-Freigabe) in <see cref="AppEinstellungService"/>.</summary>
public sealed class AppEinstellungServiceTests_Backmerge : IDisposable
{
    private readonly SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_Backmerge"/>
    public AppEinstellungServiceTests_Backmerge()
        => _sut = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist der Backmerge-Modus deaktiviert.</summary>
    [Fact]
    public async Task GetScmBackmergeModusAsync_WithoutEntry_DefaultsToDeaktiviert()
        => (await _sut.GetScmBackmergeModusAsync()).Should().Be(ScmBackmergeModus.Deaktiviert);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Deaktiviert“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmBackmergeModusAsync_InvalidStoredValue_FallsBackToDeaktiviert(string stored)
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmBackmergeModeKey, stored);
        (await _sut.GetScmBackmergeModusAsync()).Should().Be(ScmBackmergeModus.Deaktiviert);
    }

    /// <summary>Der persistierte Modus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmBackmergeModus.Global)]
    [InlineData(ScmBackmergeModus.Projektbezogen)]
    public async Task SetScmBackmergeModusAsync_PersistedValue_IsHonored(ScmBackmergeModus modus)
    {
        await _sut.SetScmBackmergeModusAsync(modus);
        (await _sut.GetScmBackmergeModusAsync()).Should().Be(modus);
    }

    /// <summary>Ein nicht definierter Modus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmBackmergeModusAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmBackmergeModusAsync((ScmBackmergeModus)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

    /// <summary>Die Projekt-Freigabe ist ohne Eintrag deaktiviert und projektbezogen gespeichert.</summary>
    [Fact]
    public async Task BackmergeProjektFlag_WithoutEntry_DefaultsFalseAndIsPerProject()
    {
        var projektId = Guid.NewGuid();
        (await _sut.GetBackmergeProjektAktivAsync(projektId)).Should().BeFalse();

        await _sut.SetBackmergeProjektAktivAsync(projektId, true);

        (await _sut.GetBackmergeProjektAktivAsync(projektId)).Should().BeTrue();
        (await _sut.GetBackmergeProjektAktivAsync(Guid.NewGuid())).Should().BeFalse("die Freigabe gilt je Projekt");
        (await _sut.GetSettingAsync(AppEinstellungService.BackmergeProjektKey(projektId))).Should().Be("True");
    }
}
