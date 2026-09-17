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
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
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

    /// <summary>
    /// Initialisiert die Anwendung und konfiguriert Logging, Dependency Injection und globale Exception-Handler.
    /// </summary>
    /// <param name="e">Die Startargumente der Anwendung.</param>
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

        _host.Services.GetService<UpdateE2ETestKontext>()
            ?.Protokoll.Schreibe(UpdateE2EEreignisse.DatabaseInitializationCompleted);

        using (var recoveryScope = _host.Services.CreateScope())
        {
            try
            {
                var db = recoveryScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                var projektleiterAgentService = recoveryScope.ServiceProvider.GetRequiredService<ProjektleiterAgentService>();

                var wiederherzustellendeAufgaben = await db.AutonomAufgabeKonfigurationen
                    .Include(k => k.Aufgabe)
                    .Where(k => !k.ExplizitGestoppt && k.Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv)
                    .ToListAsync();

                foreach (var konfiguration in wiederherzustellendeAufgaben)
                {
                    try
                    {
                        var resumePrompt = ErstelleResumePromptNachAppNeustart(konfiguration);
                        await projektleiterAgentService.StarteAgenNachAppNeustartAsync(konfiguration.AufgabeId, resumePrompt);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Autonome Aufgabe {AufgabeId} konnte beim App-Start nicht automatisch fortgesetzt werden.", konfiguration.AufgabeId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "App-Startup-Recovery für Autonome Aufgaben ist fehlgeschlagen.");
            }
        }

        try
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            // Explizit vor Show() zuweisen, damit Dialoge (z. B. Update-Fortschritt)
            // schon bei einer sofortigen Start-Updateprüfung einen Owner haben.
            System.Windows.Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "MainWindow konnte nicht angezeigt werden.");
        }
    }

    /// <summary>Erzeugt den Weitermachen-Prompt für die automatische Wiederaufnahme einer Autonomen Aufgabe nach einem
    /// App-Neustart (analog zu <see cref="SessionManagementService"/>s Weitermachen-Prompt-Generierung).</summary>
    /// <param name="konfiguration">Die Konfiguration der Autonomen Aufgabe.</param>
    /// <returns>Der zu sendende Weitermachen-Prompt.</returns>
    private static string ErstelleResumePromptNachAppNeustart(AutonomAufgabeKonfiguration konfiguration)
        => "Weitermachen nach App-Neustart: Setze die Arbeit an der Autonomen Aufgabe im Arbeitsverzeichnis " +
           $"'{konfiguration.ArbeitsverzeichnisPfad}' fort. Prüfe state.json, plan.md und progress.md " +
           "für den aktuellen Stand, bevor du weitermachst.";

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
        var testDatenbankPfad = Environment.GetEnvironmentVariable(UpdateE2ETestConfiguration.TestDatenbankUmgebungsVariable);
        var dbPath = DatenbankPfadResolver.ErmittlePfad(
            AppContext.BaseDirectory,
            testDatenbankPfad);

        // Die kontrollierte Update-E2E-Umgebung aktiviert sich ausschließlich, wenn beide
        // Test-Umgebungsvariablen gesetzt sind und die referenzierte Konfiguration valide ist.
        // Bei gesetzter, aber ungültiger Konfiguration schlägt der Start mit klarer Diagnose fehl;
        // es wird niemals auf reale Releases, echte Updater-Pfade oder Produktionsquellen zurückgefallen.
        var updateTestKonfiguration = UpdateE2ETestConfiguration.LadeAusUmgebung(testDatenbankPfad);
        var updateTestKontext = updateTestKonfiguration is null ? null : new UpdateE2ETestKontext(updateTestKonfiguration);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<SoftwareschmiededDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            if (updateTestKontext is not null)
                options.AddInterceptors(new UpdateSettingsReadFailureInterceptor(updateTestKontext));
        });

        if (updateTestKontext is not null)
        {
            services.AddSingleton(updateTestKontext);
            services.AddSingleton<IUpdateVersuchProtokoll>(updateTestKontext);
        }

        services.AddMemoryCache();
        services.Configure<DirectoryStructureOptions>(context.Configuration.GetSection(DirectoryStructureOptions.SectionName));
        services.Configure<AutonomAufgabenOptions>(context.Configuration.GetSection(AutonomAufgabenOptions.SectionName));
        services.AddSingleton<IOptions<UpdateOptions>>(Options.Create(new UpdateOptions()));
        services.AddSingleton<DirectoryStructureBrowserService>();

        // Domain Services
        services.AddScoped<TodoService>();
        services.AddScoped<AutonomAufgabenInitialisierungsService>();
        services.AddScoped<UnteragentGovernanceService>();
        services.AddScoped<UnteragentGitProvisioningService>();
        services.AddScoped<SessionManagementService>();
        services.AddScoped<ProjektleiterAgentService>();
        services.AddScoped<AufgabeService>();
        services.AddScoped<IAktiveAufgabenService>(sp => sp.GetRequiredService<AufgabeService>());
        services.AddScoped<ProjektService>();
        services.AddScoped<ProtokollService>();
        services.AddScoped<RepositoryStartskriptService>();
        services.AddScoped<RepositoryInitialisierungService>();
        services.AddScoped<GitOrchestrationService>();
        services.AddScoped<PullRequestReferenzService>();
        services.AddScoped<EntwicklungsprozessService>(sp => new EntwicklungsprozessService(
            sp.GetRequiredService<AufgabeService>(),
            sp.GetRequiredService<ProtokollService>(),
            sp.GetRequiredService<IGitPlugin>(),
            sp.GetRequiredService<PluginSelectionService>(),
            sp.GetRequiredService<IArbeitsverzeichnisResolver>(),
            new EntwicklungsprozessServiceOptions(
                ProjektService: sp.GetRequiredService<ProjektService>(),
                RepositoryStartskriptService: sp.GetRequiredService<RepositoryStartskriptService>(),
                RepositoryInitialisierungService: sp.GetRequiredService<RepositoryInitialisierungService>(),
                KiAusfuehrungsService: sp.GetRequiredService<KiAusfuehrungsService>(),
                GitOrchestrationService: sp.GetRequiredService<GitOrchestrationService>()),
            sp.GetRequiredService<ILogger<EntwicklungsprozessService>>()));
        services.AddScoped<BenachrichtigungsService>();
        services.AddScoped<BenachrichtigungsEinstellungenService>();
        services.AddScoped<BenachrichtigungsAuditService>();
        services.AddScoped<AppEinstellungService>();
        services.AddScoped<ArbeitsverzeichnisSettingsService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginActivationService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<AufgabeRecoveryService>();
        services.AddScoped<PromptVorlagenService>();
        services.AddSingleton<AufgabeLaufdatenChangedNotifier>();
        services.AddSingleton<PromptVorlagenPlatzhalterService>();
        services.AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>();
        services.AddSingleton<ITextDiffService, TextDiffService>();
        services.AddScoped<ArbeitsverzeichnisOeffnenService>();
        services.AddSingleton<IVisualStudioCodeLocator, VisualStudioCodeLocator>();
        services.AddScoped<ICliUpdateSafetyService, CliUpdateSafetyService>();
        services.AddScoped<AutonomAufgabeStartService>();
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
        services.AddSingleton<PullRequestMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<PullRequestMonitoringService>());
        services.AddSingleton<UnteragentGovernanceMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<UnteragentGovernanceMonitoringService>());
        services.AddSingleton<CliProcessManager>();
        services.AddSingleton<IBenachrichtigungsAudioService, WpfAudioService>();
        services.AddSingleton<IBenachrichtigungsBannerService, WpfBannerService>();
        services.AddSingleton<IRunningAutomationStatusSource>(sp =>
            sp.GetRequiredService<KiAusfuehrungsService>());
        services.AddSingleton<DarkModeService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<ICliRawExportService, CliRawExportService>();
        services.AddSingleton<IUpdateProgressDialogService, WpfUpdateProgressDialogService>();
        services.AddSingleton<IApplicationShutdownService, WpfApplicationShutdownService>();
        services.AddSingleton<PluginSelectionDialogService>();
        services.AddSingleton(sp =>
            new HttpClient());
        services.AddSingleton<IUpdateReleaseClient, GitHubReleaseClient>();
        services.AddSingleton<IUpdatePackageService, UpdatePackageService>();
        services.AddSingleton<IUpdateScriptService, UpdateScriptService>();
        services.AddSingleton<IUpdateProcessLauncher, UpdateProcessLauncher>();
        services.AddTransient(sp => new MainWindowUpdateDienste(
            sp.GetService<IUpdateService>(),
            sp.GetService<ICliUpdateSafetyService>(),
            sp.GetService<IUpdateProgressDialogService>(),
            sp.GetService<IUpdateVersuchProtokoll>()));
        services.AddTransient(sp => new MainWindowOptionaleDienste(
            DialogService: sp.GetService<IDialogService>(),
            VersionProvider: sp.GetService<IApplicationVersionProvider>(),
            LaufdatenChangedNotifier: sp.GetService<AufgabeLaufdatenChangedNotifier>(),
            UpdateDienste: sp.GetService<MainWindowUpdateDienste>()));

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
        services.AddTransient<IssueCreateDialogViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddTransient<TodoListViewModel>();
        services.AddTransient<OpenTodosDialogViewModel>();
        services.AddTransient<AutonomAufgabeInitialisierungsDialogViewModel>();

        // Windows
        services.AddTransient<MainWindow>();

        // Update-E2E-Testmodus: Transport, Prozess-, Shutdown- und Sicherheitsgrenzen werden
        // kontrolliert ersetzt (letzte Registrierung gewinnt). Die Produktionsorchestrierung
        // (UpdateService, Release-Auswahl, Paket-/Skript-Pipeline, ViewModel-Ablauf) bleibt echt.
        if (updateTestKontext is not null)
        {
            var kontext = updateTestKontext;
            var testwurzel = kontext.Konfiguration.Testwurzel;

            services.AddSingleton<IApplicationVersionProvider>(sp => new ApplicationVersionProvider(
                kontext.Konfiguration.InstallationsVerzeichnis,
                sp.GetRequiredService<ILogger<ApplicationVersionProvider>>()));

            services.AddSingleton(sp => new HttpClient(new UpdateFixtureHttpMessageHandler(kontext)));
            services.AddSingleton<IUpdateScriptService>(sp => new UpdateScriptService(
                sp.GetRequiredService<IUpdateProcessLauncher>(),
                sp.GetRequiredService<IOptions<UpdateOptions>>(),
                sp.GetRequiredService<ILogger<UpdateScriptService>>(),
                testwurzel));
            services.AddSingleton<IUpdatePackageService>(sp => new UpdatePackageService(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IUpdateScriptService>(),
                sp.GetRequiredService<IOptions<UpdateOptions>>(),
                sp.GetRequiredService<ILogger<UpdatePackageService>>(),
                testwurzel));
            services.AddSingleton<IUpdateProcessLauncher>(_ => new RecordingUpdateProcessLauncher(kontext));
            services.AddSingleton<IApplicationShutdownService>(_ => new RecordingApplicationShutdownService(kontext));
            services.AddScoped<ICliUpdateSafetyService>(_ => new FixtureCliUpdateSafetyService(kontext));
            services.AddSingleton<UpdateService>();
            services.AddSingleton<IUpdateService>(sp => new ProtokollierenderUpdateService(
                sp.GetRequiredService<UpdateService>(), kontext));
        }
    }
}
