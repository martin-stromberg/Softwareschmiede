using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Backmerge--Einstellungen (Modus und Projekt-Freigabe) in <see cref="AppSettingService"/>.</summary>
public sealed class AppSettingServiceTests_Backmerge : IDisposable
{
    private readonly SoftwareschmiedeDbContext _db = TestDbContextFactory.Create();
    private readonly AppSettingService _sut;

    /// <inheritdoc cref="AppSettingServiceTests_Backmerge"/>
    public AppSettingServiceTests_Backmerge()
        => _sut = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

    /// <summary>Räumt den InMemory-Datenbankkontext auf.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherten Eintrag ist der Backmerge-Modus deaktiviert.</summary>
    [Fact]
    public async Task GetScmBackmergeModeAsync_WithoutEntry_DefaultsToDisabled()
        => (await _sut.GetScmBackmergeModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmBackmergeMode.Disabled);

    /// <summary>Ungültige gespeicherte Werte fallen auf „Deaktiviert“ zurück.</summary>
    [Theory]
    [InlineData("kein-wert")]
    [InlineData("99")]
    public async Task GetScmBackmergeModeAsync_InvalidStoredValue_IfBackToDisabled(string stored)
    {
        await _sut.SetSettingAsync(AppSettingService.ScmBackmergeModeKey, stored, TestContext.Current.CancellationToken);
        (await _sut.GetScmBackmergeModeAsync(TestContext.Current.CancellationToken)).Should().Be(ScmBackmergeMode.Disabled);
    }

    /// <summary>Der persistierte Modus gilt zur Laufzeit.</summary>
    [Theory]
    [InlineData(ScmBackmergeMode.Global)]
    [InlineData(ScmBackmergeMode.ProjectScoped)]
    public async Task SetScmBackmergeModeAsync_PersistedValue_IsHonored(ScmBackmergeMode mode)
    {
        await _sut.SetScmBackmergeModeAsync(mode, TestContext.Current.CancellationToken);
        (await _sut.GetScmBackmergeModeAsync(TestContext.Current.CancellationToken)).Should().Be(mode);
    }

    /// <summary>Ein nicht definierter Modus wird abgelehnt.</summary>
    [Fact]
    public async Task SetScmBackmergeModeAsync_UndefinedValue_Throws()
        => await FluentActions.Invoking(() => _sut.SetScmBackmergeModeAsync((ScmBackmergeMode)42))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

    /// <summary>Die Projekt-Freigabe ist ohne Eintrag deaktiviert und projektbezogen gespeichert.</summary>
    [Fact]
    public async Task BackmergeProjectFlag_WithoutEntry_DefaultsFalseAndIsPerProject()
    {
        var projectId = Guid.NewGuid();
        (await _sut.GetBackmergeProjectEnabledAsync(projectId, TestContext.Current.CancellationToken)).Should().BeFalse();

        await _sut.SetBackmergeProjectEnabledAsync(projectId, true, TestContext.Current.CancellationToken);

        (await _sut.GetBackmergeProjectEnabledAsync(projectId, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _sut.GetBackmergeProjectEnabledAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeFalse("die Freigabe gilt je Projekt");
        (await _sut.GetSettingAsync(AppSettingService.BackmergeProjectKey(projectId), TestContext.Current.CancellationToken)).Should().Be("True");
    }
}
