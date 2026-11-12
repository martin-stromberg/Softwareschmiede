using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Eigenständiger Prüfzyklus der Backmerge-Automatisierung („Prozessabläufe"): enumeriert
/// repository-weit alle Referenzen aktiver Projekt-Repositories und bewertet Pull Requests auf das
/// Backmerge-Muster. Läuft unabhängig von der SCM-Ereignisüberwachung und immer unter dem
/// Benutzerkonto (<see cref="ScmKontoModus.Benutzerkonto"/>), damit die Entdeckung auch im
/// SeparatesKonto-Modus der Ereignisüberwachung repository-weit funktioniert.</summary>
/// <remarks>
/// Repository-Auflösungen werden je Alias unter <c>backmerge-repository/{prefix}/{alias}</c> und die
/// repository-weite Enumeration unter <c>backmerge-enumeration/{provider}/{host}/{repoId}</c> mit
/// Watermark/Backoff in <see cref="ScmObservationCheckpoint"/>-Einträgen geführt. Fehler einzelner
/// Repositories isolieren jeweils nur dieses Repository.
/// </remarks>
public sealed class ScmBackmergeMonitoringService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    ScmEventStartupGate startup, ILogger<ScmBackmergeMonitoringService> logger)
    : PeriodicBackgroundService(options.Value.PollInterval, time, logger, "SCM-Backmerge-Prüfung fehlgeschlagen.")
{
    private readonly TimeProvider _time = time;

    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        // Laufzeit-Schalter (Einstellungen → Automatisierung → Prozessabläufe): Bei deaktiviertem
        // Backmerge-Modus wird nicht enumeriert. Die Ereignisüberwachung ist davon unabhängig.
        if (await einstellungen.GetScmBackmergeModusAsync(ct) == ScmBackmergeModus.Deaktiviert)
        {
            logger.LogDebug("Backmerge-Automatisierung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var repositories = await db.GitRepositories.AsNoTracking().Where(x => x.Aktiv).ToListAsync(ct);
        var plugins = scope.ServiceProvider.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        foreach (var plugin in plugins)
        {
            if (plugin is not IScmEventSource source)
                continue;
            var repositoriesForEnumeration = new List<ScmEventRepository>();
            var localRepositories = new Dictionary<ScmEventRepository, List<GitRepository>>();
            var aliasGroups = repositories
                .Where(x => ScmPluginMatcher.MatchesPlugin(plugin, x) && !string.IsNullOrWhiteSpace(x.RepositoryUrl))
                .GroupBy(x => x.RepositoryUrl, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var (alias, aliasGroup) in aliasGroups)
            {
                using var resolutionScope = scopes.CreateScope();
                var resolutionDb = resolutionScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
                var resolutionKey = $"backmerge-repository/{plugin.PluginPrefix.ToLowerInvariant()}/{Uri.EscapeDataString(alias.ToLowerInvariant())}";
                var checkpoint = await resolutionDb.ScmObservationCheckpoints.FindAsync([resolutionKey], ct);
                if (checkpoint?.NextAttemptUtc > _time.GetUtcNow().ToUnixTimeMilliseconds())
                    continue;
                try
                {
                    // Die Backmerge-Prüfung läuft immer unter dem Benutzerkonto — unabhängig vom
                    // konfigurierten Überwachungsmodus der Ereignisüberwachung.
                    var repository = await source.GetEventRepositoryAsync(
                        new ScmRepositoryContext(aliasGroup[0].Id, plugin.PluginPrefix, alias, ScmKontoModus.Benutzerkonto), ct);
                    if (!localRepositories.TryGetValue(repository, out var candidates))
                        localRepositories[repository] = candidates = [];
                    foreach (var lokal in aliasGroup)
                        if (candidates.All(x => x.Id != lokal.Id))
                            candidates.Add(lokal);
                    if (checkpoint is not null)
                    {
                        resolutionDb.Remove(checkpoint);
                        await resolutionDb.SaveChangesAsync(ct);
                        resolutionScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
                    }
                    repositoriesForEnumeration.Add(repository);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // Repository-Auflösungen werden auch bei „Permanent“ (z. B. 404 nach entzogenem
                    // oder noch fehlendem Zugriff) per Backoff erneut versucht, statt dauerhaft
                    // gesperrt zu bleiben; der Fehler isoliert nur dieses Repository.
                    await ScmCheckpointSupport.RecordFailureAsync(scopes, _time, resolutionKey, ex, ct,
                        " Bis zur erfolgreichen Repositoryauflösung wird dieses Repository für die Backmerge-Prüfung ausgelassen.",
                        retryPermanent: true);
                }
            }
            // Aufgelöste Repositories werden über den Enumerations-Checkpoint dedupliziert;
            // Enumerationsfehler blockieren die übrigen Repositories nicht.
            var enumerated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var repository in repositoriesForEnumeration)
            {
                using var enumerationScope = scopes.CreateScope();
                var backmerge = enumerationScope.ServiceProvider.GetRequiredService<ScmBackmergeService>();
                var lokale = localRepositories.TryGetValue(repository, out var kandidaten) ? kandidaten : new List<GitRepository>();
                await ScmCheckpointSupport.EnumerateRepositoryAsync(
                    scopes, _time, options.Value.PollInterval, source, repository,
                    "backmerge-enumeration", enumerated,
                    async (discovered, token) =>
                    {
                        // Die Backmerge-Bewertung ist unabhängig vom Enumerations-Checkpoint: Ein Fehler
                        // darf den erfolgreichen Enumerations-Watermark nicht zurücksetzen.
                        try
                        {
                            await backmerge.EvaluateDiscoveredReferencesAsync(plugin, repository, discovered, lokale, token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Backmerge-Bewertung für Repository {RepositoryId} fehlgeschlagen.", repository.RepositoryId);
                        }
                    }, ct);
            }
        }
    }
}
