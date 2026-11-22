using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.TestSupport;

/// <summary>Gemeinsame Dienst-Registrierung der SCM-Test-Fixtures (<see cref="ScmDispatchFixture"/>,
/// Auflösungs-Budget-Fixture): echte Services auf echtem SQLite; nur die Grenzen (Plugin-Manager,
/// SCM-Plugin, Arbeitsverzeichnisse, Terminal-Factory, Zeit) kommen als Argumente der Fixture.</summary>
internal static class ScmTestServices
{
    /// <summary>Registriert den vollständigen SCM-Dienststapel, den die Monitoring-/Dispatch-
    /// Fixtures benötigen.</summary>
    /// <param name="services">Die zu befüllende Dienstsammlung der Fixture.</param>
    /// <param name="databasePath">Dateipfad der SQLite-Testdatenbank.</param>
    /// <param name="plugins">Plugin-Manager der Fixture (Mock).</param>
    /// <param name="gitPlugin">SCM-Plugin der Fixture (Mock oder echtes Plugin).</param>
    /// <param name="directories">Arbeitsverzeichnis-Resolver der Fixture (Mock).</param>
    /// <param name="factory">Terminal-Session-Factory der Fixture (Mock).</param>
    /// <param name="time">Zeitquelle der Fixture.</param>
    internal static IServiceCollection AddScmTestServices(this IServiceCollection services,
        string databasePath, IPluginManager plugins, IGitPlugin gitPlugin,
        IWorkingDirectoryResolver directories, ITerminalSessionFactory factory, TimeProvider time)
    {
        services.AddLogging();
        services.AddDbContext<SoftwareschmiedeDbContext>(options => options.UseSqlite($"Data Source={databasePath};Pooling=False"));
        services.AddSingleton(plugins);
        services.AddSingleton(gitPlugin);
        services.AddSingleton(directories);
        services.AddSingleton(factory);
        services.AddSingleton(time);
        services.AddSingleton<IOptions<TerminalSessionOptions>>(Options.Create(new TerminalSessionOptions()));
        services.AddSingleton<IOptions<ScmEventOptions>>(Options.Create(new ScmEventOptions()));
        services.AddSingleton<AiExecutionService>();
        services.AddSingleton<ICliExecutionLimitSource>(sp => sp.GetRequiredService<AiExecutionService>());
        services.AddSingleton<IRunningAutomationStatusSource>(sp => sp.GetRequiredService<AiExecutionService>());
        services.AddSingleton<PromptScheduleDispatchService>();
        services.AddSingleton<ScmEventStartupGate>();
        services.AddSingleton<ScmEventChangeNotifier>();
        services.AddSingleton<ScmEventExchangeProtocol>();
        services.AddSingleton<ScmEventMonitoringService>();
        services.AddSingleton<ScmBackmergeMonitoringService>();
        services.AddSingleton<ScmIssuePreparationMonitoringService>();
        services.AddSingleton<ScmTaskCompletionMonitoringService>();
        services.AddSingleton<ScmStartCheckTrigger>();
        services.AddSingleton<ScmStartCheckService>();
        services.AddSingleton<WorkItemRunDataChangedNotifier>();
        services.AddScoped<PullRequestReferenceService>();
        services.AddScoped<TodoService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<LogService>();
        services.AddScoped<AppSettingService>();
        services.AddScoped<PluginDefaultSettingsService>();
        services.AddScoped<PluginActivationService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<AiPluginLimitService>();
        services.AddScoped<ScmEventStore>();
        services.AddScoped<ScmEventOutbox>();
        services.AddScoped<ScmEventOrchestrator>();
        // Jede Fixture erhält einen eigenen Reservierungsbestand — dadurch sind die
        // Reservierungen der Fixture vollständig von parallel laufenden Tests isoliert.
        services.AddSingleton<ScmStartSlotReservationPool>();
        services.AddScoped<ScmEventExecutionLimitService>();
        services.AddScoped<ScmEventTaskResolver>();
        services.AddScoped<ScmIssuePreparationService>();
        services.AddScoped<ScmBackmergeService>();
        services.AddScoped(sp => new DevelopmentProcessService(sp.GetRequiredService<WorkItemService>(), sp.GetRequiredService<LogService>(),
            gitPlugin, sp.GetRequiredService<PluginSelectionService>(), directories,
            new DevelopmentProcessServiceOptions(AiExecutionService: sp.GetRequiredService<AiExecutionService>(),
                PromptScheduleDispatchService: sp.GetRequiredService<PromptScheduleDispatchService>()), sp.GetRequiredService<ILogger<DevelopmentProcessService>>()));
        return services;
    }
}
