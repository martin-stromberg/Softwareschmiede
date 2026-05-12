using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für persistierte Standard-Plugin-Einstellungen.</summary>
public sealed class PluginDefaultSettingsServiceTests
{
    /// <summary>Speichert einen neuen Wert je Plugin-Typ.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldCreateSetting_WhenMissing()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GitHubCopilot");

        // Assert
        var entity = await db.AppEinstellungen.SingleAsync(e => e.Schluessel == "plugins.default.DevelopmentAutomation");
        entity.Wert.Should().Be("Softwareschmiede.GitHubCopilot");
    }

    /// <summary>Normalisiert gespeicherte Werte mit Leerzeichen.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldTrimStoredValue_WhenValueContainsWhitespace()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "  Softwareschmiede.GitHub  ");
        var value = await sut.GetDefaultPluginPrefixAsync(PluginType.SourceCodeManagement);

        // Assert
        value.Should().Be("Softwareschmiede.GitHub");
    }

    /// <summary>Aktualisiert bestehende Einträge statt Duplikate anzulegen.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldUpdateExistingSetting_WhenCalledAgain()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Old");

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.New");

        // Assert
        db.AppEinstellungen.Count(e => e.Schluessel == "plugins.default.DevelopmentAutomation").Should().Be(1);
        var value = await sut.GetDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation);
        value.Should().Be("Softwareschmiede.New");
    }

    /// <summary>Persistiert null, wenn Auswahl geleert wurde.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldPersistNull_WhenSelectionIsWhitespace()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.GitHub");

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "   ");

        // Assert
        var entity = await db.AppEinstellungen.SingleAsync(e => e.Schluessel == "plugins.default.SourceCodeManagement");
        entity.Wert.Should().BeNull();
    }
}
