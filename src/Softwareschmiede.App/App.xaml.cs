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
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Options;
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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.Database.MigrateAsync();

            var promptTemplatesService = scope.ServiceProvider.GetRequiredService<PromptTemplateService>();
            await promptTemplatesService.EnsureInitialPromptTemplatesAsync();
        }

        // Re-Arm persistierter zeitgesteuerter Prompts bewusst NACH MigrateAsync: Bei einem Update,
        // das die Tabelle neu erzeugt, darf der Zugriff nicht vor der Migration erfolgen.
        try
        {
            var promptScheduleDispatchService = _host.Services.GetRequiredService<PromptScheduleDispatchService>();
            await promptScheduleDispatchService.RestoreScheduledPromptsAsync();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Persistierte zeitgesteuerte Prompts konnten nicht wiederhergestellt werden. Die Anwendung läuft ohne deren Wiederherstellung weiter.");
        }

        _host.Services.GetRequiredService<ScmEventStartupGate>().Ready();

        // Auto-Shutdown-Orchestrator frühzeitig aktivieren, damit die Event-Abonnements
        // (RunningCountChanged, LaufdatenChanged) bereits vor dem ersten UI-Schalter stehen.
        _host.Services.GetRequiredService<IAutoShutdownOrchestrator>();

        _host.Services.GetService<UpdateE2ETestContext>()
            ?.Log.Write(UpdateE2EEvents.DatabaseInitializationCompleted);

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
        var testDatabasePath = Environment.GetEnvironmentVariable(UpdateE2ETestConfiguration.TestDatabaseEnvironmentVariable);
        var dbPath = DatabasePathResolver.ResolvePath(
            AppContext.BaseDirectory,
            testDatabasePath);

        // Die kontrollierte Update-E2E-Umgebung aktiviert sich ausschließlich, wenn beide
        // Test-Umgebungsvariablen gesetzt sind und die referenzierte Konfiguration valide ist.
        // Bei gesetzter, aber ungültiger Konfiguration schlägt der Start mit klarer Diagnose fehl;
        // es wird niemals auf reale Releases, echte Updater-Pfade oder Produktionsquellen zurückgefallen.
        var updateTestConfiguration = UpdateE2ETestConfiguration.LoadFromEnvironment(testDatabasePath);
        var updateTestContext = updateTestConfiguration is null ? null : new UpdateE2ETestContext(updateTestConfiguration);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<SoftwareschmiedeDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            if (updateTestContext is not null)
                options.AddInterceptors(new UpdateSettingsReadFailureInterceptor(updateTestContext));
        });

        if (updateTestContext is not null)
        {
            services.AddSingleton(updateTestContext);
            services.AddSingleton<IUpdateAttemptLog>(updateTestContext);
        }

        services.AddMemoryCache();
        services.Configure<DirectoryStructureOptions>(context.Configuration.GetSection(DirectoryStructureOptions.SectionName));
        services.Configure<TerminalSessionOptions>(context.Configuration.GetSection(TerminalSessionOptions.SectionName));
        services.AddSingleton<IOptions<UpdateOptions>>(Options.Create(new UpdateOptions()));
        services.AddSingleton<DirectoryStructureBrowserService>();

        // Domain Services
        services.AddScoped<TodoService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<IActiveWorkItemsService>(sp => sp.GetRequiredService<WorkItemService>());
        services.AddScoped<ProjectService>();
        services.AddScoped<LogService>();
        services.AddScoped<RepositoryStartScriptService>();
        services.AddScoped<RepositoryInitializationService>();
        services.AddScoped<GitOrchestrationService>();
        services.AddScoped<PullRequestReferenceService>();
        services.Configure<ScmEventOptions>(context.Configuration.GetSection("ScmEvents"));
        // Technischer Auflösungscache der SCM-Plugins (gleiche „ScmEvents“-Sektion); der Optionswert
        // wird als Singleton bereitgestellt, damit ActivatorUtilities ihn in Plugin-Konstruktoren injiziert.
        services.Configure<ScmResolutionCacheOptions>(context.Configuration.GetSection("ScmEvents"));
        services.PostConfigure<ScmResolutionCacheOptions>(o =>
            o.RepositoryResolutionCacheTtl = o.EffectiveRepositoryResolutionCacheTtl());
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ScmResolutionCacheOptions>>().Value);
        services.AddSingleton(ScmTaskCoordinator.Shared);
        services.AddSingleton<ScmEventStartupGate>();
        services.AddSingleton<ScmEventChangeNotifier>();
        services.AddSingleton<ScmEventExchangeProtocol>();
        services.AddScoped<ScmEventStore>();
        services.AddScoped<ScmEventOutbox>();
        services.AddScoped<ScmEventOrchestrator>();
        // Prozessweiter Reservierungsbestand der SCM-Ausführungsbegrenzung (geteilt über alle Scopes).
        services.AddSingleton<ScmStartSlotReservationPool>();
        services.AddScoped<ScmEventExecutionLimitService>();
        services.AddScoped<ScmEventTaskResolver>();
        services.AddScoped<ScmIssuePreparationService>();
        services.AddScoped<ScmBackmergeService>();
        services.AddSingleton<ScmEventMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmEventMonitoringService>());
        services.AddSingleton<ScmBackmergeMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmBackmergeMonitoringService>());
        services.AddSingleton<ScmIssuePreparationMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmIssuePreparationMonitoringService>());
        services.AddSingleton<ScmEventExchangeService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmEventExchangeService>());
        services.AddScoped<DevelopmentProcessService>(sp => new DevelopmentProcessService(
            sp.GetRequiredService<WorkItemService>(),
            sp.GetRequiredService<LogService>(),
            sp.GetRequiredService<IGitPlugin>(),
            sp.GetRequiredService<PluginSelectionService>(),
            sp.GetRequiredService<IWorkingDirectoryResolver>(),
            new DevelopmentProcessServiceOptions(
                ProjectService: sp.GetRequiredService<ProjectService>(),
                RepositoryStartScriptService: sp.GetRequiredService<RepositoryStartScriptService>(),
                RepositoryInitializationService: sp.GetRequiredService<RepositoryInitializationService>(),
                AiExecutionService: sp.GetRequiredService<AiExecutionService>(),
                GitOrchestrationService: sp.GetRequiredService<GitOrchestrationService>(),
                PromptScheduleDispatchService: sp.GetRequiredService<PromptScheduleDispatchService>()),
            sp.GetRequiredService<ILogger<DevelopmentProcessService>>()));
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationSettingsService>();
        services.AddScoped<NotificationAuditService>();
        services.AddScoped<AppSettingService>();
        services.AddScoped<AiPluginLimitService>();
        services.AddScoped<WorkingDirectorySettingsService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginActivationService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<WorkItemRecoveryService>();
        services.AddScoped<PromptTemplateService>();
        services.AddScoped<ScheduledPromptStore>();
        services.AddSingleton<WorkItemRunDataChangedNotifier>();
        services.AddSingleton<PromptTemplatePlaceholderService>();
        services.AddScoped<IGitWorkspaceBrowserService, GitWorkspaceBrowserService>();
        services.AddSingleton<ITextDiffService, TextDiffService>();
        services.AddScoped<WorkingDirectoryOpenService>();
        services.AddSingleton<IVisualStudioCodeLocator, VisualStudioCodeLocator>();
        services.AddScoped<ICliUpdateSafetyService, CliUpdateSafetyService>();
        services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // Infrastructure Services
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TerminalSessionService.TestDatabasePathVariable)))
        {
            services.AddSingleton<IProcessStarter>(sp => new RecordingProcessStarter(
                sp.GetRequiredService<ILogger<RecordingProcessStarter>>(),
                RecordingProcessStarter.ResolveLogFilePath(dbPath)));
            // Testmodus: kein echter OS-Shutdown — Anforderungen landen in der Logdatei neben der Test-DB.
            services.AddSingleton<ISystemShutdownService>(sp => new RecordingSystemShutdownService(
                sp.GetRequiredService<ILogger<RecordingSystemShutdownService>>(),
                RecordingSystemShutdownService.ResolveLogFilePath(dbPath)));
        }
        else
        {
            services.AddSingleton<IProcessStarter, SystemProcessStarter>();
            services.AddSingleton<ISystemShutdownService, SystemShutdownService>();
        }

        // Terminal-Session-Erzeugung: die Backend-Wahl (PTY/Pipe, E2E-Modus, Diagnose) liegt vollständig
        // im TerminalSessionService — beide Launcher werden als konkrete Typen registriert und über die
        // Factory injiziert.
        services.AddSingleton<Win32PseudoConsoleProcessLauncher>();
        services.AddSingleton<SimulatedPseudoConsoleProcessLauncher>();
        services.AddSingleton<TerminalSessionService>(sp => new TerminalSessionService(
            sp.GetRequiredService<IOptions<TerminalSessionOptions>>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Win32PseudoConsoleProcessLauncher>(),
            sp.GetRequiredService<SimulatedPseudoConsoleProcessLauncher>(),
            sp.GetRequiredService<ILogger<TerminalSessionService>>()));
        services.AddSingleton<ITerminalSessionFactory>(sp => sp.GetRequiredService<TerminalSessionService>());
        services.AddSingleton<AiExecutionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PromptScheduleDispatchService>();
        services.AddSingleton<PullRequestMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<PullRequestMonitoringService>());
        services.AddSingleton<ScmTaskCompletionMonitoringService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmTaskCompletionMonitoringService>());

        // Remote-Übersicht: ausgehender Relay-Client + Snapshot-Pipeline + Zustandsinfo.
        // Bleibt ohne wirksame status.remote.enabled-Einstellung im Zustand Disabled inaktiv.
        services.AddSingleton<RemoteStatusConnectionInfo>();
        services.AddSingleton<StatusSnapshotProjector>();
        services.AddSingleton<RemoteStatusRelayClient>();
        services.AddSingleton<IRemoteStatusRelayClient>(sp => sp.GetRequiredService<RemoteStatusRelayClient>());
        services.AddSingleton<RemoteStatusRelayService>();
        services.AddHostedService(sp => sp.GetRequiredService<RemoteStatusRelayService>());
        // Ereignisgesteuerter Auslöser der SCM-Startprüfung (freiwerdende Plätze, Pausen).
        services.AddSingleton<ScmStartCheckTrigger>();
        services.AddSingleton<ScmStartCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScmStartCheckService>());
        services.AddSingleton<CliProcessManager>();
        services.AddSingleton<INotificationAudioService, WpfAudioService>();
        services.AddSingleton<INotificationBannerService, WpfBannerService>();
        services.AddSingleton<IRunningAutomationStatusSource>(sp =>
            sp.GetRequiredService<AiExecutionService>());
        services.AddSingleton<ICliExecutionLimitSource>(sp =>
            sp.GetRequiredService<AiExecutionService>());
        services.AddSingleton<DarkModeService>();
        services.AddSingleton<WorkItemListModeService>();

        // Auto-Shutdown bei Abschluss: Zähler und Orchestrator sind Singletons (greifen für
        // scoped Dienste selbst über IServiceScopeFactory zu); der Einstellungen-Service
        // persistiert nur die Nebenwerte (Erzwingen, Toleranz) — die Aktivierung bleibt Session-Zustand.
        services.AddSingleton<RunningAutomationsCount>();
        services.AddSingleton<IRunningAutomationsCount>(sp =>
            sp.GetRequiredService<RunningAutomationsCount>());
        services.AddSingleton<AutoShutdownOrchestrator>();
        services.AddSingleton<IAutoShutdownOrchestrator>(sp =>
            sp.GetRequiredService<AutoShutdownOrchestrator>());
        services.AddSingleton<AutoShutdownSettingsService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<ICliRawExportService, CliRawExportService>();
        services.AddSingleton<CliReplayRecordingStore>();
        services.AddSingleton<ICliReplayExportService, CliReplayExportService>();
        services.AddSingleton<IUpdateProgressDialogService, WpfUpdateProgressDialogService>();
        services.AddSingleton<IApplicationShutdownService, WpfApplicationShutdownService>();
        services.AddSingleton<PluginSelectionDialogService>();
        services.AddSingleton(sp =>
            new HttpClient());
        services.AddSingleton<IUpdateReleaseClient, GitHubReleaseClient>();
        services.AddSingleton<IUpdatePackageService, UpdatePackageService>();
        services.AddSingleton<IUpdateScriptService, UpdateScriptService>();
        services.AddSingleton<IUpdateProcessLauncher, UpdateProcessLauncher>();
        services.AddTransient(sp => new MainWindowUpdateServices(
            sp.GetService<IUpdateService>(),
            sp.GetService<ICliUpdateSafetyService>(),
            sp.GetService<IUpdateProgressDialogService>(),
            sp.GetService<IUpdateAttemptLog>()));
        services.AddTransient(sp => new MainWindowOptionalServices(
            DialogService: sp.GetService<IDialogService>(),
            VersionProvider: sp.GetService<IApplicationVersionProvider>(),
            RunDataChangedNotifier: sp.GetService<WorkItemRunDataChangedNotifier>(),
            UpdateServices: sp.GetService<MainWindowUpdateServices>(),
            WorkItemListModeService: sp.GetService<WorkItemListModeService>(),
            AutoShutdownOrchestrator: sp.GetService<IAutoShutdownOrchestrator>(),
            AutoShutdownSettingsService: sp.GetService<AutoShutdownSettingsService>(),
            TimeProvider: sp.GetService<TimeProvider>()));

        // Plugin Infrastructure
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginManager>());
        services.AddSingleton<WindowsCredentialStore>();
        services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<WindowsCredentialStore>());
        services.AddSingleton<CliRunner>();
        services.AddSingleton<ICliRunner>(sp => sp.GetRequiredService<CliRunner>());

        // Infrastructure implementations for domain interfaces
        services.AddScoped<IUserContextService, UserContextService>();
        services.AddScoped<IWorkingDirectoryResolver, RuntimeWorkingDirectoryResolver>();
        services.AddScoped<PluginDefaultSettingsService>();
        services.AddScoped<IGitPlugin>(sp => sp.GetRequiredService<IPluginManager>().GetDefaultSourceCodeManagementPlugin());

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ScmEventQueueViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProjectListViewModel>();
        services.AddTransient<ProjectDetailViewModel>();
        services.AddTransient<RepositoryAssignViewModel>();
        services.AddTransient<WorkingDirectoryEditViewModel>();
        services.AddTransient<TaskDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<IssueSelectionDialogViewModel>();
        services.AddTransient<IssueCreateDialogViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddTransient<TodoListViewModel>();
        services.AddTransient<OpenTodosDialogViewModel>();
        services.AddTransient<PauseWorkItemDialogViewModel>();
        services.AddTransient<ConsoleTestViewModel>();

        var scmTestConfiguration = ScmE2ETestConfiguration.LoadFromEnvironment(testDatabasePath);
        if (scmTestConfiguration is not null)
        {
            services.AddSingleton(scmTestConfiguration);
            // Prozessgebundener Credential-Store: Plugin--Einstellungen und das Fixture-Plugin
            // teilen denselben Speicher; der echte WindowsCredentialStore bleibt unberührt.
            services.AddSingleton<ScmE2ECredentialStore>();
            services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<ScmE2ECredentialStore>());
            services.AddSingleton<IPluginManager, ScmE2EPluginManager>();
            services.AddSingleton<ITerminalSessionFactory, ScmE2ETerminalSessionFactory>();
            services.AddHostedService<ScmE2ERuntimeControl>();
            services.Configure<ScmEventOptions>(o => o.PollInterval = TimeSpan.FromMilliseconds(300));
        }

        // Windows
        services.AddTransient<MainWindow>();

        // Update-E2E-Testmodus: Transport, Prozess-, Shutdown- und Sicherheitsgrenzen werden
        // kontrolliert ersetzt (letzte Registrierung gewinnt). Die Produktionsorchestrierung
        // (UpdateService, Release-Auswahl, Paket-/Skript-Pipeline, ViewModel-Ablauf) bleibt echt.
        if (updateTestContext is not null)
        {
            var updateContext = updateTestContext;
            var testwurzel = updateContext.Configuration.TestRoot;

            services.AddSingleton<IApplicationVersionProvider>(sp => new ApplicationVersionProvider(
                updateContext.Configuration.InstallationDirectory,
                sp.GetRequiredService<ILogger<ApplicationVersionProvider>>()));

            services.AddSingleton(sp => new HttpClient(new UpdateFixtureHttpMessageHandler(updateContext)));
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
            services.AddSingleton<IUpdateProcessLauncher>(_ => new RecordingUpdateProcessLauncher(updateContext));
            services.AddSingleton<IApplicationShutdownService>(_ => new RecordingApplicationShutdownService(updateContext));
            services.AddScoped<ICliUpdateSafetyService>(_ => new FixtureCliUpdateSafetyService(updateContext));
            services.AddSingleton<UpdateService>();
            services.AddSingleton<IUpdateService>(sp => new LoggingUpdateService(
                sp.GetRequiredService<UpdateService>(), updateContext));
        }
    }
}
