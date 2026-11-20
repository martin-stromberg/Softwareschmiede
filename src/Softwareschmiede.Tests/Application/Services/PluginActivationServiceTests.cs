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
    public async Task IsPluginEnabled_ReturnsTrue_WhenNoEntryExisting()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.Unbekannt", TestContext.Current.CancellationToken);

        // Assert
        enabled.Should().BeTrue();
    }

    /// <summary>Ein gespeicherter Status wird beim erneuten Lesen unverändert zurückgegeben.</summary>
    [Fact]
    public async Task SetPluginEnabled_PersistedAndReadsBack()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        await sut.SetPluginEnabledAsync("Softwareschmiede.GitHub", false, TestContext.Current.CancellationToken);
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.GitHub", TestContext.Current.CancellationToken);

        // Assert
        enabled.Should().BeFalse();
    }

    /// <summary>Deaktivierte SCM-Plugins werden aus der Liste der aktiven Plugins entfernt.</summary>
    [Fact]
    public async Task GetEnabledSourceCodeManagementPlugins_FiltersDisabled()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var activePlugin = CreateGitPlugin("GitHub", "Softwareschmiede.GitHub");
        var disabledPlugin = CreateGitPlugin("GitLab", "Softwareschmiede.GitLab");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([activePlugin, disabledPlugin]);
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.GitLab", false, TestContext.Current.CancellationToken);

        // Act
        var activePlugins = await sut.GetEnabledSourceCodeManagementPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        activePlugins.Should().ContainSingle();
        activePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.GitHub");
    }

    /// <summary>Deaktivierte KI-Plugins werden aus der Liste der aktiven Plugins entfernt.</summary>
    [Fact]
    public async Task GetEnabledDevelopmentAutomationPlugins_FiltersDisabled()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var activePlugin = CreateAiPlugin("Claude", "Softwareschmiede.Claude");
        var disabledPlugin = CreateAiPlugin("Copilot", "Softwareschmiede.Copilot");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([activePlugin, disabledPlugin]);
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.Copilot", false, TestContext.Current.CancellationToken);

        // Act
        var activePlugins = await sut.GetEnabledDevelopmentAutomationPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        activePlugins.Should().ContainSingle();
        activePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.Claude");
    }

    /// <summary>Aktivierte IDE-Plugins werden zurückgegeben, deaktivierte herausgefiltert.</summary>
    [Fact]
    public async Task GetEnabledIdePlugins_FiltersDisabled()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var activePlugin = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var disabledPlugin = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetIdePlugins()).Returns([activePlugin, disabledPlugin]);
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudioCode", false, TestContext.Current.CancellationToken);

        // Act
        var activePlugins = await sut.GetEnabledIdePluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        activePlugins.Should().ContainSingle();
        activePlugins[0].PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein neues IDE-Plugin ohne gespeicherten Eintrag gilt standardmäßig als aktiviert.</summary>
    [Fact]
    public async Task IsPluginEnabledAsync_ShouldReturnTrueByDefault_ForIdePlugin()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var pluginManager = new Mock<IPluginManager>();
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.VisualStudio", TestContext.Current.CancellationToken);

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
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);

        // Act
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false, TestContext.Current.CancellationToken);
        var enabled = await sut.IsPluginEnabledAsync("Softwareschmiede.VisualStudio", TestContext.Current.CancellationToken);

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
        var sut = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await sut.SetPluginEnabledAsync("Softwareschmiede.VisualStudio", false, TestContext.Current.CancellationToken);

        // Act
        var activePlugins = await sut.GetEnabledIdePluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        activePlugins.Should().BeEmpty();
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

    private static IAiPlugin CreateAiPlugin(string name, string prefix)
    {
        var plugin = new Mock<IAiPlugin>();
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
