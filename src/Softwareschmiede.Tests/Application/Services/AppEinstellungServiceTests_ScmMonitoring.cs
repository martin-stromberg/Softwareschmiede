using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die SCM-Einstellungen (Überwachungs-Schalter und Trigger-Keyword) in <see cref="AppEinstellungService"/>.</summary>
public sealed class AppEinstellungServiceTests_ScmMonitoring : IDisposable
{
    private readonly SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _sut;

    /// <inheritdoc cref="AppEinstellungServiceTests_ScmMonitoring"/>
    public AppEinstellungServiceTests_ScmMonitoring()
        => _sut = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist die SCM-Ereignisüberwachung aktiviert.</summary>
    [Fact]
    public async Task GetScmMonitoringEnabledAsync_WithoutEntry_DefaultsToTrue()
        => (await _sut.GetScmMonitoringEnabledAsync()).Should().BeTrue();

    /// <summary>Der persistierte Schalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmMonitoringEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false);
        (await _sut.GetScmMonitoringEnabledAsync()).Should().BeFalse();

        await _sut.SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, true);
        (await _sut.GetScmMonitoringEnabledAsync()).Should().BeTrue();
    }

    /// <summary>Ohne gespeicherten Eintrag gilt der Standard-Trigger <c>$Softwareschmiede</c>.</summary>
    [Fact]
    public async Task GetScmTriggerKeywordAsync_WithoutEntry_DefaultsToSoftwareschmiede()
        => (await _sut.GetScmTriggerKeywordAsync()).Should().Be(ScmEventValidationService.DefaultTriggerKeyword);

    /// <summary>Ungültige gespeicherte Werte (leer/Leerraum) fallen auf den Standard zurück.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetScmTriggerKeywordAsync_InvalidStoredValue_FallsBackToDefault(string stored)
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmTriggerKeywordKey, stored);

        (await _sut.GetScmTriggerKeywordAsync()).Should().Be(ScmEventValidationService.DefaultTriggerKeyword);
    }

    /// <summary>Ein gespeichertes Keyword wird getrimmt zurückgegeben.</summary>
    [Fact]
    public async Task GetScmTriggerKeywordAsync_StoredValue_ReturnsTrimmed()
    {
        await _sut.SetSettingAsync(AppEinstellungService.ScmTriggerKeywordKey, "  !Bearbeite  ");

        (await _sut.GetScmTriggerKeywordAsync()).Should().Be("!Bearbeite");
    }
}
