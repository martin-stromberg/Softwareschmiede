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
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Stored", TestContext.Current.CancellationToken);

        var selected = CreateKiPlugin("Selected", "Softwareschmiede.Selected");
        var stored = CreateKiPlugin("Stored", "Softwareschmiede.Stored");
        var pluginManager = CreatePluginManager([selected, stored]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync("Softwareschmiede.Selected", TestContext.Current.CancellationToken);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.Selected");
    }

    /// <summary>Gibt nur die Prefixe aktiver KI-Plugins zurück.</summary>
    [Fact]
    public async Task GetAvailableKiPluginPrefixesAsync_ShouldReturnOnlyActivePlugins()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var active = CreateKiPlugin("Aktiv", "Softwareschmiede.Aktiv");
        var deaktiviert = CreateKiPlugin("Deaktiviert", "Softwareschmiede.Deaktiviert");
        var pluginManager = CreatePluginManager([active, deaktiviert]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("Softwareschmiede.Deaktiviert", false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var prefixe = await sut.GetAvailableKiPluginPrefixesAsync(TestContext.Current.CancellationToken);

        // Assert
        prefixe.Should().ContainSingle().Which.Should().Be("Softwareschmiede.Aktiv");
    }

    /// <summary>Fällt auf gespeicherten Standard zurück, wenn keine explizite Auswahl vorliegt.</summary>
    [Fact]
    public async Task ResolveDevelopmentAutomationPluginAsync_ShouldUseStoredDefault_WhenNoExplicitSelection()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Stored", TestContext.Current.CancellationToken);

        var first = CreateKiPlugin("First", "Softwareschmiede.First");
        var stored = CreateKiPlugin("Stored", "Softwareschmiede.Stored");
        var pluginManager = CreatePluginManager([first, stored]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null, TestContext.Current.CancellationToken);

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
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Missing", TestContext.Current.CancellationToken);

        var claude = new TestCliKiPlugin("Claude", "Softwareschmiede.Claude", "claude");
        var copilot = new TestCliKiPlugin("Copilot", "Softwareschmiede.Copilot", "copilot");
        var pluginManager = CreatePluginManager([claude, copilot]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null, TestContext.Current.CancellationToken);

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
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginAsync(null, TestContext.Current.CancellationToken);

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
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync(null, TestContext.Current.CancellationToken);

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
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.StoredGit", TestContext.Current.CancellationToken);

        var explicitPlugin = CreateGitPlugin("Explicit Git", "Softwareschmiede.ExplicitGit");
        var storedPlugin = CreateGitPlugin("Stored Git", "Softwareschmiede.StoredGit");
        var pluginManager = CreatePluginManager([], [explicitPlugin, storedPlugin]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync("Softwareschmiede.ExplicitGit", TestContext.Current.CancellationToken);

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
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.SourceCodeManagement, "Softwareschmiede.StoredGit", TestContext.Current.CancellationToken);

        var firstPlugin = CreateGitPlugin("First Git", "Softwareschmiede.FirstGit");
        var storedPlugin = CreateGitPlugin("Stored Git", "Softwareschmiede.StoredGit");
        var pluginManager = CreatePluginManager([], [firstPlugin, storedPlugin]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveSourceCodeManagementPluginAsync(null, TestContext.Current.CancellationToken);

        // Assert
        resolved.PluginPrefix.Should().Be("Softwareschmiede.StoredGit");
    }

    /// <summary>ResolveDevelopmentAutomationPluginWithProjectScopeAsync nutzt Projekt-Default wenn vorhanden.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_UsesProjectDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);

        var pluginManager = CreatePluginManager([]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, projectId, TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().Be("Softwareschmiede.ProjectKi");
    }

    /// <summary>ResolveDevelopmentAutomationPluginWithProjectScopeAsync löst die in den Einstellungen
    /// konfigurierte Standard-CLI (ki.plugin.default, gespeicherter PluginName) als letzte Stufe auf.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_UsesConfiguredSettingsDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().Be("Softwareschmiede.KiSimulator");
    }

    /// <summary>Die Aufgaben-Auswahl hat Vorrang vor der in den Einstellungen konfigurierten Standard-CLI.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_TaskPluginBeatsSettingsDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync("Softwareschmiede.TaskCli", Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().Be("Softwareschmiede.TaskCli");
    }

    /// <summary>Der Projekt-Default hat Vorrang vor der in den Einstellungen konfigurierten Standard-CLI.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_ProjectDefaultBeatsSettingsDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, projectId, TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().Be("Softwareschmiede.ProjectKi");
    }

    /// <summary>Der gespeicherte Global-Default (plugins.default.*) hat Vorrang vor der
    /// Einstellungen-Standard-CLI (ki.plugin.default).</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_StoredGlobalDefaultBeatsSettingsDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.Stored", TestContext.Current.CancellationToken);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().Be("Softwareschmiede.Stored");
    }

    /// <summary>Ein auf kein vorhandenes KI-Plugin passender Einstellungen-Default wird nicht geraten:
    /// Die Auflösung gibt null zurück und der Aufrufer zeigt weiterhin die Auswahl/Warteanzeige.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_UnknownSettingsDefaultReturnsNull()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Nicht installiert", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().BeNull();
    }

    /// <summary>ResolveDevelopmentAutomationPluginWithProjectScopeAsync gibt null zurück (Dialog erforderlich), falls kein Projekt- oder Aufgaben-Plugin vorhanden.</summary>
    [Fact]
    public async Task TestResolvePluginWithProjectScope_ReturnsNullIfNoDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginManager = CreatePluginManager([]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolved = await sut.ResolveDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().BeNull();
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein aktiviertes Aufgaben-Prefix gewinnt
    /// vor Projekt- und Global-Default.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_TaskPrefixEnabled_Wins()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GlobalKi", TestContext.Current.CancellationToken);

        var task = CreateKiPlugin("Task", "Softwareschmiede.TaskKi");
        var project = CreateKiPlugin("Project", "Softwareschmiede.ProjectKi");
        var globalKi = CreateKiPlugin("Global", "Softwareschmiede.GlobalKi");
        var pluginManager = CreatePluginManager([task, project, globalKi]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync("Softwareschmiede.TaskKi", projectId, TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().Be("Softwareschmiede.TaskKi");
        resolution.IgnoredPluginPrefixes.Should().BeEmpty();
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein deaktiviertes Aufgaben-Prefix wird ignoriert
    /// und der aktivierte Projekt-Default gewählt.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_TaskPrefixDisabled_FallsBackToProjectDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);

        var task = CreateKiPlugin("Task", "Softwareschmiede.TaskKi");
        var project = CreateKiPlugin("Project", "Softwareschmiede.ProjectKi");
        var pluginManager = CreatePluginManager([task, project]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("Softwareschmiede.TaskKi", false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync("Softwareschmiede.TaskKi", projectId, TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().Be("Softwareschmiede.ProjectKi");
        resolution.IgnoredPluginPrefixes.Should().ContainSingle().Which.Should().Be("Softwareschmiede.TaskKi");
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein nicht installiertes Aufgaben-Prefix wird
    /// ignoriert und der aktivierte Projekt-Default gewählt.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_TaskPrefixNotInstalled_FallsBack()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);

        var project = CreateKiPlugin("Project", "Softwareschmiede.ProjectKi");
        var pluginManager = CreatePluginManager([project]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync("Softwareschmiede.Missing", projectId, TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().Be("Softwareschmiede.ProjectKi");
        resolution.IgnoredPluginPrefixes.Should().ContainSingle().Which.Should().Be("Softwareschmiede.Missing");
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein deaktivierter Projekt-Default wird ignoriert
    /// und der aktivierte Global-Default gewählt.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_ProjectDefaultDisabled_FallsBackToGlobalDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GlobalKi", TestContext.Current.CancellationToken);

        var project = CreateKiPlugin("Project", "Softwareschmiede.ProjectKi");
        var globalKi = CreateKiPlugin("Global", "Softwareschmiede.GlobalKi");
        var pluginManager = CreatePluginManager([project, globalKi]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("Softwareschmiede.ProjectKi", false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(null, projectId, TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().Be("Softwareschmiede.GlobalKi");
        resolution.IgnoredPluginPrefixes.Should().ContainSingle().Which.Should().Be("Softwareschmiede.ProjectKi");
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein deaktivierter Global-Default wird ignoriert
    /// und die in den Einstellungen konfigurierte Standard-CLI (ki.plugin.default) gewählt.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_GlobalDefaultDisabled_FallsBackToSettingsDefault()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GlobalKi", TestContext.Current.CancellationToken);

        var globalKi = CreateKiPlugin("Global", "Softwareschmiede.GlobalKi");
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([globalKi, simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("Softwareschmiede.GlobalKi", false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().Be("Softwareschmiede.KiSimulator");
        resolution.IgnoredPluginPrefixes.Should().ContainSingle().Which.Should().Be("Softwareschmiede.GlobalKi");
    }

    /// <summary>Aktivierungsbewusste Auflösung: Sind alle konfigurierten Zuordnungen deaktiviert,
    /// wird null zurückgegeben und jede ignorierte Zuordnung benannt.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_AllConfiguredDisabled_ReturnsNullWithIgnored()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var projectId = Guid.NewGuid();
        await defaultSettings.SaveProjectDefaultPluginPrefixAsync(projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.ProjectKi", TestContext.Current.CancellationToken);
        await defaultSettings.SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, "Softwareschmiede.GlobalKi", TestContext.Current.CancellationToken);

        var task = CreateKiPlugin("Task", "Softwareschmiede.TaskKi");
        var project = CreateKiPlugin("Project", "Softwareschmiede.ProjectKi");
        var globalKi = CreateKiPlugin("Global", "Softwareschmiede.GlobalKi");
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([task, project, globalKi, simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        foreach (var prefix in new[] { "Softwareschmiede.TaskKi", "Softwareschmiede.ProjectKi", "Softwareschmiede.GlobalKi", "Softwareschmiede.KiSimulator" })
            await activationService.SetPluginEnabledAsync(prefix, false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync("Softwareschmiede.TaskKi", projectId, TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().BeNull();
        resolution.IgnoredPluginPrefixes.Should().BeEquivalentTo(
            ["Softwareschmiede.TaskKi", "Softwareschmiede.ProjectKi", "Softwareschmiede.GlobalKi", "Simulator CLI"]);
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ist nichts konfiguriert, wird null mit
    /// leerer Ignoriert-Liste zurückgegeben.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_NothingConfigured_ReturnsNullEmptyIgnored()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);
        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(new AppSettingService(db, NullLogger<AppSettingService>.Instance), pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().BeNull();
        resolution.IgnoredPluginPrefixes.Should().BeEmpty();
    }

    /// <summary>Aktivierungsbewusste Auflösung: Ein auf ein deaktiviertes Plugin zeigender
    /// Einstellungen-Default (ki.plugin.default) wird ignoriert und führt zu null.</summary>
    [Fact]
    public async Task ResolveEnabledPlugin_SettingsDefaultDisabled_ReturnsNull()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var settings = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
        await settings.SetSettingAsync(AppSettingService.DefaultKiPluginKey, "Simulator CLI", TestContext.Current.CancellationToken);
        var defaultSettings = new PluginDefaultSettingsService(db, NullLogger<PluginDefaultSettingsService>.Instance);

        var simulator = CreateKiPlugin("Simulator CLI", "Softwareschmiede.KiSimulator");
        var pluginManager = CreatePluginManager([simulator]);
        var activationService = new PluginActivationService(settings, pluginManager.Object, NullLogger<PluginActivationService>.Instance);
        await activationService.SetPluginEnabledAsync("Softwareschmiede.KiSimulator", false, TestContext.Current.CancellationToken);
        var sut = new PluginSelectionService(pluginManager.Object, defaultSettings, activationService, NullLogger<PluginSelectionService>.Instance, settings);

        // Act
        var resolution = await sut.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        resolution.PluginPrefix.Should().BeNull();
        resolution.IgnoredPluginPrefixes.Should().ContainSingle().Which.Should().Be("Simulator CLI");
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
        public override string ProviderFilePrefix => providerPrefix;
        public override string PluginName => name;
        public override string PluginPrefix => prefix;
        public override PluginType PluginType => PluginType.DevelopmentAutomation;
        /// <summary>IReadOnlyList.</summary>
        public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        /// <summary>SupportsSessionContinuation.</summary>
        public override bool SupportsSessionContinuation() => false;
        /// <summary>Task.</summary>
        public override Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

        protected override System.Diagnostics.ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
            => new() { FileName = "test-cli", WorkingDirectory = localRepoPath };
    }
}
