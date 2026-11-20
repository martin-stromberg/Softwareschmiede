using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die neuen Automatisierungs-Einstellungen in <see cref="AppEinstellungService"/>:
/// Prüfmodus der Abschlussüberwachung sowie die Backmerge-Optionen Draft-Aktivierung und Admin-Merge.</summary>
public sealed class AppEinstellungServiceTests_Automatisierung : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_Automatisierung"/>
    public AppEinstellungServiceTests_Automatisierung()
        => _sut = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag gilt der ereignisgesteuerte Prüfmodus.</summary>
    [Fact]
    public async Task GetScmTaskCompletionPruefmodusAsync_WithoutEntry_DefaultsToEreignisgesteuert()
        => (await _sut.GetScmTaskCompletionPruefmodusAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.EventDriven);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Ereignisgesteuert“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmTaskCompletionPruefmodusAsync_InvalidStoredValue_FallsBackToEreignisgesteuert(string stored)
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmTaskCompletionPruefmodusKey, stored, TestContext.Current.CancellationToken);
        (await _sut.GetScmTaskCompletionPruefmodusAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.EventDriven);
    }

    /// <summary>Der persistierte Prüfmodus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmTaskCompletionCheckMode.EventDriven)]
    [InlineData(ScmTaskCompletionCheckMode.AllWorkItems)]
    public async Task SetScmTaskCompletionPruefmodusAsync_PersistedValue_IsHonored(ScmTaskCompletionCheckMode modus)
    {
        await _sut.SetScmTaskCompletionPruefmodusAsync(modus, TestContext.Current.CancellationToken);
        (await _sut.GetScmTaskCompletionPruefmodusAsync(TestContext.Current.CancellationToken)).Should().Be(modus);
    }

    /// <summary>Ein nicht definierter Prüfmodus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmTaskCompletionPruefmodusAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmTaskCompletionPruefmodusAsync((ScmTaskCompletionCheckMode)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

    /// <summary>Beide Backmerge-Optionen sind ohne Eintrag deaktiviert und werden getrennt persistiert.</summary>
    [Fact]
    public async Task BackmergeOptionen_WithoutEntry_DefaultFalseAndPersistIndependently()
    {
        (await _sut.GetScmBackmergeDraftAktivierungAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeFalse();

        await _sut.SetScmBackmergeDraftAktivierungAsync(true, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeDraftAktivierungAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeFalse("die Optionen sind unabhängig voneinander");
        (await _sut.GetSettingAsync(AppEinstellungService.ScmBackmergeDraftAktivierungKey, TestContext.Current.CancellationToken)).Should().Be("True");

        await _sut.SetScmBackmergeAdminMergeAsync(true, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _sut.GetSettingAsync(AppEinstellungService.ScmBackmergeAdminMergeKey, TestContext.Current.CancellationToken)).Should().Be("True");

        await _sut.SetScmBackmergeDraftAktivierungAsync(false, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeDraftAktivierungAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeTrue("das Zurücksetzen der einen Option lässt die andere unberührt");
    }
}
