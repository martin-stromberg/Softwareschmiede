using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// T-09-Tests: GetUpdateSettingsAsync-Lesefehler an allen Grenzen (Startprüfung, vor Vorbereitung,
/// vor Updater-Start) stoppen den Update-Fluss sicher – ohne Fallback auf Defaults, Snapshots
/// oder interne Wiederholungen. Getestet mit echter SQLite-Datenbank und dem
/// <see cref="UpdateSettingsReadFailureInterceptor"/>.
/// </summary>
public sealed class MainWindowViewModelTests_UpdateSettingsReadFailure : IDisposable
{
    private static readonly UpdateInfo NeuesUpdate = new(
        "1.2.3", "v1.2.3", "release.zip",
        new Uri("https://example.invalid/release.zip"), null, IsPrerelease: false);

    private static readonly UpdatePreparationResult FertigeVorbereitung = new(
        "release.zip", "extracted", "update.ps1", "update.log", false);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"sw-mwvm-updfail-{Guid.NewGuid():N}.db");
    private readonly UpdateSettingsReadFailureInterceptor _interceptor = new();
    private readonly ServiceProvider _provider;
    private readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock = new();
    private readonly Mock<IPluginManager> _pluginManagerMock = new();
    private readonly Mock<IUpdateService> _updateServiceMock = new();
    private readonly Mock<ICliUpdateSafetyService> _safetyServiceMock = new();
    private readonly Mock<IUpdateProgressDialogService> _progressDialogMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    /// <summary>Initialisiert den DI-Container mit echter SQLite-Datenbank inklusive Fehler-Interceptor.</summary>
    public MainWindowViewModelTests_UpdateSettingsReadFailure()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDbContext<SoftwareschmiededDbContext>(o =>
            o.UseSqlite($"Data Source={_dbPath}")
                .AddInterceptors(_interceptor));
        services.AddScoped<AppEinstellungService>();
        services.AddScoped<ArbeitsverzeichnisSettingsService>();
        services.AddScoped<PluginActivationService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PromptVorlagenService>();
        services.AddScoped<TodoService>();
        services.AddScoped<AufgabeService>();
        services.AddScoped<IAktiveAufgabenService>(sp => sp.GetRequiredService<AufgabeService>());
        services.AddScoped<ProjektService>();
        services.AddScoped<AufgabeRecoveryService>();
        services.AddScoped<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<DarkModeService>();
        services.AddSingleton(_runningStatusSourceMock.Object);
        services.AddSingleton(_pluginManagerMock.Object);
        services.AddSingleton<ICredentialStore>(new InMemoryCredentialStoreForSettings());
        services.AddSingleton(Options.Create(new AutonomAufgabenOptions()));
        services.AddSingleton(TimeProvider.System);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Database.EnsureCreated();
    }

    /// <summary>Gibt den DI-Container und die SQLite-Datei frei.</summary>
    public void Dispose()
    {
        _provider.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>
    /// Ein Lesefehler beim ersten Settings-Zugriff stoppt vor jedem Release-Request –
    /// für Startautomatik, manuelle Prüfung und direkten Installationsaufruf gleichermaßen.
    /// Es gibt keinen internen Retry; über die Settings-UI ist eine Wiederherstellung möglich.
    /// </summary>
    [Theory]
    [InlineData("startautomatik")]
    [InlineData("manuellPruefen")]
    [InlineData("manuellInstallieren")]
    public async Task UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest(string einstieg)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));

        var sut = CreateSut();

        var erwartetePruefungen = 0;
        if (einstieg == "manuellInstallieren")
        {
            // Zuvor ein sichtbares Angebot herstellen, damit der Installationspfad getestet wird
            await sut.InitializeUpdatesAfterWindowReadyAsync();
            sut.UpdateVerfuegbar.Should().BeTrue();
            erwartetePruefungen = 1;
        }

        _interceptor.ActivateFailure();
        var readsBefore = _interceptor.SettingsReadCount;

        switch (einstieg)
        {
            case "startautomatik":
                await sut.InitializeUpdatesAfterWindowReadyAsync();
                break;
            case "manuellPruefen":
                await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();
                break;
            case "manuellInstallieren":
                await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
                break;
        }

        _interceptor.SettingsReadCount.Should().Be(readsBefore + 1,
            "ein Lesefehler darf nicht intern wiederholt werden");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(erwartetePruefungen),
            "nach einem Lesefehler darf kein Release-Request mehr gesendet werden");
        _safetyServiceMock.Verify(s => s.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
        _progressDialogMock.Verify(d => d.Show(It.IsAny<UpdateProgressViewModel>()), Times.Never);
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sut.UpdateHinweis.Should().NotBeNullOrEmpty(
            "ein Lesefehler muss einen sichtbaren Hinweis setzen");
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung: Fehler deaktivieren, Einstellungen über die UI neu laden und speichern
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(
            sut, "Bei Programmstart pruefen und ausfuehren", true);

        sut.UpdatePruefenCommand.CanExecute(null).Should().BeTrue(
            "nach erfolgreichem Speichern muss die Prüfung wieder verfügbar sein");
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(
                It.Is<UpdateCheckOptions>(o => o.IncludePrereleases), It.IsAny<CancellationToken>()),
            Times.Once, "die Folgeprüfung muss die aktuelle Checkbox-Einstellung übergeben");
        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(erwartetePruefungen + 1));
        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never, "eine manuelle Prüfung darf keine automatische Installation auslösen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ein Lesefehler beim erneuten Settings-Read nach der Sicherheitsbestätigung stoppt
    /// die Installation vor der Vorbereitung; der Fortschrittsdialog zeigt den Fehler.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateSettingsReadFailure_BeforePreparationStopsInstall(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        var aktivierenBeiAufruf = automatisch ? 1 : 2;
        var checkCalls = 0;
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateCheckOptions, CancellationToken>((_, _) =>
            {
                checkCalls++;
                if (checkCalls == aktivierenBeiAufruf)
                    _interceptor.ActivateFailure();
                return Task.FromResult(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
        {
            sut.UpdateVerfuegbar.Should().BeTrue();
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
        }

        _updateServiceMock.Verify(
            s => s.PrepareUpdateAsync(It.IsAny<UpdateInfo>(), It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor der Vorbereitung muss die Installation stoppen");
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        progressViewModel.Should().NotBeNull("der Fortschrittsdialog war bereits geöffnet");
        progressViewModel!.HasError.Should().BeTrue("späte Lesefehler müssen im Dialog sichtbar sein");
        progressViewModel.Message.Should().Contain("Update-Einstellungen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateCheckLaeuft.Should().BeFalse();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung über die Settings-UI: Neue Prüfung findet das Update erneut.
        var checksVorWiederherstellung = checkCalls;
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(sut, "Nur Pruefen", false);
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        _updateServiceMock.Verify(
            s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(checksVorWiederherstellung + 1));
        sut.UpdateVerfuegbar.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ein Lesefehler beim finalen Settings-Read nach erfolgreicher Vorbereitung verhindert
    /// den Updater-Start vollständig – kein MarkUpdaterStarting, kein Launcher, kein Shutdown.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateSettingsReadFailure_BeforeUpdaterStartStopsInstall(bool automatisch)
    {
        await SetUpdateSettingsAsync(new UpdateSettings(
            automatisch ? UpdateMode.BeiProgrammstartPruefenUndAusfuehren : UpdateMode.NurPruefen, false));
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<UpdateCheckOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateCheckResult.UpdateVerfuegbar(NeuesUpdate));
        var prepareCalls = 0;
        _updateServiceMock
            .Setup(s => s.PrepareUpdateAsync(NeuesUpdate, It.IsAny<IProgress<UpdatePreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<UpdateInfo, IProgress<UpdatePreparationProgress>?, CancellationToken>((_, _, _) =>
            {
                prepareCalls++;
                _interceptor.ActivateFailure();
                return Task.FromResult(FertigeVorbereitung);
            });
        _safetyServiceMock.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliUpdateSafetyResult(0, []));
        UpdateProgressViewModel? progressViewModel = null;
        _progressDialogMock.Setup(d => d.Show(It.IsAny<UpdateProgressViewModel>()))
            .Callback<UpdateProgressViewModel>(vm => progressViewModel = vm);

        var sut = CreateSut();
        await sut.InitializeUpdatesAfterWindowReadyAsync();
        if (!automatisch)
        {
            sut.UpdateVerfuegbar.Should().BeTrue();
            await ((AsyncRelayCommand)sut.UpdateStartenCommand).ExecuteAsync();
        }

        prepareCalls.Should().Be(1);
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never, "ein Lesefehler vor dem Updater-Start muss den Start verhindern");
        progressViewModel.Should().NotBeNull();
        progressViewModel!.HasError.Should().BeTrue();
        progressViewModel.Message.Should().Contain("Update-Einstellungen",
            "der Fehlerzustand darf nicht als erfolgreicher Start erscheinen");
        progressViewModel.CanClose.Should().BeTrue();
        sut.UpdateHinweis.Should().NotBeNullOrEmpty();
        sut.UpdateVerfuegbar.Should().BeFalse();
        sut.VerfuegbaresUpdate.Should().BeNull();
        sut.UpdateWirdVorbereitet.Should().BeFalse();

        // Wiederherstellung über die Settings-UI: Neue Prüfung findet das Update erneut.
        _interceptor.DeactivateFailure();
        await SpeichereUpdateEinstellungenUeberUiAsync(sut, "Nur Pruefen", false);
        await ((AsyncRelayCommand)sut.UpdatePruefenCommand).ExecuteAsync();

        sut.UpdateVerfuegbar.Should().BeTrue();
        _updateServiceMock.Verify(
            s => s.StartPreparedUpdateAsync(It.IsAny<UpdatePreparationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Erstellt ein MainWindowViewModel mit den konfigurierten Update-Mocks.</summary>
    private MainWindowViewModel CreateSut()
    {
        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        var promptZeitVersand = new PromptZeitVersandService(
            kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        return new MainWindowViewModel(
            _provider.GetRequiredService<DarkModeService>(),
            _provider,
            _provider.GetRequiredService<IAktiveAufgabenService>(),
            _provider.GetRequiredService<TodoService>(),
            promptZeitVersand,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            action => action(),
            _updateServiceMock.Object,
            _safetyServiceMock.Object,
            _progressDialogMock.Object,
            _dialogServiceMock.Object);
    }

    /// <summary>Speichert Update-Einstellungen über den echten AppEinstellungService.</summary>
    private async Task SetUpdateSettingsAsync(UpdateSettings settings)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetUpdateSettingsAsync(settings);
    }

    /// <summary>
    /// Speichert Update-Einstellungen über die echte Settings-UI (gleicher Pfad wie in der Anwendung),
    /// sodass das UpdateSettingsSaved-Ereignis im MainWindowViewModel ausgelöst wird.
    /// </summary>
    private async Task SpeichereUpdateEinstellungenUeberUiAsync(
        MainWindowViewModel sut, string modusLabel, bool includePrereleases)
    {
        sut.NavigateToSettingsCommand.Execute(null);
        var settingsViewModel = sut.CurrentView.Should().BeOfType<SettingsViewModel>().Subject;
        await ((AsyncRelayCommand)settingsViewModel.LadenCommand).ExecuteAsync();
        settingsViewModel.SelectedUpdateMode = modusLabel;
        settingsViewModel.IncludePrereleases = includePrereleases;
        await ((AsyncRelayCommand)settingsViewModel.SpeichernCommand).ExecuteAsync();
        settingsViewModel.FehlerMeldung.Should().BeNull(
            "das Speichern der Update-Einstellungen muss im Test erfolgreich sein");
    }
}
