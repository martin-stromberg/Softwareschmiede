using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Services;
using Softwareschmiede.Infrastructure.Services.Updates;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App;

/// <summary>Einstiegspunkt der WPF-Desktopanwendung Softwareschmiede.</summary>
public sealed partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>Service Locator für WPF-Code-behind-Klassen (Controls/Views), die von XAML ohne Konstruktor-Injection erzeugt werden.</summary>
    internal static IServiceProvider? Services { get; private set; }

    /// <inheritdoc/>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "softwareschmiede-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            await StartupAsync(e);
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Fehler beim Starten der Anwendung.");
            MessageBox.Show(
                $"Die Anwendung konnte nicht gestartet werden:\n\n{ex.Message}",
                "Startfehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async System.Threading.Tasks.Task StartupAsync(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        Services = _host.Services;

        try
        {
            _host.Services.GetRequiredService<CliProcessManager>();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "CliProcessManager konnte nicht initialisiert werden. Die Anwendung läuft ohne CLI-Funktionalität weiter.");
        }

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.Database.MigrateAsync();

            var promptVorlagenService = scope.ServiceProvider.GetRequiredService<PromptVorlagenService>();
            await promptVorlagenService.EnsureInitialPromptVorlagenAsync();
        }

        try
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "MainWindow konnte nicht angezeigt werden.");
        }
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unbehandelte Exception im UI-Thread.");
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Logger.Error(e.ExceptionObject as Exception, "Unbehandelte Exception außerhalb des UI-Threads.");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unbeobachtete Task-Exception.");
        e.SetObserved();
    }

    /// <inheritdoc/>
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Fehler beim Beenden des Hosts.");
            }
            finally
            {
                _host.Dispose();
            }
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var dbPath = Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Softwareschmiede",
                "softwareschmiede.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<SoftwareschmiededDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddMemoryCache();
        services.Configure<DirectoryStructureOptions>(context.Configuration.GetSection(DirectoryStructureOptions.SectionName));
        services.AddSingleton<IOptions<UpdateOptions>>(Options.Create(new UpdateOptions()));
        services.AddSingleton<DirectoryStructureBrowserService>();

        // Domain Services
        services.AddScoped<AufgabeService>();
        services.AddScoped<ProjektService>();
        services.AddScoped<ProtokollService>();
        services.AddScoped<RepositoryStartskriptService>();
        services.AddScoped<GitOrchestrationService>();
        services.AddScoped<EntwicklungsprozessService>(sp => new EntwicklungsprozessService(
            sp.GetRequiredService<AufgabeService>(),
            sp.GetRequiredService<ProtokollService>(),
            sp.GetRequiredService<IGitPlugin>(),
            sp.GetRequiredService<PluginSelectionService>(),
            sp.GetRequiredService<IArbeitsverzeichnisResolver>(),
            new EntwicklungsprozessServiceOptions(
                ProjektService: sp.GetRequiredService<ProjektService>(),
                RepositoryStartskriptService: sp.GetRequiredService<RepositoryStartskriptService>(),
                KiAusfuehrungsService: sp.GetRequiredService<KiAusfuehrungsService>(),
                GitOrchestrationService: sp.GetRequiredService<GitOrchestrationService>()),
            sp.GetRequiredService<ILogger<EntwicklungsprozessService>>()));
        services.AddScoped<BenachrichtigungsService>();
        services.AddScoped<BenachrichtigungsEinstellungenService>();
        services.AddScoped<BenachrichtigungsAuditService>();
        services.AddScoped<AppEinstellungService>();
        services.AddScoped<ArbeitsverzeichnisSettingsService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<AufgabeRecoveryService>();
        services.AddScoped<PromptVorlagenService>();
        services.AddSingleton<PromptVorlagenPlatzhalterService>();
        services.AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>();
        services.AddSingleton<ITextDiffService, TextDiffService>();
        services.AddScoped<ArbeitsverzeichnisOeffnenService>();
        services.AddScoped<IdeOeffnenService>();
        services.AddScoped<ICliUpdateSafetyService, CliUpdateSafetyService>();
        services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // Infrastructure Services
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH")))
        {
            services.AddSingleton<IPseudoConsoleProcessLauncher, SimulatedPseudoConsoleProcessLauncher>();
            services.AddSingleton<IProzessStarter>(sp => new AufzeichnenderProzessStarter(
                sp.GetRequiredService<ILogger<AufzeichnenderProzessStarter>>(),
                AufzeichnenderProzessStarter.ResolveLogDateiPfad(dbPath)));
        }
        else
        {
            services.AddSingleton<IPseudoConsoleProcessLauncher, Win32PseudoConsoleProcessLauncher>();
            services.AddSingleton<IProzessStarter, SystemProzessStarter>();
        }
        services.AddSingleton<KiAusfuehrungsService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PromptZeitVersandService>();
        services.AddSingleton<CliProcessManager>();
        services.AddSingleton<IBenachrichtigungsAudioService, WpfAudioService>();
        services.AddSingleton<IBenachrichtigungsBannerService, WpfBannerService>();
        services.AddSingleton<IRunningAutomationStatusSource>(sp =>
            sp.GetRequiredService<KiAusfuehrungsService>());
        services.AddSingleton<DarkModeService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IUpdateProgressDialogService, WpfUpdateProgressDialogService>();
        services.AddSingleton<IApplicationShutdownService, WpfApplicationShutdownService>();
        services.AddSingleton<PluginSelectionDialogService>();
        services.AddSingleton(sp =>
            new HttpClient());
        services.AddSingleton<IUpdateReleaseClient, GitHubReleaseClient>();
        services.AddSingleton<IUpdatePackageService, UpdatePackageService>();
        services.AddSingleton<IUpdateScriptService, UpdateScriptService>();
        services.AddSingleton<IUpdateProcessLauncher, UpdateProcessLauncher>();

        // Plugin Infrastructure
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginManager>());
        services.AddSingleton<WindowsCredentialStore>();
        services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<WindowsCredentialStore>());
        services.AddSingleton<CliRunner>();
        services.AddSingleton<ICliRunner>(sp => sp.GetRequiredService<CliRunner>());

        // Infrastructure implementations for domain interfaces
        services.AddScoped<IBenutzerkontextService, BenutzerkontextService>();
        services.AddScoped<IArbeitsverzeichnisResolver, ArbeitsverzeichnisResolver>();
        services.AddScoped<PluginDefaultSettingsService>();
        services.AddScoped<IGitPlugin>(sp => sp.GetRequiredService<IPluginManager>().GetDefaultSourceCodeManagementPlugin());

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProjectListViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<RepositoryAssignViewModel>();
        services.AddTransient<ArbeitsverzeichnisBearbeitenViewModel>();
        services.AddTransient<TaskDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<IssueSelectionDialogViewModel>();
        services.AddTransient<FileExplorerViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }
}
