using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service Layer für KI-Plugin-Session-Limits. Persistiert den vom Plugin gemeldeten
/// Reset-Zeitpunkt pro <see cref="WorkItem.AiPluginPrefix"/> als <see cref="AppSetting"/>
/// (Schlüssel <c>plugins.sessionlimit.&lt;Prefix&gt;</c>) und wendet die Pause auf alle
/// aktiv ausgeführten regulären Aufgaben desselben Prefix an — ohne laufende CLI-Prozesse
/// zu unterbrechen.
/// </summary>
public sealed class AiPluginLimitService
{
    /// <summary>Schlüsselpräfix für persistierte Session-Limit-Reset-Zeitpunkte in <see cref="AppSetting"/>.</summary>
    public const string SessionLimitKeyPrefix = "plugins.sessionlimit.";

    private readonly SoftwareschmiedeDbContext _db;
    private readonly AppSettingService _appSettingService;
    private readonly WorkItemRunDataChangedNotifier _runDataChangedNotifier;
    private readonly ILogger<AiPluginLimitService> _logger;
    private readonly ScmStartCheckTrigger? _startCheckTrigger;

    /// <inheritdoc cref="AiPluginLimitService"/>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="appSettingService">Dienst für Anwendungseinstellungen.</param>
    /// <param name="runDataChangedNotifier">Benachrichtigt über geänderte Laufdaten.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="startCheckTrigger">Optionaler Auslöser der SCM-Startprüfung;
    /// wird benachrichtigt, wenn das Session-Limit Aufgaben pausiert.</param>
    public AiPluginLimitService(
        SoftwareschmiedeDbContext db,
        AppSettingService appSettingService,
        WorkItemRunDataChangedNotifier runDataChangedNotifier,
        ILogger<AiPluginLimitService> logger,
        ScmStartCheckTrigger? startCheckTrigger = null)
    {
        _db = db;
        _appSettingService = appSettingService;
        _runDataChangedNotifier = runDataChangedNotifier;
        _logger = logger;
        _startCheckTrigger = startCheckTrigger;
    }

    /// <summary>
    /// Verarbeitet einen erkannten Session-Limit-Marker: Persistiert den Reset-Zeitpunkt
    /// unter <c>plugins.sessionlimit.&lt;KiPluginPrefix&gt;</c> der auslösenden Aufgabe und
    /// pausiert bei zukünftigem Zeitpunkt alle aktiv laufenden regulären Aufgaben mit
    /// demselben Prefix (pro pausierter Aufgabe ein <see cref="LogEntryType.SystemMessage"/>-Eintrag).
    /// Laufende CLI-Prozesse werden nicht angefasst.
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, in deren CLI-Ausgabe der Marker erkannt wurde.</param>
    /// <param name="resetUtc">Gemeldeter Reset-Zeitpunkt des Session-Limits.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task ProcessRateLimitAsync(Guid workItemId, DateTimeOffset resetUtc, CancellationToken ct = default)
    {
        var triggeringWorkItem = await _db.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == workItemId, ct);

        var prefix = triggeringWorkItem?.AiPluginPrefix;
        if (triggeringWorkItem is null || string.IsNullOrWhiteSpace(prefix))
        {
            _logger.LogWarning(
                "Rate-Limit-Marker für Aufgabe {AufgabeId} ohne KiPluginPrefix erkannt — kein Plugin-Limit wird persistiert.",
                workItemId);
            return;
        }

        var resetUtcNormalized = resetUtc.ToUniversalTime();
        await _appSettingService.SetSettingAsync(
            SessionLimitKeyPrefix + prefix,
            resetUtcNormalized.ToString("O", CultureInfo.InvariantCulture),
            ct);

        var now = DateTimeOffset.UtcNow;
        if (resetUtcNormalized <= now)
        {
            // Berichtetes, bereits abgelaufenes Limit wird zwar persistiert, löst aber keine Pause aus.
            _logger.LogInformation(
                "Session-Limit für Plugin {Prefix} auf {ResetUtc} persistiert (bereits abgelaufen — keine Pause).",
                prefix,
                resetUtcNormalized.ToString("O"));
            return;
        }

        var candidates = await _db.WorkItems
            .Where(a => WorkItemStatusExtensions.ActiveOrWaitingStatuses.Contains(a.Status)
                && a.ExecutionStatus == WorkItemExecutionStatus.Active
                && a.AiPluginPrefix != null)
            .ToListAsync(ct);

        var betroffene = candidates
            .Where(a => string.Equals(a.AiPluginPrefix, prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (betroffene.Count == 0)
        {
            _logger.LogInformation(
                "Session-Limit für Plugin {Prefix} auf {ResetUtc} persistiert — keine aktiv laufenden Aufgaben betroffen.",
                prefix,
                resetUtcNormalized.ToString("O"));
            return;
        }

        foreach (var workItem in betroffene)
        {
            // Max-Semantik: Eine manuell gesetzte, später endende Pause darf durch einen
            // früheren Session-Limit-Reset nicht verkürzt werden.
            var effektiv = workItem.PausedUntilUtc is { } existing && existing > resetUtcNormalized
                ? existing
                : resetUtcNormalized;
            workItem.PausedUntilUtc = effektiv;
            _db.LogEntries.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItem.Id,
                Type = LogEntryType.SystemMessage,
                Content = $"Aufgabe pausiert bis {effektiv:O} (Session-Limit des KI-Plugins '{prefix}').",
                Timestamp = now
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Session-Limit für Plugin {Prefix} auf {ResetUtc} persistiert; {Count} Aufgabe(n) pausiert.",
            prefix,
            resetUtcNormalized.ToString("O"),
            betroffene.Count);

        foreach (var workItem in betroffene)
        {
            _runDataChangedNotifier.NotifyRunDataChanged(workItem.Id);
        }

        // Pausen-Setzpfad der SCM-Startprüfung: Die Prüfung bewertet Priorisierung/Freigabe
        // sofort neu statt erst beim nächsten Poll.
        _startCheckTrigger?.RequestStartCheck();
    }

    /// <summary>
    /// Liest die persistierten Session-Limits mehrerer Plugin-Prefixe in einer Abfrage.
    /// Abgelaufene oder ungültig gespeicherte Werte werden nicht zurückgegeben.
    /// </summary>
    /// <param name="prefixes">Die zu prüfenden <see cref="WorkItem.AiPluginPrefix"/>-Werte.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Zuordnung Prefix → Reset-Zeitpunkt (nur zukünftige Limits; Schlüsselvergleich OrdinalIgnoreCase).</returns>
    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetAktiveSessionLimitsAsync(
        IReadOnlyCollection<string> prefixes,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        if (prefixes.Count == 0)
            return result;

        var keys = prefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .Select(p => SessionLimitKeyPrefix + p)
            .ToList();
        if (keys.Count == 0)
            return result;

        var settings = await _appSettingService.GetSettingsAsync(keys, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var (schluessel, value) in settings)
        {
            if (TryParseLimitValue(value, now, out var limit))
            {
                result[schluessel[SessionLimitKeyPrefix.Length..]] = limit;
            }
        }

        return result;
    }

    private static bool TryParseLimitValue(string? value, DateTimeOffset now, out DateTimeOffset limit)
    {
        limit = default;
        if (string.IsNullOrWhiteSpace(value)
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
            || parsed <= now)
        {
            return false;
        }

        limit = parsed;
        return true;
    }
}
