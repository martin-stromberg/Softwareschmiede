using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für die Plugin-Auflösung (explizit → Default → Fallback).</summary>
public sealed class PluginSelectionServiceTests
{
    /// <summary>Verwendet explizit ausgewähltes Plugin mit höchster Priorität.</summary>
    [Fact]
    public async Task ResolveDevelopmentAutomationPluginAsync_ShouldUseExplicitSelection_WhenProvided()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Stored");

        var selected = CreateKiPlugin("Selected", "Softwareschmiede.Selected");
        var stored = CreateKiPlugin("Stored", "Softwareschmiede.Stored");
        var pluginManager = CreatePluginManager([selected, stored]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync("Softwareschmiede.Selected");

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.Selected");
    }

    /// <summary>Fällt auf gespeicherten Standard zurück, wenn keine explizite Auswahl vorliegt.</summary>
    [Fact]
    public async Task ResolveDevelopmentAutomationPluginAsync_ShouldUseStoredDefault_WhenNoExplicitSelection()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Stored");

        var first = CreateKiPlugin("First", "Softwareschmiede.First");
        var stored = CreateKiPlugin("Stored", "Softwareschmiede.Stored");
        var pluginManager = CreatePluginManager([first, stored]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.Stored");
    }

    /// <summary>Bevorzugt Copilot im Fallback, wenn kein explizites/gespeichertes Plugin verfügbar ist.</summary>
    [Fact]
    public async Task ResolveDevelopmentAutomationPluginAsync_ShouldPreferCopilotProviderInFallback()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Missing");

        var claude = new TestCliKiPlugin("Claude", "Softwareschmiede.Claude", "claude");
        var copilot = new TestCliKiPlugin("Copilot", "Softwareschmiede.Copilot", "copilot");
        var pluginManager = CreatePluginManager([claude, copilot]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.Copilot");
    }

    /// <summary>Verwendet Default-Resolver, wenn keine KI-Plugins zur Auswahl stehen.</summary>
    [Fact]
    public async Task ResolveDevelopmentAutomationPluginAsync_ShouldUseDefaultResolver_WhenAvailableListIsEmpty()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginManager = CreatePluginManager([]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.DefaultKi");
    }

    /// <summary>Verwendet Default-Resolver, wenn keine Plugins zur Auswahl stehen.</summary>
    [Fact]
    public async Task ResolveSourceCodeManagementPluginAsync_ShouldUseDefaultResolver_WhenAvailableListIsEmpty()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var defaultGit = CreateGitPlugin("Default", "Softwareschmiede.Default");
        var pluginManager = CreatePluginManager([], [defaultGit]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync(null);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.Default");
    }

    /// <summary>Verwendet explizit ausgewähltes SCM-Plugin mit höchster Priorität.</summary>
    [Fact]
    public async Task ResolveSourceCodeManagementPluginAsync_ShouldUseExplicitSelection_WhenProvided()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.StoredGit");

        var explicitPlugin = CreateGitPlugin("Explicit Git", "Softwareschmiede.ExplicitGit");
        var storedPlugin = CreateGitPlugin("Stored Git", "Softwareschmiede.StoredGit");
        var pluginManager = CreatePluginManager([], [explicitPlugin, storedPlugin]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync("Softwareschmiede.ExplicitGit");

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.ExplicitGit");
    }

    /// <summary>Fällt für SCM auf gespeicherten Standard zurück, wenn keine explizite Auswahl vorliegt.</summary>
    [Fact]
    public async Task ResolveSourceCodeManagementPluginAsync_ShouldUseStoredDefault_WhenNoExplicitSelection()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.StoredGit");

        var firstPlugin = CreateGitPlugin("First Git", "Softwareschmiede.FirstGit");
        var storedPlugin = CreateGitPlugin("Stored Git", "Softwareschmiede.StoredGit");
        var pluginManager = CreatePluginManager([], [firstPlugin, storedPlugin]);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync(null);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.StoredGit");
    }

    private static Mock<IPluginManager> CreatePluginManager(
        IReadOnlyList<IKiPlugin> kiPlugins,
        IReadOnlyList<IGitPlugin>? gitPlugins = null)
    {
        var effectiveGitPlugins = gitPlugins ?? [CreateGitPlugin("Git", "Softwareschmiede.Git")];
        var gitDefault = effectiveGitPlugins.First();
        var kiDefault = kiPlugins.Count > 0 ? kiPlugins.First() : CreateKiPlugin("Default KI", "Softwareschmiede.DefaultKi");

        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetSourceCodeManagementPlugins()).Returns(effectiveGitPlugins);
        pluginManager.Setup(m => m.GetDefaultSourceCodeManagementPlugin()).Returns(gitDefault);
        pluginManager.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns(kiPlugins);
        pluginManager.Setup(m => m.GetDefaultDevelopmentAutomationPlugin()).Returns(kiDefault);
        return pluginManager;
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

    private sealed class TestCliKiPlugin(string name, string prefix, string providerPrefix) : CliKiPluginBase
    {
        public override string ProviderDateiPraefix => providerPrefix;
        public override string PluginName => name;
        public override string PluginPrefix => prefix;
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<AgentInfo>());
        public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default)
            => Task.FromResult(true);
        public override Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default)
            => Task.CompletedTask;
        public override IAsyncEnumerable<string> StartDevelopmentAsync(string prompt, AgentInfo agent, string localRepoPath, string? model = null, CancellationToken ct = default)
            => AsyncEnumerable.Empty<string>();
        public override Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default)
            => Task.FromResult(new TestResult(true, []));
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
