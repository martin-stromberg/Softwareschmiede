using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Issue-Aufbereitungs-Einstellungen (Funktions-, Plugin- und
/// Kommentar-Schalter) in <see cref="AppEinstellungService"/>.</summary>
public sealed class AppEinstellungServiceTests_IssueAufbereitung : IDisposable
{
    private readonly SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_IssueAufbereitung"/>
    public AppEinstellungServiceTests_IssueAufbereitung()
        => _sut = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist die Issue-Aufbereitung deaktiviert (opt-in).</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungEnabledAsync_WithoutEntry_DefaultsToFalse()
        => (await _sut.GetScmIssueAufbereitungEnabledAsync()).Should().BeFalse();

    /// <summary>Der persistierte Funktionsschalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmIssueAufbereitungEnabledKey, true);
        (await _sut.GetScmIssueAufbereitungEnabledAsync()).Should().BeTrue();

        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmIssueAufbereitungEnabledKey, false);
        (await _sut.GetScmIssueAufbereitungEnabledAsync()).Should().BeFalse();
    }

    /// <summary>Ohne gespeicherten Eintrag greift der Plugin-Fallback (null).</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_WithoutEntry_ReturnsNull()
        => (await _sut.GetScmIssueAufbereitungPluginPrefixAsync()).Should().BeNull();

    /// <summary>Leere gespeicherte Plugin-Werte fallen auf den Standard-Fallback zurück.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_EmptyStoredValue_ReturnsNull(string stored)
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungPluginKey, stored);

        (await _sut.GetScmIssueAufbereitungPluginPrefixAsync()).Should().BeNull();
    }

    /// <summary>Ein gespeicherter Plugin-Prefix wird getrimmt zurückgegeben.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungPluginPrefixAsync_StoredValue_ReturnsTrimmed()
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungPluginKey, "  custom.auf  ");

        (await _sut.GetScmIssueAufbereitungPluginPrefixAsync()).Should().Be("custom.auf");
    }

    /// <summary>Ohne gespeicherten Eintrag ist der Analyse-Kommentar aktiviert.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungKommentarEnabledAsync_WithoutEntry_DefaultsToTrue()
        => (await _sut.GetScmIssueAufbereitungKommentarEnabledAsync()).Should().BeTrue();

    /// <summary>Der persistierte Kommentar-Schalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmIssueAufbereitungKommentarEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmIssueAufbereitungKommentarEnabledKey, false);
        (await _sut.GetScmIssueAufbereitungKommentarEnabledAsync()).Should().BeFalse();

        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmIssueAufbereitungKommentarEnabledKey, true);
        (await _sut.GetScmIssueAufbereitungKommentarEnabledAsync()).Should().BeTrue();
    }
}
