using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Gemeinsame Test-Infrastruktur für Update-Tests des <see cref="MainWindowViewModel"/>:
/// echter DI-Container mit SQLite-Dateidatenbank, die Update-Kollaborateure als Mocks und
/// Hilfsmethoden zum Setzen/Speichern der Update-Einstellungen über Service und UI.
/// </summary>
public abstract class MainWindowViewModelUpdateTestBase : IDisposable
{
    /// <summary>Standard-Updateangebot der Tests (stabile Version 1.2.3).</summary>
    protected static readonly UpdateInfo NeuesUpdate = new(
        "1.2.3", "v1.2.3", "release.zip",
        new Uri("https://example.invalid/release.zip"), null, IsPrerelease: false);

    /// <summary>Standard-Vorbereitungsergebnis der Tests.</summary>
    protected static readonly UpdatePreparationResult FertigeVorbereitung = new(
        "release.zip", "extracted", "update.ps1", "update.log", false);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"sw-mwvm-update-{Guid.NewGuid():N}.db");

    /// <summary>Der DI-Container des Tests.</summary>
    protected readonly ServiceProvider _provider;
    /// <summary>Mock der Quelle für den Status laufender Automatisierungen.</summary>
    protected readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock = new();
    /// <summary>Mock des Plugin-Managers.</summary>
    protected readonly Mock<IPluginManager> _pluginManagerMock = new();
    /// <summary>Mock des Update-Service.</summary>
    protected readonly Mock<IUpdateService> _updateServiceMock = new();
    /// <summary>Mock des CLI-Update-Sicherheitsdienstes.</summary>
    protected readonly Mock<ICliUpdateSafetyService> _safetyServiceMock = new();
    /// <summary>Mock des Fortschrittsdialog-Dienstes.</summary>
    protected readonly Mock<IUpdateProgressDialogService> _progressDialogMock = new();
    /// <summary>Mock des Dialog-Dienstes.</summary>
    protected readonly Mock<IDialogService> _dialogServiceMock = new();

    /// <summary>Initialisiert den DI-Container mit echter SQLite-Datenbank (In-Memory-Datei).</summary>
    protected MainWindowViewModelUpdateTestBase()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetIdePlugins()).Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDbContext<SoftwareschmiededDbContext>(o =>
        {
            o.UseSqlite($"Data Source={_dbPath}");
            ConfigureDbContext(o);
        });
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
    /// Haken für abgeleitete Klassen, um die DbContext-Optionen zu erweitern
    /// (z. B. EF-Interceptoren für kontrollierte Lesefehler zu registrieren).
    /// </summary>
    /// <param name="options">Der Options-Builder des DbContext.</param>
    protected virtual void ConfigureDbContext(DbContextOptionsBuilder options)
    {
    }

    /// <summary>Erstellt ein MainWindowViewModel mit den konfigurierten Update-Mocks.</summary>
    protected MainWindowViewModel CreateSut()
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
            new MainWindowOptionaleDienste(
                DispatcherInvoke: action => action(),
                DialogService: _dialogServiceMock.Object,
                UpdateDienste: new MainWindowUpdateDienste(
                    _updateServiceMock.Object,
                    _safetyServiceMock.Object,
                    _progressDialogMock.Object,
                    VersuchProtokoll: null)));
    }

    /// <summary>Speichert Update-Einstellungen über den echten AppEinstellungService.</summary>
    protected async Task SetUpdateSettingsAsync(UpdateSettings settings)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetUpdateSettingsAsync(settings);
    }

    /// <summary>
    /// Speichert Update-Einstellungen über die echte Settings-UI (gleicher Pfad wie in der Anwendung),
    /// sodass das UpdateSettingsSaved-Ereignis im MainWindowViewModel ausgelöst wird.
    /// </summary>
    protected async Task SpeichereUpdateEinstellungenUeberUiAsync(
        MainWindowViewModel sut, UpdateModusOption modus, bool includePrereleases)
    {
        sut.NavigateToSettingsCommand.Execute(null);
        var settingsViewModel = sut.CurrentView.Should().BeOfType<SettingsViewModel>().Subject;
        await ((AsyncRelayCommand)settingsViewModel.LadenCommand).ExecuteAsync();
        settingsViewModel.SelectedUpdateMode = modus;
        settingsViewModel.IncludePrereleases = includePrereleases;
        await ((AsyncRelayCommand)settingsViewModel.SpeichernCommand).ExecuteAsync();
        settingsViewModel.FehlerMeldung.Should().BeNull(
            "das Speichern der Update-Einstellungen muss im Test erfolgreich sein");
    }
}
