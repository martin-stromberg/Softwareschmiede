using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die SCM--Einstellungen (Überwachungs-Schalter und Trigger-Keyword) in <see cref="AppSettingService"/>.</summary>
public sealed class AppSettingServiceTests_ScmMonitoring : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppSettingServiceTests_ScmMonitoring"/>
    public AppSettingServiceTests_ScmMonitoring()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist die SCM-Ereignisüberwachung aktiviert.</summary>
    [Fact]
    public async Task GetScmMonitoringEnabledAsync_WithoutEntry_DefaultsToTrue()
        => (await _sut.GetScmMonitoringEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();

    /// <summary>Der persistierte Schalter gilt zur Laufzeit in beide Richtungen.</summary>
    [Fact]
    public async Task GetScmMonitoringEnabledAsync_PersistedValue_IsHonored()
    {
        await _sut.SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);
        (await _sut.GetScmMonitoringEnabledAsync(TestContext.Current.CancellationToken)).Should().BeFalse();

        await _sut.SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, true, TestContext.Current.CancellationToken);
        (await _sut.GetScmMonitoringEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>Ohne gespeicherten Eintrag gilt der Standard-Trigger <c>$Softwareschmiede</c>.</summary>
    [Fact]
    public async Task GetScmTriggerKeywordAsync_WithoutEntry_DefaultsToSoftwareschmiede()
        => (await _sut.GetScmTriggerKeywordAsync(TestContext.Current.CancellationToken)).Should().Be(ScmEventValidationService.DefaultTriggerKeyword);

    /// <summary>Ungültige gespeicherte Werte (leer/Leerraum) fallen auf den Standard zurück.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetScmTriggerKeywordAsync_InvalidStoredValue_IfBackToDefault(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmTriggerKeywordKey, stored, TestContext.Current.CancellationToken);

        (await _sut.GetScmTriggerKeywordAsync(TestContext.Current.CancellationToken)).Should().Be(ScmEventValidationService.DefaultTriggerKeyword);
    }

    /// <summary>Ein gespeichertes Keyword wird getrimmt zurückgegeben.</summary>
    [Fact]
    public async Task GetScmTriggerKeywordAsync_StoredValue_ReturnsTrimmed()
    {
        await _sut.SetSettingAsync(AppSettingService.ScmTriggerKeywordKey, "  !Bearbeite  ", TestContext.Current.CancellationToken);

        (await _sut.GetScmTriggerKeywordAsync(TestContext.Current.CancellationToken)).Should().Be("!Bearbeite");
    }

    /// <summary>Ohne gespeicherten Eintrag gilt das Standard-Verzögerungs-Keyword <c>#verzögert</c>.</summary>
    [Fact]
    public async Task GetScmDelayKeywordAsync_WithoutEntry_DefaultsToDelayed()
        => (await _sut.GetScmDelayKeywordAsync(TestContext.Current.CancellationToken)).Should().Be(ScmDelayDirective.DefaultDelayKeyword);

    /// <summary>Ungültige gespeicherte Werte (leer/Leerraum) fallen auf den Standard zurück.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetScmDelayKeywordAsync_InvalidStoredValue_IfBackToDefault(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmDelayKeywordKey, stored, TestContext.Current.CancellationToken);

        (await _sut.GetScmDelayKeywordAsync(TestContext.Current.CancellationToken)).Should().Be(ScmDelayDirective.DefaultDelayKeyword);
    }

    /// <summary>Ein gespeichertes Verzögerungs-Keyword wird getrimmt zurückgegeben.</summary>
    [Fact]
    public async Task GetScmDelayKeywordAsync_StoredValue_ReturnsTrimmed()
    {
        await _sut.SetSettingAsync(AppSettingService.ScmDelayKeywordKey, "  #warte  ", TestContext.Current.CancellationToken);

        (await _sut.GetScmDelayKeywordAsync(TestContext.Current.CancellationToken)).Should().Be("#warte");
    }

    /// <summary>Ohne gespeicherte Einträge liefert die gebündelte Anhang-Abfrage eine leere Zuordnung.</summary>
    [Fact]
    public async Task GetScmPromptAppendicesAsync_WithoutEntries_ReturnsEmpty()
        => (await _sut.GetScmPromptAppendicesAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();

    /// <summary>Nur tatsächlich gespeicherte Anhang-Schlüssel erscheinen in der gebündelten Abfrage.</summary>
    [Fact]
    public async Task GetScmPromptAppendicesAsync_PersistedValues_AreReturned()
    {
        await _sut.SetSettingAsync(AppSettingService.ScmPromptAppendGeneralKey, "allgemein", TestContext.Current.CancellationToken);
        await _sut.SetSettingAsync(AppSettingService.ScmPromptAppendPullRequestCommentKey, "pr-kommentar\nmehrzeilig", TestContext.Current.CancellationToken);
        await _sut.SetSettingAsync(AppSettingService.ScmTriggerKeywordKey, "!Bearbeite", TestContext.Current.CancellationToken);

        var result = await _sut.GetScmPromptAppendicesAsync(TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result[AppSettingService.ScmPromptAppendGeneralKey].Should().Be("allgemein");
        result[AppSettingService.ScmPromptAppendPullRequestCommentKey].Should().Be("pr-kommentar\nmehrzeilig");
        result.Should().NotContainKey(AppSettingService.ScmPromptAppendIssueKey);
        result.Should().NotContainKey(AppSettingService.ScmTriggerKeywordKey);
    }
}
