using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>
/// Gemeinsame Test-Factory für <see cref="WorkItemListModeService"/>: baut einen
/// DI-Container (DbContext auf temporärer SQLite-Datei + scoped <see cref="AppSettingService"/>),
/// dessen <see cref="IServiceScopeFactory"/> der Service für seine Scope-Operationen nutzt.
/// Der zurückgegebene <see cref="ServiceProvider"/> gehört dem Aufrufer (Dispose-Verantwortung).
/// </summary>
public static class WorkItemsListsModeServiceTestFactory
{
    /// <summary>
    /// Erstellt einen <see cref="WorkItemListModeService"/> über einer frischen, leeren
    /// SQLite-Testdatenbank.
    /// </summary>
    /// <returns>Der Service und der zugrundeliegende ServiceProvider (für direkte DB-/Scope-Zugriffe in Tests).</returns>
    public static (WorkItemListModeService Service, ServiceProvider Provider) CreateModeService()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sws-mods-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SoftwareschmiedeDbContext>(
            o => o.UseSqlite($"Data Source={dbPath};Pooling=False"));
        services.AddScoped<AppSettingService>();
        var provider = services.BuildServiceProvider();
        using (var initScope = provider.CreateScope())
        {
            initScope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().Database.EnsureCreated();
        }

        var service = new WorkItemListModeService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkItemListModeService>.Instance);
        return (service, provider);
    }
}
