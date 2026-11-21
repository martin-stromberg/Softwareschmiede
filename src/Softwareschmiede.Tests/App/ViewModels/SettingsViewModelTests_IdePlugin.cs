using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die IDE-Plugin-Logik in SettingsViewModel: Reihenfolge-Parsing (plugins.ide.order), Verschiebe-Logik sowie Aktivierungs-Validierung und -Persistierung.</summary>
public sealed class SettingsViewModelTests_IdePlugin : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _workingDirectoryService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptTemplatesService;

    /// <summary>SettingsViewModelTests_IdePlugin.</summary>
    public SettingsViewModelTests_IdePlugin()
    {
        _db = TestDbContextFactory.Create();
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _workingDirectoryService = new WorkingDirectorySettingsService(_db, NullLogger<WorkingDirectorySettingsService>.Instance);

        var scopeMock = new Mock<IServiceScope>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceProviderMock.Setup(p => p.GetService(typeof(AppSettingService))).Returns(_settingService);

        _darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_settingService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSettingsService = new PluginSettingsService(new Mock<ICredentialStore>().Object, NullLogger<PluginSettingsService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private SettingsViewModel CreateSut() =>
        new(
            _settingService,
            _workingDirectoryService,
            _darkModeService,
            _pluginManagerMock.Object,
            _pluginActivationService,
            _pluginSettingsService,
            _promptTemplatesService,
            new ProjectService(_db, NullLogger<ProjectService>.Instance),
            NullLogger<SettingsViewModel>.Instance,
            new Mock<IDialogService>().Object,
            new Mock<IServiceProvider>().Object);

    private static IIdePlugin CreateIdePlugin(string pluginName, string pluginPrefix)
    {
        var mock = new Mock<IIdePlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.Ide);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock.Object;
    }

    private static IGitPlugin CreateScmPlugin(string pluginName, string pluginPrefix)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns(pluginName);
        mock.Setup(p => p.PluginPrefix).Returns(pluginPrefix);
        mock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock.Object;
    }

    /// <summary>
    /// Die Sichtbarkeit der beiden Inhaltsbereiche (SCM/KI-Plugin--Einstellungen vs. IDE-Plugin--Einstellungen)
    /// ist exklusiv: Nur der zuletzt ausgewählte Bereich ist sichtbar. Die jeweils andere Auswahl
    /// (SelectedPlugin bzw. SelectedIdePlugin) bleibt dabei erhalten und wird nicht auf null zurückgesetzt.
    /// </summary>
    [Fact]
    public async Task PluginAndIdePluginSelection_SwitchContentareasExclusiveVisible()
    {
        var scmPlugin = CreateScmPlugin("GitHub", "Softwareschmiede.GitHub");
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin]);
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var scmEntry = sut.SourceCodeManagementPlugins.Single();
        var idePluginEntry = sut.DevelopmentEnvironmentPlugins.Single();

        sut.PluginSelectedCommand.Execute(scmEntry);

        sut.IsScmAiPluginContentVisible.Should().BeTrue();
        sut.IsIdePluginContentVisible.Should().BeFalse();

        sut.IdePluginSelectedCommand.Execute(idePluginEntry);

        sut.IsIdePluginContentVisible.Should().BeTrue();
        sut.IsScmAiPluginContentVisible.Should().BeFalse();
        sut.SelectedPlugin.Should().NotBeNull();

        sut.PluginSelectedCommand.Execute(scmEntry);

        sut.IsScmAiPluginContentVisible.Should().BeTrue();
        sut.IsIdePluginContentVisible.Should().BeFalse();
        sut.SelectedIdePlugin.Should().NotBeNull();
    }

    /// <summary>Ohne gespeichertes plugins.ide.order-Setting entspricht die Reihenfolge der Entdeckungsreihenfolge.</summary>
    [Fact]
    public async Task LoadAsync_WithoutOrderSetting_UsesDiscoveryorder()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).Should().Equal(
            "Softwareschmiede.VisualStudio",
            "Softwareschmiede.VisualStudioCode");
        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudio", "Softwareschmiede.VisualStudioCode");
        sut.DefaultIdePlugin.Should().Be("Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein unbekannter Prefix im gespeicherten Setting wird beim Parsen ignoriert.</summary>
    [Fact]
    public async Task LoadAsync_OrderSettingWithUnknownPrefix_IgnoresUnknownPrefix()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        await _settingService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.Unbekannt,Softwareschmiede.VisualStudioCode", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
    }

    /// <summary>Ein im gespeicherten Setting fehlender Prefix wird in Entdeckungsreihenfolge angehängt.</summary>
    [Fact]
    public async Task LoadAsync_OrderSettingWithMissingPrefix_HangsMissingPrefixAn()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        await _settingService.SetSettingAsync("plugins.ide.order", "Softwareschmiede.VisualStudioCode", TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
    }

    /// <summary>Der erste Eintrag kann nicht weiter nach oben, der letzte nicht weiter nach unten verschoben werden.</summary>
    [Fact]
    public async Task IdePluginMoveCommands_AnTheBoundary_AreNotExecutable()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var firstEntry = sut.DevelopmentEnvironmentPlugins[0];
        var lastEntry = sut.DevelopmentEnvironmentPlugins[1];

        sut.IdePluginMoveUpCommand.CanExecute(firstEntry).Should().BeFalse();
        sut.IdePluginMoveDownCommand.CanExecute(firstEntry).Should().BeTrue();
        sut.IdePluginMoveUpCommand.CanExecute(lastEntry).Should().BeTrue();
        sut.IdePluginMoveDownCommand.CanExecute(lastEntry).Should().BeFalse();
    }

    /// <summary>IdePluginMoveDownCommand verschiebt den Eintrag und aktualisiert IdePluginOrder sowie DefaultIdePlugin.</summary>
    [Fact]
    public async Task IdePluginMoveDownCommand_MovesEntryAndUpdatesOrder()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var firstEntry = sut.DevelopmentEnvironmentPlugins[0];

        sut.IdePluginMoveDownCommand.Execute(firstEntry);

        sut.DevelopmentEnvironmentPlugins.Select(e => e.PluginPrefix).Should().Equal(
            "Softwareschmiede.VisualStudioCode",
            "Softwareschmiede.VisualStudio");
        sut.IdePluginOrder.Should().Equal("Softwareschmiede.VisualStudioCode", "Softwareschmiede.VisualStudio");
        sut.DefaultIdePlugin.Should().Be("Softwareschmiede.VisualStudioCode");
    }

    /// <summary>Der Versuch, das letzte aktive IDE-Plugin zu deaktivieren, wird sofort rückgängig gemacht und zeigt eine Fehlermeldung, ohne dass Speichern nötig ist.</summary>
    [Fact]
    public async Task DeactivateOfLastActiveIdePlugins_WillImmediatelyUndoneMade()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var entry = sut.DevelopmentEnvironmentPlugins.Single();
        entry.IsEnabled.Should().BeTrue();

        entry.IsEnabled = false;

        entry.IsEnabled.Should().BeTrue();
        sut.ErrorMessage.Should().Contain("Mindestens ein IDE-Plugin");
    }

    /// <summary>Eine noch nicht gespeicherte IDE-Plugin-Aktivierungsänderung wird durch VerwerfenAsync rückgängig gemacht und bleibt in der Datenbank unverändert (Konsistenz mit SCM-/KI-Plugins).</summary>
    [Fact]
    public async Task DiscardAsync_MakesNotSavedIdePluginActivationUndone()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled = false;
        await ((AsyncRelayCommand)sut.DiscardCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled.Should().BeTrue();
        var savedStatus = await _pluginActivationService.IsPluginEnabledAsync("Softwareschmiede.VisualStudioCode", TestContext.Current.CancellationToken);
        savedStatus.Should().BeTrue();
    }

    /// <summary>SpeichernAsync persistiert geänderte IDE-Plugin-Aktivierungen zusammen mit SCM-/KI-Plugins in derselben Speicher-Operation.</summary>
    [Fact]
    public async Task SaveAsync_PersistedIdePluginActivation()
    {
        var visualStudio = CreateIdePlugin("Visual Studio", "Softwareschmiede.VisualStudio");
        var vsCode = CreateIdePlugin("Visual Studio Code", "Softwareschmiede.VisualStudioCode");
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([visualStudio, vsCode]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DevelopmentEnvironmentPlugins.Single(e => e.PluginPrefix == "Softwareschmiede.VisualStudioCode").IsEnabled = false;
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var savedStatus = await _pluginActivationService.IsPluginEnabledAsync("Softwareschmiede.VisualStudioCode", TestContext.Current.CancellationToken);
        savedStatus.Should().BeFalse();
    }
}
