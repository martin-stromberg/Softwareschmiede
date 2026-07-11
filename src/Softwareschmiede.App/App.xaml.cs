using System.IO;
using System.Linq;
using System.Threading;
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

    /// <summary>Service Locator für WPF-Code-behind-Klassen (Controls/Views), die von XAML ohne Konstruktor-Injection erzeugt werden.</summary>
    internal static IServiceProvider? Services { get; private set; }

    /// <inheritdoc/>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Diagnosemodus fuer Softwareschmiede.Tests.E2E.ConPtyEnvironmentProbe: Prueft, ob ConPTY-
        // Kindprozesse isoliert an die Pseudo-Konsole gebunden werden, wenn Softwareschmiede.App.exe
        // (statt z. B. des Test-Hosts) der unmittelbare Elternprozess ist - genau der Prozessbaum,
        // den KiAusfuehrungsService.StartPseudoConsoleProcess in der echten Anwendung erzeugt.
        // Ueberspringt die gesamte restliche Initialisierung (kein Host, keine DB, kein Fenster),
        // damit das Ergebnis schnell vorliegt und keine Seiteneffekte (Log-Dateien, DB-Migrationen)
        // entstehen.
        if (e.Args.Contains("--conpty-probe"))
        {
            RunConPtyProbeAndExit();
            return;
        }

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

    /// <summary>
    /// Fuehrt den ConPTY-Isolations-Selbsttest fuer <c>--conpty-probe</c> aus (siehe <see cref="OnStartup"/>)
    /// und beendet den Prozess sofort mit dem Ergebnis auf stdout ("OK" oder "FAIL:&lt;Grund&gt;").
    /// </summary>
    private static void RunConPtyProbeAndExit()
    {
        var result = RunConPtyProbeOnce();
        Console.WriteLine(result);
        Console.Out.Flush();
        Environment.Exit(0);
    }

    private static string RunConPtyProbeOnce()
    {
        const string sentinel = "CONPTY_PROBE_OK";
        Softwareschmiede.Infrastructure.Terminal.PseudoConsole? pseudoConsole = null;
        Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession? session = null;
        try
        {
            pseudoConsole = Softwareschmiede.Infrastructure.Terminal.PseudoConsole.Create(80, 25);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c echo {sentinel}",
                UseShellExecute = false,
            };

            var startResult = Softwareschmiede.Infrastructure.Terminal.PseudoConsoleProcessStarter.Start(psi, pseudoConsole);
            var process = System.Diagnostics.Process.GetProcessById(startResult.Pid);

            var inputStream = new FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.InputWritePipe, ownsHandle: false),
                FileAccess.Write, bufferSize: 1, isAsync: false);
            var outputStream = new FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(pseudoConsole.OutputReadPipe, ownsHandle: false),
                FileAccess.Read, bufferSize: 4096, isAsync: false);

            session = new Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession(pseudoConsole, process, inputStream, outputStream);

            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                for (var row = 0; row < 5; row++)
                {
                    var text = string.Concat(session.Buffer.GetRow(row).Select(c => c.Character));
                    if (text.Contains(sentinel, StringComparison.Ordinal))
                        return "OK";
                }

                Thread.Sleep(50);
            }

            return "FAIL:Timeout nach 8s ohne Sentinel-Ausgabe im Terminal-Buffer.";
        }
        catch (Exception ex)
        {
            return $"FAIL:{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            session?.Dispose();
            pseudoConsole?.Dispose();
        }
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
        services.AddTransient<ArbeitsverzeichnisBearbeitenViewModel>();
        services.AddTransient<TaskDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<IssueSelectionDialogViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }
}
