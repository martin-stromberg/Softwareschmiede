using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den Plugin-Aktivierungsstatus (Lesen, Schreiben, Filterung).</summary>
public sealed class PluginActivationServiceTests
{
    /// <summary>Fehlender Eintrag bedeutet, dass das Plugin als aktiviert gilt.</summary>
    [Fact]
    public async Task IsPluginEnabled_LiefertTrue_WennKeinEintragVorhanden()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.Unbekannt");

        // Assert
        enabled.Should().BeTrue();
    }

    /// <summary>Ein gespeicherter Status wird beim erneuten Lesen unverändert zurückgegeben.</summary>
    [Fact]
    public async Task SetPluginEnabled_PersistiertUndLiestZurueck()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        await sut.SetPluginEnabledAsync("Softwareschmiede.GitHub", false);
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.GitHub");

        // Assert
        enabled.Should().BeFalse();
    }

    /// <summary>Deaktivierte SCM-Plugins werden aus der Liste der aktiven Plugins entfernt.</summary>
    [Fact]
    public async Task GetEnabledSourceCodeManagementPlugins_FiltertDeaktivierte()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var aktivesPlugin = CreateGitPlugin("GitHub", "Softwareschmiede.GitHub");
        var deaktiviertesPlugin = CreateGitPlugin("GitLab", "Softwareschmiede.GitLab");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([aktivesPlugin, deaktiviertesPlugin]);
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.GitLab", false);

        // Act
        var aktivePlugins = await sut.GetEnabledSourceCodeManagementPluginsAsync();

        // Assert
        aktivePlugins.Should().ContainSingle();
        aktivePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.GitHub");
    }

    /// <summary>Deaktivierte KI-Plugins werden aus der Liste der aktiven Plugins entfernt.</summary>
    [Fact]
    public async Task GetEnabledDevelopmentAutomationPlugins_FiltertDeaktivierte()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var aktivesPlugin = CreateKiPlugin("Claude", "Softwareschmiede.Claude");
        var deaktiviertesPlugin = CreateKiPlugin("Copilot", "Softwareschmiede.Copilot");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aktivesPlugin, deaktiviertesPlugin]);
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.Copilot", false);

        // Act
        var aktivePlugins = await sut.GetEnabledDevelopmentAutomationPluginsAsync();

        // Assert
        aktivePlugins.Should().ContainSingle();
        aktivePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.Claude");
    }

    private static IGitPlugin CreateGitPlugin(string name, string prefix)
    {
        var plugin = new Mock<IGitPlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(name);
        plugin.SetupGet(p => p.PluginPrefix).Returns(prefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        plugin.Setup(p => p.GetSettingGroups()).Returns([]);
        return plugin.Object;
    }

    private static IKiPlugin CreateKiPlugin(string name, string prefix)
    {
        var plugin = new Mock<IKiPlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(name);
        plugin.SetupGet(p => p.PluginPrefix).Returns(prefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        plugin.Setup(p => p.GetSettingGroups()).Returns([]);
        return plugin.Object;
    }
}
