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

/// <summary>Eigenständiger Entdeckungszyklus der Issue-Aufbereitung („Issue erstellt“): enumeriert
/// freigeschaltete Projekt-Repositories repository-weit und reiht neu angelegte Issues über
/// <see cref="ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync"/> ein. Läuft unabhängig vom
/// konfigurierten Überwachungsmodus und immer unter dem Benutzerkonto
/// (<see cref="ScmKontoModus.Benutzerkonto"/>), damit die Entdeckung auch im SeparatesKonto-Modus
/// funktioniert, in dem die Notifications-Inbox die Enumeration der Ereignisüberwachung ersetzt.</summary>
/// <remarks>
/// Repository-Auflösungen werden je Alias unter <c>issueaufbereitung-repository/{prefix}/{alias}</c>,
/// die repository-weite Enumeration unter <c>issueaufbereitung-enumeration/{provider}/{host}/{repoId}</c>
/// und die abgeschlossene Auswertung je Issue unter
/// <c>issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}</c> in
/// <see cref="ScmObservationCheckpoint"/>-Einträgen geführt. Der je-Issue-Checkpoint begrenzt
/// Beschreibungs-Reads strikt auf neue bzw. fehlerbehaftete Referenzen und isoliert deren Fehler;
/// Fehler einzelner Repositories isolieren jeweils nur dieses Repository.
/// </remarks>
public sealed class ScmIssueAufbereitungMonitoringService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    ScmEventStartupGate startup, ILogger<ScmIssueAufbereitungMonitoringService> logger)
    : PeriodicBackgroundService(options.Value.PollInterval, time, logger, "SCM-Issue-Aufbereitung fehlgeschlagen.")
{
    private readonly TimeProvider _time = time;

    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        // Laufzeit-Schalter (Einstellungen → Automatisierung → Issue-Aufbereitung): Bei deaktivierter
        // Aufbereitung wird nicht enumeriert. Die Ereignisüberwachung ist davon unabhängig.
        if (!await einstellungen.GetScmIssueAufbereitungEnabledAsync(ct))
        {
            logger.LogDebug("Issue-Aufbereitung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }
        var kontoModus = await einstellungen.GetScmKontoModusAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        // Die Vorfilterung auf die Aufbereitungs-Freischaltung spart Enumeration und
        // Beschreibungs-Reads für nicht freigeschaltete Repositories.
        var repositories = await db.GitRepositories.AsNoTracking()
            .Where(x => x.Aktiv && x.IssueAufbereitungAktiv).ToListAsync(ct);
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
                var resolutionKey = $"issueaufbereitung-repository/{plugin.PluginPrefix.ToLowerInvariant()}/{Uri.EscapeDataString(alias.ToLowerInvariant())}";
                var checkpoint = await resolutionDb.ScmObservationCheckpoints.FindAsync([resolutionKey], ct);
                if (checkpoint?.NextAttemptUtc > _time.GetUtcNow().ToUnixTimeMilliseconds())
                    continue;
                try
                {
                    // Die Aufbereitungs-Entdeckung läuft immer unter dem Benutzerkonto — unabhängig
                    // vom konfigurierten Überwachungsmodus der Ereignisüberwachung.
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
                        " Bis zur erfolgreichen Repositoryauflösung wird dieses Repository für die Issue-Aufbereitung ausgelassen.",
                        retryPermanent: true);
                }
            }
            // Aufgelöste Repositories werden über den Enumerations-Checkpoint dedupliziert;
            // Enumerationsfehler blockieren die übrigen Repositories nicht.
            var enumerated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var repository in repositoriesForEnumeration)
            {
                var lokale = localRepositories.TryGetValue(repository, out var kandidaten) ? kandidaten : new List<GitRepository>();
                await ScmCheckpointSupport.EnumerateRepositoryAsync(
                    scopes, _time, options.Value.PollInterval, source, repository,
                    "issueaufbereitung-enumeration", enumerated,
                    async (discovered, token) =>
                    {
                        // Die Issue-Auswertung ist unabhängig vom Enumerations-Checkpoint: Ein Fehler
                        // darf den erfolgreichen Enumerations-Watermark nicht zurücksetzen.
                        try
                        {
                            await EvaluateDiscoveredIssuesAsync(source, repository, discovered, lokale, kontoModus, token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Issue-Aufbereitung: Auswertung für Repository {RepositoryId} fehlgeschlagen.", repository.RepositoryId);
                        }
                    }, ct);
            }
        }
        // Sofort fällige Aufbereitungsereignisse werden auch bei deaktivierter Ereignisüberwachung
        // zugestellt — der Zyklus stößt die Zustellung daher selbst an.
        using var dispatchScope = scopes.CreateScope();
        await dispatchScope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchDueEventsAsync(ct);
    }

    /// <summary>Wertet die enumerierten Referenzen eines Repositories aus: Beim ersten Lauf wird
    /// nur die Baseline angelegt und der Bestand ohne Beschreibungs-Read als ausgewertet markiert;
    /// danach werden nicht ausgewertete Issue-Referenzen einzeln gelesen und eingereiht.</summary>
    private async Task EvaluateDiscoveredIssuesAsync(
        IScmEventSource source, ScmEventRepository repository,
        IReadOnlyList<ScmEventReference> discovered, IReadOnlyList<GitRepository> lokaleRepositories,
        ScmKontoModus kontoModus, CancellationToken ct)
    {
        var issues = discovered.Where(x => x.Kind == ScmReferenceKind.Issue).ToList();
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var baseline = await db.ScmObservationCheckpoints.FindAsync([ScmIssueAufbereitungsService.BaselineKey(repository)], ct);
        if (baseline is null)
        {
            // Erstlauf: Beobachtungsbeginn festlegen und alle enumerierten Issue-Referenzen als
            // ausgewertet markieren — kein Retro-Scan, keine Beschreibungs-Reads.
            await scope.ServiceProvider.GetRequiredService<ScmIssueAufbereitungsService>()
                .BaselineAnlegenAsync(repository, issues, ct);
            return;
        }
        foreach (var issue in issues)
        {
            try
            {
                await EvaluateIssueAsync(source, issue, lokaleRepositories, kontoModus, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Ein Auswertungsfehler isoliert nur dieses Issue (Standard-Backoff;
                // „Permanent“/„Permission“/„Authentication“ sperren nur dieses Issue dauerhaft).
                await ScmCheckpointSupport.RecordFailureAsync(scopes, _time,
                    ScmIssueAufbereitungsService.AuswertungsCheckpointKey(issue), ex, ct,
                    " Bis zur erfolgreichen Auswertung wird dieses Issue für die Aufbereitung ausgelassen.");
            }
        }
    }

    /// <summary>Liest die Beschreibung einer nicht ausgewerteten Issue-Referenz und übergibt sie an
    /// <see cref="ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync"/>; danach steht der
    /// Auswertungs-Checkpoint auf Endzustand.</summary>
    private async Task EvaluateIssueAsync(
        IScmEventSource source, ScmEventReference issue,
        IReadOnlyList<GitRepository> lokaleRepositories, ScmKontoModus kontoModus, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var aufbereitung = scope.ServiceProvider.GetRequiredService<ScmIssueAufbereitungsService>();
        // Bereits ausgewertet (kein offener Fehler) bzw. Fehler-Backoff noch nicht erreicht:
        // kein Beschreibungs-Read (Rate-Limit-Schonung).
        if (await aufbereitung.AuswertungUeberspringenAsync(issue, ct))
            return;
        // Die erzeugte Referenz trägt das konfigurierte Monitoring-Konto: Remote-Aktionen des
        // Ausführungspfads wählen ihr Credential über ScmEventRepository.Konto.
        var tagged = issue with { Repository = issue.Repository with { Konto = kontoModus } };
        var batch = await source.ReadEventsAsync(
            new ScmEventReadRequest(tagged, ScmContentKind.Description, null, _time.GetUtcNow()), ct);
        await aufbereitung.ErkennenUndEinreihenAsync(source, tagged, batch, lokaleRepositories, ct);
        // Endzustand je Issue: Folgezyklen überspringen die Referenz ohne Beschreibungs-Read.
        await aufbereitung.AuswertungAbschliessenAsync(tagged, ct);
    }
}
