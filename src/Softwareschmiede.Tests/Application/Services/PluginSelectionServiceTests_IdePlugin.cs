using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für <see cref="PluginSelectionService.ResolveIdePluginAsync"/> und <see cref="PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync"/> (IDE-Plugin-Auflösung).</summary>
public sealed class PluginSelectionServiceTests_IdePlugin
{
    /// <summary>Ein aktives Plugin mit Explicit-Kompatibilität wird zurückgegeben.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnExplicitPlugin_WhenAvailable()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([visualStudio]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ist das erste Plugin Explicit, gewinnt es gegenüber weiteren Plugins.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnFirstExplicitPlugin_WhenMultipleAvailable()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var pluginManager = CreatePluginManager([visualStudio, vsCode]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ist das erste Plugin nur Fallback und ein späteres Explicit, gewinnt das Explicit-Plugin.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnFallbackPlugin_WhenNoExplicitAvailable()
    {
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([vsCode, visualStudio]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Erste Incompatible, zweite Fallback: Das Fallback-Plugin wird zurückgegeben.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnFallback_WhenFirstIncompatible()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Incompatible);
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var pluginManager = CreatePluginManager([visualStudio, vsCode]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Die Reihenfolge aus dem Setting plugins.ide.order wird bei der Prüfung beachtet.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldRespectPluginOrder_FromSetting()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Fallback);
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var pluginManager = CreatePluginManager([visualStudio, vsCode]);
        var (sut, appEinstellungService) = CreateSut(pluginManager.Object);
        await appEinstellungService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.VisualStudioCode,Softwareschmiede.VisualStudio");

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Sind keine IDE-Plugins aktiv, wird das Default-Plugin des PluginManagers zurückgegeben.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive()
    {
        var defaultPlugin = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([], defaultPlugin);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ist kein Plugin kompatibel, wird das Default-Plugin des PluginManagers zurückgegeben.</summary>
    [Fact]
    public async Task ResolveIdePluginAsync_ShouldReturnDefaultPlugin_WhenNoPluginCompatible()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Incompatible);
        var defaultPlugin = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Incompatible);
        var pluginManager = CreatePluginManager([visualStudio], defaultPlugin);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveIdePluginAsync(@"C:\repos\projekt");

        resolved.PluginPrefix.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Ein Explicit- und ein Fallback-kompatibles Plugin sind beide in der Rückgabeliste enthalten.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnExplicitAndFallbackPlugins_WhenBothCompatible()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var pluginManager = CreatePluginManager([visualStudio, vsCode]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Select(p => p.PluginPrefix).Should().BeEquivalentTo(
            ["Softwareschmiede.VisualStudio", "Softwareschmiede.VisualStudioCode"]);
    }

    /// <summary>Bei gemischter Kompatibilität stehen alle Explicit-Plugins vor allen Fallback-Plugins, unabhängig von der Entdeckungsreihenfolge.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldOrderExplicitPluginsBeforeFallbackPlugins()
    {
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Fallback);
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([vsCode, visualStudio]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Select(p => p.PluginPrefix).Should().ContainInOrder(
            "Softwareschmiede.VisualStudio", "Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Innerhalb der Explicit- bzw. Fallback-Gruppe wird die plugins.ide.order-Reihenfolge eingehalten.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldRespectPluginOrder_FromSetting_WithinEachGroup()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var rider = CreateIdePlugin("Rider", "Softwareschmiede.Rider", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([visualStudio, rider]);
        var (sut, appEinstellungService) = CreateSut(pluginManager.Object);
        await appEinstellungService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.Rider,Softwareschmiede.VisualStudio");

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Select(p => p.PluginPrefix).Should().ContainInOrder(
            "Softwareschmiede.Rider", "Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein Incompatible-Plugin taucht nicht in der Rückgabeliste auf, auch wenn es aktiviert ist.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldExcludeIncompatiblePlugins()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var incompatible = CreateIdePlugin("Incompatible IDE", "Softwareschmiede.Incompatible", IdePluginCompatibility.Incompatible);
        var pluginManager = CreatePluginManager([visualStudio, incompatible]);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Select(p => p.PluginPrefix).Should().BeEquivalentTo(["Softwareschmiede.VisualStudio"]);
    }

    /// <summary>Keine aktivierten Plugins → einelementige Liste mit dem Default-Plugin.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginActive()
    {
        var defaultPlugin = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Explicit);
        var pluginManager = CreatePluginManager([], defaultPlugin);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Should().ContainSingle().Which.PluginPrefix.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Alle aktivierten Plugins Incompatible → einelementige Liste mit dem Default-Plugin.</summary>
    [Fact]
    public async Task ResolveAlleKompatiblenIdePluginsAsync_ShouldReturnDefaultPlugin_WhenNoPluginCompatible()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio", IdePluginCompatibility.Incompatible);
        var defaultPlugin = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode", IdePluginCompatibility.Incompatible);
        var pluginManager = CreatePluginManager([visualStudio], defaultPlugin);
        var (sut, _) = CreateSut(pluginManager.Object);

        var resolved = await sut.ResolveAlleKompatiblenIdePluginsAsync(@"C:\repos\projekt");

        resolved.Should().ContainSingle().Which.PluginPrefix.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>
    /// Erzeugt den SUT und konstruiert dabei <see cref="PluginDefaultSettingsService"/>, <see cref="PluginActivationService"/>
    /// und <see cref="AppEinstellungService"/> aus demselben <see cref="SoftwareschmiededDbContext"/> — wie in der Produktion,
    /// wo sich alle drei Dienste innerhalb eines <see cref="PluginSelectionService"/> denselben Scoped DbContext teilen.
    /// </summary>
    private static (PluginSelectionService Sut, AppEinstellungService AppEinstellungService) CreateSut(IPluginManager pluginManager)
    {
        var db = CreateDb();
        var appEinstellungService = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var activationService = new PluginActivationService(appEinstellungService, pluginManager, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, appEinstellungService);
        return (sut, appEinstellungService);
    }

    private static SoftwareschmiededDbContext CreateDb() => TestDbContextFactory.Create();

    private static Mock<IPluginManager> CreatePluginManager(IReadOnlyList<IIdePlugin> idePlugins, IIdePlugin? defaultIdePlugin = null)
    {
        var effectiveDefault = defaultIdePlugin ?? (idePlugins.Count > 0
            ? idePlugins[0]
            : CreateIdePlugin("Default IDE", "Softwareschmiede.DefaultIde", IdePluginCompatibility.Fallback));

        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetIdePlugins()).Returns(idePlugins);
        pluginManager.Setup(m => m.GetDefaultIdePlugin()).Returns(effectiveDefault);
        return pluginManager;
    }

    private static IIdePlugin CreateIdePlugin(string name, string prefix, IdePluginCompatibility compatibility)
    {
        var plugin = new Mock<IIdePlugin>();
        plugin.SetupGet(p => p.PluginName).Returns(name);
        plugin.SetupGet(p => p.PluginPrefix).Returns(prefix);
        plugin.SetupGet(p => p.PluginType).Returns(PluginType.Ide);
        plugin.Setup(p => p.GetSettingGroups()).Returns([]);
        plugin.Setup(p => p.CheckCompatibilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(compatibility);
        return plugin.Object;
    }
}
