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

    /// <summary>Aktivierte IDE-Plugins werden zurückgegeben, deaktivierte herausgefiltert.</summary>
    [Fact]
    public async Task GetEnabledIdePlugins_FiltertDeaktivierte()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var aktivesPlugin = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var deaktiviertesPlugin = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetIdePlugins()).Returns([aktivesPlugin, deaktiviertesPlugin]);
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudioCode", false);

        // Act
        var aktivePlugins = await sut.GetEnabledIdePluginsAsync();

        // Assert
        aktivePlugins.Should().ContainSingle();
        aktivePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein neues IDE-Plugin ohne gespeicherten Eintrag gilt standardmäßig als aktiviert.</summary>
    [Fact]
    public async Task IsPluginEnabledAsync_ShouldReturnTrueByDefault_ForIdePlugin()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.VisualStudio");

        // Assert
        enabled.Should().BeTrue();
    }

    /// <summary>Der Aktivierungsstatus eines IDE-Plugins wird persistiert und beim erneuten Lesen zurückgegeben.</summary>
    [Fact]
    public async Task SetPluginEnabledAsync_ShouldPersistIdePluginActivation()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false);
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.VisualStudio");

        // Assert
        enabled.Should().BeFalse();
    }

    /// <summary>Sind alle IDE-Plugins deaktiviert, liefert GetEnabledIdePluginsAsync eine leere Liste.</summary>
    [Fact]
    public async Task GetEnabledIdePluginsAsync_ShouldReturnEmpty_WhenAllDisabled()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var plugin = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetIdePlugins()).Returns([plugin]);
        var sut = new PluginActivationService(new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false);

        // Act
        var aktivePlugins = await sut.GetEnabledIdePluginsAsync();

        // Assert
        aktivePlugins.Should().BeEmpty();
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

    private static IIdePlugin CreateIdePlugin(string name, string prefix)
    {
        var plugin = new Mock<IIdePlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(name);
        plugin.SetupGet(p => p.PluginPrefix).Returns(prefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.Ide);
        plugin.Setup(p => p.GetSettingGroups()).Returns([]);
        return plugin.Object;
    }
}
