using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für persistierte Standard-Plugin--Einstellungen.</summary>
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
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GitHubCopilot", TestContext.Current.CancellationToken);

        // Assert
        var entity = await db.AppSettings.SingleAsync(e => e.Key == "plugins.default.DevelopmentAutomation", cancellationToken: TestContext.Current.CancellationToken);
        entity.Value.Should().Be("Softwareschmiede.GitHubCopilot");
    }

    /// <summary>Normalisiert gespeicherte Werte mit Leerzeichen.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldTrimStoredValue_WhenValueContainsWhitespace()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "  Softwareschmiede.GitHub  ", TestContext.Current.CancellationToken);
        var value = await sut.GetDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, TestContext.Current.CancellationToken);

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
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Old", TestContext.Current.CancellationToken);

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.New", TestContext.Current.CancellationToken);

        // Assert
        db.AppSettings.Count(e => e.Key == "plugins.default.DevelopmentAutomation").Should().Be(1);
        var value = await sut.GetDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, TestContext.Current.CancellationToken);
        value.Should().Be("Softwareschmiede.New");
    }

    /// <summary>Persistiert null, wenn Auswahl geleert wurde.</summary>
    [Fact]
    public async Task SaveDefaultPluginPrefixAsync_ShouldPersistNull_WhenSelectionIsWhitespace()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.GitHub", TestContext.Current.CancellationToken);

        // Act
        await sut.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "   ", TestContext.Current.CancellationToken);

        // Assert
        var entity = await db.AppSettings.SingleAsync(e => e.Key == "plugins.default.SourceCodeManagement", cancellationToken: TestContext.Current.CancellationToken);
        entity.Value.Should().BeNull();
    }

    /// <summary>Gespeicherter Projekt-Default wird korrekt abgerufen.</summary>
    [Fact]
    public async Task TestGetProjectDefaultPluginPrefix_ReturnsStoredValue()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await sut.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);

        // Act
        var value = await sut.GetProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, TestContext.Current.CancellationToken);

        // Assert
        value.Should().Be("Softwareschmiede.ProjectKi");
    }

    /// <summary>Falls kein Projekt-Default gespeichert ist, wird null zurückgegeben.</summary>
    [Fact]
    public async Task TestGetProjectDefaultPluginPrefix_NoProjectDefault_ReturnsNull()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);

        // Act
        var value = await sut.GetProjectDefaultPluginPrefixAsync(Guid.NewGuid(), PluginType.DevelopmentAutomation, TestContext.Current.CancellationToken);

        // Assert
        value.Should().BeNull();
    }

    /// <summary>Projekt-Default wird mit scoped Key in AppEinstellung gespeichert.</summary>
    [Fact]
    public async Task TestSaveProjectDefaultPluginPrefix_StoresInAppEinstellung()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var sut = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();

        // Act
        await sut.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);

        // Assert
        var entity = await db.AppSettings.SingleAsync(e => e.Key == $"plugins.default.project.{projectId}.DevelopmentAutomation", cancellationToken: TestContext.Current.CancellationToken);
        entity.Value.Should().Be("Softwareschmiede.ProjectKi");
    }
}
