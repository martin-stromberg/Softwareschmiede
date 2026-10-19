using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>
/// Gemeinsame Test-Factory für <see cref="AufgabenListenModusService"/>: baut einen
/// DI-Container (DbContext auf temporärer SQLite-Datei + scoped <see cref="AppEinstellungService"/>),
/// dessen <see cref="IServiceScopeFactory"/> der Service für seine Scope-Operationen nutzt.
/// Der zurückgegebene <see cref="ServiceProvider"/> gehört dem Aufrufer (Dispose-Verantwortung).
/// </summary>
public static class AufgabenListenModusServiceTestFactory
{
    /// <summary>
    /// Erstellt einen <see cref="AufgabenListenModusService"/> über einer frischen, leeren
    /// SQLite-Testdatenbank.
    /// </summary>
    /// <returns>Der Service und der zugrundeliegende ServiceProvider (für direkte DB-/Scope-Zugriffe in Tests).</returns>
    public static (AufgabenListenModusService Service, ServiceProvider Provider) ErstelleModusService()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sws-mods-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SoftwareschmiededDbContext>(
            o => o.UseSqlite($"Data Source={dbPath};Pooling=False"));
        services.AddScoped<AppEinstellungService>();
        var provider = services.BuildServiceProvider();
        using (var initScope = provider.CreateScope())
        {
            initScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Database.EnsureCreated();
        }

        var service = new AufgabenListenModusService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AufgabenListenModusService>.Instance);
        return (service, provider);
    }
}
