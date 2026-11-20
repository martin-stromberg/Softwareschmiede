using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Issue-Aufbereitungs-Einstellungen (Funktions-, Plugin- und
/// Kommentar-Schalter) in <see cref="AppSettingService"/>.</summary>
public sealed class AppEinstellungServiceTests_IssueAufbereitung : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_IssueAufbereitung"/>
    public AppEinstellungServiceTests_IssueAufbereitung()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist die Issue-Aufbereitung deaktiviert (opt-in).</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungEnabledAsync_WithoutEntry_DefaultsToFalse()
        => (await _sut.GetScmIssuePreparationEnabledAsync(TestContext.Current.CancellationToken)).Should().BeFalse();

    /// <summary>Der persistierte Funktionsschalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, true, TestContext.Current.CancellationToken);
        (await _sut.GetScmIssuePreparationEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();

        await _sut.SetBoolSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, false, TestContext.Current.CancellationToken);
        (await _sut.GetScmIssuePreparationEnabledAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>Ohne gespeicherten Eintrag greift der Plugin-Fallback (null).</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_WithoutEntry_ReturnsNull()
        => (await _sut.GetScmIssuePreparationPluginPrefixAsync(TestContext.Current.CancellationToken)).Should().BeNull();

    /// <summary>Leere gespeicherte Plugin-Werte fallen auf den Standard-Fallback zurück.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_EmptyStoredValue_ReturnsNull(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, stored, TestContext.Current.CancellationToken);

        (await _sut.GetScmIssuePreparationPluginPrefixAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Ein gespeicherter Plugin-Prefix wird getrimmt zurückgegeben.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_StoredValue_ReturnsTrimmed()
    {
        await _sut.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "  custom.auf  ", TestContext.Current.CancellationToken);

        (await _sut.GetScmIssuePreparationPluginPrefixAsync(TestContext.Current.CancellationToken)).Should().Be("custom.auf");
    }

    /// <summary>Ohne gespeicherten Eintrag ist der Analyse-Kommentar aktiviert.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungKommentarEnabledAsync_WithoutEntry_DefaultsToTrue()
        => (await _sut.GetScmIssuePreparationCommentEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();

    /// <summary>Der persistierte Kommentar-Schalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungKommentarEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppSettingService.ScmIssuePreparationCommentEnabledKey, false, TestContext.Current.CancellationToken);
        (await _sut.GetScmIssuePreparationCommentEnabledAsync(TestContext.Current.CancellationToken)).Should().BeFalse();

        await _sut.SetBoolSettingAsync(AppSettingService.ScmIssuePreparationCommentEnabledKey, true, TestContext.Current.CancellationToken);
        (await _sut.GetScmIssuePreparationCommentEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
