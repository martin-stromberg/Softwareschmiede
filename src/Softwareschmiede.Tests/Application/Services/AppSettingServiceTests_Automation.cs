using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die neuen Automatisierungs--Einstellungen in <see cref="AppSettingService"/>:
/// Prüfmodus der Abschlussüberwachung sowie die Backmerge-Optionen Draft-Aktivierung und Admin-Merge.</summary>
public sealed class AppSettingServiceTests_Automation : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppSettingServiceTests_Automation"/>
    public AppSettingServiceTests_Automation()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag gilt der ereignisgesteuerte Prüfmodus.</summary>
    [Fact]
    public async Task GetScmTaskCompletionCheckmodeAsync_WithoutEntry_DefaultsToEventdriven()
        => (await _sut.GetScmTaskCompletionCheckModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.EventDriven);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Ereignisgesteuert“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmTaskCompletionCheckmodeAsync_InvalidStoredValue_IfBackToEventdriven(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmTaskCompletionCheckModeKey, stored, TestContext.Current.CancellationToken);
        (await _sut.GetScmTaskCompletionCheckModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmTaskCompletionCheckMode.EventDriven);
    }

    /// <summary>Der persistierte Prüfmodus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmTaskCompletionCheckMode.EventDriven)]
    [InlineData(ScmTaskCompletionCheckMode.AllWorkItems)]
    public async Task SetScmTaskCompletionCheckmodeAsync_PersistedValue_IsHonored(ScmTaskCompletionCheckMode mode)
    {
        await _sut.SetScmTaskCompletionCheckModeAsync(mode, TestContext.Current.CancellationToken);
        (await _sut.GetScmTaskCompletionCheckModeAsync(TestContext.Current.CancellationToken)).Should().Be(mode);
    }

    /// <summary>Ein nicht definierter Prüfmodus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmTaskCompletionCheckmodeAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmTaskCompletionCheckModeAsync((ScmTaskCompletionCheckMode)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

    /// <summary>Beide Backmerge-Optionen sind ohne Eintrag deaktiviert und werden getrennt persistiert.</summary>
    [Fact]
    public async Task BackmergeOptions_WithoutEntry_DefaultFalseAndPersistIndependently()
    {
        (await _sut.GetScmBackmergeDraftActivationAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeFalse();

        await _sut.SetScmBackmergeDraftActivationAsync(true, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeDraftActivationAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeFalse("die Optionen sind unabhängig voneinander");
        (await _sut.GetSettingAsync(AppSettingService.ScmBackmergeDraftActivationKey, TestContext.Current.CancellationToken)).Should().Be("True");

        await _sut.SetScmBackmergeAdminMergeAsync(true, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _sut.GetSettingAsync(AppSettingService.ScmBackmergeAdminMergeKey, TestContext.Current.CancellationToken)).Should().Be("True");

        await _sut.SetScmBackmergeDraftActivationAsync(false, TestContext.Current.CancellationToken);

        (await _sut.GetScmBackmergeDraftActivationAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await _sut.GetScmBackmergeAdminMergeAsync(TestContext.Current.CancellationToken)).Should().BeTrue("das Zurücksetzen der einen Option lässt die andere unberührt");
    }
}
