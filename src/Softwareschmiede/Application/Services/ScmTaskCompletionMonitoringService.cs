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

/// <summary>
/// Abschlussüberwachung ereignisgesteuerter Aufgaben: Prüft zyklisch — im selben Intervall-Stil wie
/// <see cref="ScmEventMonitoringService"/> — den Remote-Zustand der SCM-Referenzen von Aufgaben, denen
/// mindestens ein Inbox-Ereignis zugeordnet wurde. Ist die verknüpfte Referenz geschlossen/gemergt
/// (alle verknüpften Pull Requests; ohne Pull-Request-Verknüpfung das verknüpfte Issue), startet die
/// persistierte Wartezeit (<see cref="Aufgabe.ScmAbschlussErkanntUtc"/>); nach deren Ablauf wird die
/// laufende KI-Ausführung beendet und die Aufgabe auf den Endstatus <see cref="AufgabeStatus.Beendet"/>
/// gesetzt. Manuelle Aufgaben ohne SCM-Ereigniszuordnung bleiben unberührt.
/// </summary>
public sealed class ScmTaskCompletionMonitoringService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<ScmEventOptions> options,
    ScmEventStartupGate startup, ILogger<ScmTaskCompletionMonitoringService> logger)
    : PeriodicBackgroundService(options.Value.PollInterval, time, logger, "SCM-Abschlussüberwachung fehlgeschlagen.")
{
    private readonly TimeProvider _time = time;
    /// <inheritdoc/>
    public override async Task RunOnceAsync(CancellationToken ct = default)
    {
        await startup.WaitAsync(ct);
        using var scope = scopes.CreateScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
        // Laufzeit-Schalter (Einstellungen → Allgemein → SCM-Ereignisse): Bei deaktivierter
        // Abschlussüberwachung wird weder beobachtet noch automatisch abgeschlossen; ein bereits
        // gesetzter Erkennungszeitpunkt bleibt persistiert bestehen.
        if (!await einstellungen.GetScmTaskCompletionEnabledAsync(ct))
        {
            logger.LogDebug("SCM-Abschlussüberwachung ist deaktiviert; Durchlauf wird übersprungen.");
            return;
        }
        var wait = TimeSpan.FromMinutes(await einstellungen.GetScmTaskCompletionWaitMinutesAsync(ct));
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        // Ereignisgesteuert = mindestens ein eingegangenes SCM-Ereignis; abgeschlossene Ereignisse
        // entscheiden die Referenzbeobachtung nicht vorzeitig — erst nach deren Abschluss.
        var candidates = await db.Aufgaben
            .Where(a => a.Status != AufgabeStatus.Beendet && a.Status != AufgabeStatus.Archiviert
                && db.ScmIncomingEvents.Any(e => e.AufgabeId == a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        foreach (var taskId in candidates)
        {
            try
            {
                await CheckTaskAsync(taskId, wait, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "SCM-Abschlussprüfung für Aufgabe {AufgabeId} fehlgeschlagen.", taskId);
            }
        }
    }

    private async Task CheckTaskAsync(Guid taskId, TimeSpan wait, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SoftwareschmiededDbContext>();
        // Laufende oder wartende Ereignisverarbeitung hat Vorrang; eine Referenzbeobachtung wäre verfrüht.
        if (await db.ScmIncomingEvents.AnyAsync(
                x => x.AufgabeId == taskId && x.Status != "Verarbeitet" && x.Status != "Abgebrochen", ct))
            return;
        var task = await db.Aufgaben
            .Include(a => a.GitRepository)
            .Include(a => a.IssueReferenz)
            .Include(a => a.PullRequests)
            .SingleOrDefaultAsync(a => a.Id == taskId, ct);
        if (task is null || task.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert)
            return;

        var now = _time.GetUtcNow();
        var protokoll = services.GetRequiredService<ProtokollService>();
        var closedLabel = await DetermineClosureAsync(services, db, task, ct);
        if (closedLabel is null)
        {
            // Die Referenz wurde wieder geöffnet (oder ist noch offen): Der Countdown wird zurückgesetzt.
            if (task.ScmAbschlussErkanntUtc is not null)
            {
                var label = task.ScmAbschlussReferenz ?? "Verknüpfte SCM-Referenz";
                task.ScmAbschlussErkanntUtc = null;
                task.ScmAbschlussReferenz = null;
                await db.SaveChangesAsync(ct);
                await protokoll.AddEintragAsync(taskId, ProtokollTyp.SystemMeldung,
                    $"{label} ist wieder offen; der automatische Abschluss wurde zurückgesetzt.", ct: ct);
            }
            return;
        }
        if (task.ScmAbschlussErkanntUtc is null)
        {
            task.ScmAbschlussErkanntUtc = now;
            task.ScmAbschlussReferenz = closedLabel;
            await db.SaveChangesAsync(ct);
            await protokoll.AddEintragAsync(taskId, ProtokollTyp.SystemMeldung,
                $"{closedLabel} ist geschlossen/gemergt; die Aufgabe wird nach Ablauf der konfigurierten Wartezeit automatisch beendet.",
                ct: ct);
            return;
        }
        if (now - task.ScmAbschlussErkanntUtc.Value < wait)
            return;
        // Der Abschluss läuft unter derselben Aufgabensperre wie Dispatch und manuelle Starts.
        await ScmTaskCoordinator.Shared.RunAsync(taskId, async () =>
        {
            db.ChangeTracker.Clear();
            var current = await db.Aufgaben.FindAsync([taskId], ct);
            if (current is null || current.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert
                || current.ScmAbschlussErkanntUtc is null
                || _time.GetUtcNow() - current.ScmAbschlussErkanntUtc.Value < wait)
                return;
            if (await db.ScmIncomingEvents.AnyAsync(
                    x => x.AufgabeId == taskId && x.Status != "Verarbeitet" && x.Status != "Abgebrochen", ct))
                return;
            try
            {
                await services.GetRequiredService<KiAusfuehrungsService>().StopCliAsync(taskId, ct);
                await services.GetRequiredService<EntwicklungsprozessService>().AbschliessenAsync(taskId, ct);
                await protokoll.AddEintragAsync(taskId, ProtokollTyp.SystemMeldung,
                    $"Aufgabe automatisch beendet: {current.ScmAbschlussReferenz} ist geschlossen/gemergt und die Abschluss-Wartezeit ist abgelaufen.",
                    ct: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                // Z. B. offene To-Dos oder ein nicht löschbarer Klon blockieren den Abschluss; der
                // Erkennungszeitpunkt bleibt persistiert, der nächste Durchlauf versucht es erneut.
                logger.LogWarning(ex,
                    "Automatischer Abschluss der Aufgabe {AufgabeId} nach Referenz-Schluss fehlgeschlagen.", taskId);
            }
        }, ct);
    }

    /// <summary>Ermittelt, ob die verknüpfte SCM-Referenz der Aufgabe geschlossen/gemergt ist.
    /// Verknüpfte Pull Requests haben Vorrang und müssen allesamt geschlossen/gemergt sein; ohne
    /// Pull-Request-Verknüpfung gilt der Zustand des verknüpften Issues. Gibt eine Anzeigebezeichnung
    /// der geschlossenen Referenz zurück oder <see langword="null"/>.</summary>
    private async Task<string?> DetermineClosureAsync(
        IServiceProvider services, SoftwareschmiededDbContext db, Aufgabe task, CancellationToken ct)
    {
        var plugins = services.GetRequiredService<IPluginManager>().GetSourceCodeManagementPlugins();
        if (task.PullRequests.Count > 0)
        {
            var changed = false;
            foreach (var reference in task.PullRequests)
            {
                // Gemergt ist final; ein lediglich geschlossener Pull Request kann sich
                // wieder öffnen und wird daher weiter geprüft.
                if (reference.Status == PullRequestStatus.Merged)
                    continue;
                var plugin = ResolvePlugin(plugins, task, reference.Provider);
                if (plugin is null || !PullRequestMonitoringPolicy.CanMonitor(reference.Rolle, reference.Provider))
                    return null;
                PullRequestStatusInfo status;
                try
                {
                    status = await plugin.GetPullRequestStatusAsync(
                        reference.RepositoryId, reference.PullRequestNumber, ct);
                }
                catch (NotSupportedException)
                {
                    return null;
                }
                reference.Status = status.Status;
                reference.MergeStatus = status.MergeStatus;
                reference.LastCheckedUtc = _time.GetUtcNow();
                if (!string.IsNullOrWhiteSpace(status.Url))
                    reference.Url = status.Url;
                changed = true;
                if (status.Status is not (PullRequestStatus.Closed or PullRequestStatus.Merged))
                    return null;
            }
            if (changed)
                await db.SaveChangesAsync(ct);
            var numbers = task.PullRequests.Select(x => x.PullRequestNumber);
            return "Pull Request " + string.Join(", ", numbers.Select(x => "#" + x));
        }
        if (task.IssueReferenz?.IssueNummer is > 0 && task.GitRepository is not null)
        {
            var plugin = plugins.FirstOrDefault(x => MatchesPlugin(x, task.GitRepository));
            if (plugin is not IScmEventSource source)
                return null;
            var repository = await source.GetEventRepositoryAsync(
                new ScmRepositoryContext(task.GitRepository.Id, plugin.PluginPrefix,
                    task.GitRepository.RepositoryUrl), ct);
            var issue = await source.ReadIssueAsync(
                new ScmEventReference(repository, ScmReferenceKind.Issue, task.IssueReferenz.IssueNummer.Value), ct);
            return issue?.IstGeschlossen == true
                ? $"Issue #{task.IssueReferenz.IssueNummer.Value}"
                : null;
        }
        return null;
    }

    /// <summary>Löst das Plugin für eine Referenz bevorzugt über den Provider-Prefix auf und fällt
    /// andernfalls auf das Repository-Plugin der Aufgabe zurück.</summary>
    private static IGitPlugin? ResolvePlugin(IReadOnlyList<IGitPlugin> plugins, Aufgabe task, PullRequestProvider provider)
    {
        var expected = PullRequestProviderDescriptor.GetPluginPrefix(provider);
        return plugins.FirstOrDefault(x => string.Equals(x.PluginPrefix, expected, StringComparison.OrdinalIgnoreCase))
            ?? (task.GitRepository is null ? null : plugins.FirstOrDefault(x => MatchesPlugin(x, task.GitRepository)));
    }

    private static bool MatchesPlugin(IGitPlugin plugin, GitRepository repository)
        => string.Equals(plugin.PluginPrefix, repository.PluginTyp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(plugin.PluginName, repository.PluginTyp, StringComparison.OrdinalIgnoreCase);
}
