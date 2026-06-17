using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Services;

namespace Softwareschmiede.App;

/// <summary>Einstiegspunkt der WPF-Desktopanwendung Softwareschmiede.</summary>
public sealed partial class App : System.Windows.Application
{
    private IHost? _host;

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

        _host.Services.GetRequiredService<CliProcessManager>();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.Database.MigrateAsync();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
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

        // Domain Services
        services.AddScoped<AufgabeService>();
        services.AddScoped<ProjektService>();
        services.AddScoped<ProtokollService>();
        services.AddScoped<EntwicklungsprozessService>(sp => new EntwicklungsprozessService(
            sp.GetRequiredService<AufgabeService>(),
            sp.GetRequiredService<ProtokollService>(),
            sp.GetService<ProjektService>(),
            sp.GetRequiredService<IGitPlugin>(),
            sp.GetRequiredService<PluginSelectionService>(),
            sp.GetRequiredService<IArbeitsverzeichnisResolver>(),
            sp.GetService<RepositoryStartskriptService>(),
            sp.GetRequiredService<KiAusfuehrungsService>(),
            sp.GetRequiredService<ILogger<EntwicklungsprozessService>>()));
        services.AddScoped<BenachrichtigungsService>();
        services.AddScoped<BenachrichtigungsEinstellungenService>();
        services.AddScoped<BenachrichtigungsAuditService>();
        services.AddScoped<AppEinstellungService>();
        services.AddScoped<ArbeitsverzeichnisSettingsService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<AufgabeRecoveryService>();

        // Infrastructure Services
        services.AddSingleton<KiAusfuehrungsService>();
        services.AddSingleton<CliProcessManager>();
        services.AddSingleton<IBenachrichtigungsAudioService, WpfAudioService>();
        services.AddSingleton<IBenachrichtigungsBannerService, WpfBannerService>();
        services.AddSingleton<IRunningAutomationStatusSource>(sp =>
            sp.GetRequiredService<KiAusfuehrungsService>());
        services.AddSingleton<DarkModeService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<PluginSelectionDialogService>();

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
        services.AddTransient<TaskDetailViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }
}
