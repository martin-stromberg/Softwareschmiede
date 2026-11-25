using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Unit-Tests für die Remote-Übersicht in <see cref="SettingsViewModel"/> (Issue 478):
/// Laden und Speichern der Einstellungswerte inkl. Token im CredentialStore, Instanz-ID-Vergabe,
/// Feld-Sichtbarkeit und Verbindungsstatus-Anzeige.
/// </summary>
public sealed class SettingsViewModelTests_RemoteStatus : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _settingService;
    private readonly WorkingDirectorySettingsService _workingDirectoryService;
    private readonly DarkModeService _darkModeService;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly PluginActivationService _pluginActivationService;
    private readonly InMemoryCredentialStoreForSettings _credentialStore;
    private readonly PluginSettingsService _pluginSettingsService;
    private readonly PromptTemplateService _promptTemplatesService;
    private readonly RemoteStatusConnectionInfo _remoteStatusInfo;

    /// <summary>SettingsViewModelTests_RemoteStatus.</summary>
    public SettingsViewModelTests_RemoteStatus()
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
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginActivationService = new PluginActivationService(_settingService, _pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _credentialStore = new InMemoryCredentialStoreForSettings();
        _pluginSettingsService = new PluginSettingsService(_credentialStore, NullLogger<PluginSettingsService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _remoteStatusInfo = new RemoteStatusConnectionInfo();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Ohne gespeicherte Werte lädt Load die Defaults: deaktiviert, leere Felder, Felder unsichtbar.</summary>
    [Fact]
    public async Task LoadAsync_WithoutSavedValues_LoadsDefaults()
    {
        var sut = CreateSut();

        await LoadAsync(sut);

        sut.IsRemoteStatusEnabled.Should().BeFalse();
        sut.IsRemoteStatusFieldEnabled.Should().BeFalse();
        sut.RemoteStatusUrl.Should().BeEmpty();
        sut.RemoteStatusInstanceName.Should().BeEmpty();
        sut.RemoteStatusToken.Should().BeEmpty();
        sut.RemoteStatusHeaderStatusText.Should().Be("deaktiviert");
        sut.RemoteStatusConnectionText.Should().NotBeNullOrEmpty();
    }

    /// <summary>Gespeicherte Werte (inkl. Token und Instanz-ID) werden beim Laden befüllt.</summary>
    [Fact]
    public async Task LoadAsync_WithSavedValues_PopulatesAllFields()
    {
        await _settingService.SetRemoteStatusEnabledAsync(true, TestContext.Current.CancellationToken);
        await _settingService.SetRemoteStatusUrlAsync("https://status.example.org", TestContext.Current.CancellationToken);
        await _settingService.SetRemoteStatusInstanceNameAsync("Martin-PC", TestContext.Current.CancellationToken);
        var instanceId = await _settingService.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken);
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "geheim");
        var sut = CreateSut();

        await LoadAsync(sut);

        sut.IsRemoteStatusEnabled.Should().BeTrue();
        sut.IsRemoteStatusFieldEnabled.Should().BeTrue();
        sut.HasRemoteStatusToken.Should().BeTrue();
        sut.RemoteStatusHeaderStatusText.Should().Be("aktiviert");
        sut.RemoteStatusUrl.Should().Be("https://status.example.org");
        sut.RemoteStatusInstanceName.Should().Be("Martin-PC");
        sut.RemoteStatusInstanceId.Should().Be(instanceId);
        sut.RemoteStatusToken.Should().Be("geheim");
    }

    /// <summary>Aktiviert ohne gespeicherte Instanz-ID vergibt Load sofort eine GUID-förmige ID.</summary>
    [Fact]
    public async Task LoadAsync_EnabledWithoutInstanceId_AssignsInstanceId()
    {
        await _settingService.SetRemoteStatusEnabledAsync(true, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        await LoadAsync(sut);

        sut.RemoteStatusInstanceId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParseExact(sut.RemoteStatusInstanceId, "N", out _).Should().BeTrue();
    }

    /// <summary>Save persistiert Schalter, URL, Name und Token und vergibt die Instanz-ID.</summary>
    [Fact]
    public async Task SaveAsync_PersistsRemoteStatusSettingsAndToken()
    {
        var sut = CreateSut();
        await LoadAsync(sut);
        sut.IsRemoteStatusEnabled = true;
        sut.RemoteStatusUrl = " http://localhost:5055 ";
        sut.RemoteStatusInstanceName = " Martin-PC ";
        sut.RemoteStatusToken = " geheim ";

        await SaveAsync(sut);

        (await _settingService.GetRemoteStatusEnabledAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await _settingService.GetRemoteStatusUrlAsync(TestContext.Current.CancellationToken))
            .Should().Be("http://localhost:5055");
        (await _settingService.GetRemoteStatusInstanceNameAsync(TestContext.Current.CancellationToken))
            .Should().Be("Martin-PC");
        (await _settingService.GetOrCreateRemoteStatusInstanceIdAsync(TestContext.Current.CancellationToken))
            .Should().Be(sut.RemoteStatusInstanceId);
        _credentialStore.GetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget)
            .Should().Be("geheim");
    }

    /// <summary>Ein geleertes Token-Feld entfernt den gespeicherten Credential-Eintrag beim Speichern.</summary>
    [Fact]
    public async Task SaveAsync_EmptyToken_DeletesCredential()
    {
        _credentialStore.SetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget, "alt");
        var sut = CreateSut();
        await LoadAsync(sut);
        sut.RemoteStatusToken = "   ";

        await SaveAsync(sut);

        _credentialStore.GetCredential(AppSettingService.RemoteStatusRelayTokenCredentialTarget)
            .Should().BeNull();
    }

    /// <summary>Ein Zustandswechsel der Verbindungsinfo aktualisiert den angezeigten Status-Text.</summary>
    [Fact]
    public async Task StateChanged_UpdatesConnectionText()
    {
        var sut = CreateSut();
        await LoadAsync(sut);
        var before = sut.RemoteStatusConnectionText;

        _remoteStatusInfo.SetState(RemoteStatusConnectionState.Connected);

        sut.RemoteStatusConnectionText.Should().NotBe(before);
        sut.RemoteStatusConnectionText.Should().Contain("verbunden");
    }

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
            new Mock<IServiceProvider>().Object,
            _remoteStatusInfo,
            _credentialStore);

    private static Task LoadAsync(SettingsViewModel sut)
        => ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

    private static Task SaveAsync(SettingsViewModel sut)
        => ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();
}
