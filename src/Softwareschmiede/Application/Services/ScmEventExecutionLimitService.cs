using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Belegung laufender CLI-Ausführungen für die Ausführungsbegrenzung.
/// Pausierte Aufgaben (<c>Aufgabe.PausiertBisUtc</c> in der Zukunft) belegen trotz
/// weiterlaufendem Prozess keinen Platz und fehlen in beiden Zählern.</summary>
/// <param name="Global">Anzahl aller Aufgaben mit laufender, nicht pausierter CLI-Ausführung
/// (inklusive laufender Handles ohne auflösbare Aufgabenzeile).</param>
/// <param name="ProPlugin">Anzahl laufender, nicht pausierter Ausführungen je zugeordnetem KI-Plugin-Prefix.</param>
public sealed record CliExecutionOccupancy(int Global, IReadOnlyDictionary<string, int> ProPlugin)
{
    /// <summary>Anzahl laufender Ausführungen, die dem angegebenen Plugin-Prefix zugeordnet sind.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix (Groß-/Kleinschreibung wird ignoriert).</param>
    /// <returns>Anzahl laufender Ausführungen des Plugins; <c>0</c>, wenn keine bekannt sind.</returns>
    public int ForPlugin(string pluginPrefix)
        => !string.IsNullOrWhiteSpace(pluginPrefix) && ProPlugin.TryGetValue(pluginPrefix, out var count) ? count : 0;
}

/// <summary>Ergebnis der atomaren Zulassungsprüfung für einen neuen CLI-Start.</summary>
/// <param name="WaitReason">Benutzerlesbarer Wartegrund bei erreichtem Limit; <c>null</c>, wenn ein Platz frei ist.</param>
/// <param name="Reservation">
/// Belegter Startplatz für die Dauer bis zur Prozess-Handle-Registrierung; <c>null</c> bei
/// vollem Limit (<paramref name="WaitReason"/> gesetzt) oder bei unbegrenzter Konfiguration.
/// </param>
public sealed record CliStartSlotAdmission(string? WaitReason, IDisposable? Reservation);

/// <summary>
/// Zulassungskontrolle für neue CLI-Ausführungen aus dem SCM-Ereignis-Dispatch:
/// Zählt die aktuell laufenden CLI-Ausführungen global und je zugeordnetem KI-Plugin und
/// vergleicht sie mit den konfigurierten Grenzen. Laufende Ausführungen werden niemals
/// unterbrochen; die Prüfung entscheidet ausschließlich über den Start neuer Ausführungen.
/// Grenzen: leer/unset/ungültig = unbegrenzt; gültige Werte sind Ganzzahlen ≥ 1.
/// </summary>
/// <remarks>
/// <see cref="CheckAndReserveStartSlotAsync"/> verbindet Limit-Prüfung und Platzbelegung
/// zu einer atomaren Operation: Der reservierte Platz zählt sofort für jeden weiteren
/// Zulassungsversuch, sodass nebenläufige Dispatches verschiedener Aufgaben (Polling, Queue,
/// UI-Aktionen) einander nicht zwischen Prüfung und Prozessstart überholen können. Die
/// Reservierung wird nach erfolgreicher Handle-Registrierung von der echten Laufzählung
/// abgelöst, bei jedem Fehlschlag vor dem Start freigegeben und verfällt spätestens nach
/// <see cref="ReservationLifetime"/>, falls der Start hängenbleibt. Reservierungen liegen
/// im <see cref="ScmStartSlotReservationPool"/> (DI-Singleton; der Service selbst ist scoped),
/// damit alle Dispatch-Pfade desselben Providers denselben Bestand sehen.
/// </remarks>
public sealed class ScmEventExecutionLimitService(
    SoftwareschmiedeDbContext db,
    AppSettingService settings,
    ICliExecutionLimitSource cli,
    TimeProvider time,
    ScmStartSlotReservationPool reservationPool)
{
    /// <summary>Einstellungsschlüssel des globalen Limits paralleler CLI-Ausführungen.</summary>
    public const string GlobalLimitKey = "scm.execution.maxparallel";

    /// <summary>Schlüsselpräfix der Plugin-Limits paralleler CLI-Ausführungen (<c>plugins.maxparallel.&lt;prefix&gt;</c>).</summary>
    public const string PluginLimitKeyPrefix = "plugins.maxparallel.";

    /// <summary>Lebensdauer einer Startplatz-Reservierung; danach verfällt der Platz,
    /// falls der Start zwischen Reservierung und Handle-Registrierung hängenbleibt.</summary>
    internal static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Einstellungsschlüssel des Parallel-Limits eines konkreten KI-Plugins.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix.</param>
    /// <returns>Vollständiger Einstellungsschlüssel.</returns>
    public static string PluginLimitKey(string pluginPrefix) => PluginLimitKeyPrefix + pluginPrefix;

    /// <summary>
    /// Prüft atomar, ob für einen neuen CLI-Start mit dem gegebenen Plugin-Prefix noch Platz
    /// ist, und belegt den Platz bis zur Handle-Registrierung. Zwischen dieser Zulassung und
    /// der Zählung des gestarteten Prozesses kann kein zweiter Start mehr dazwischenrutschen.
    /// Aufrufer müssen die Reservierung nach dem Startversuch freigeben (bei Erfolg hat die
    /// echte Laufzählung den Platz bereits übernommen); vergessene Reservierungen verfallen.
    /// </summary>
    /// <param name="pluginPrefix">Prefix des zu startenden KI-Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Entweder eine freizugebende <see cref="CliStartSlotAdmission.Reservation"/> oder
    /// einen benutzerlesbaren <see cref="CliStartSlotAdmission.WaitReason"/>; bei unbegrenzter
    /// Konfiguration ist beides <c>null</c>.</returns>
    public async Task<CliStartSlotAdmission> CheckAndReserveStartSlotAsync(string pluginPrefix, CancellationToken ct = default)
    {
        var (globalLimit, pluginLimit) = await GetLimitsAsync(pluginPrefix, ct);
        if (globalLimit is null && pluginLimit is null)
            return new CliStartSlotAdmission(null, null);
        await reservationPool.AdmissionGate.WaitAsync(ct);
        try
        {
            // Check + Claim unter derselben Sperre: Auch die Handle-Belegung wird erst hier
            // gelesen, damit die Momentaufnahme nicht zwischen Prüfung und Platzbelegung
            // veralten kann. Starts, die die Zulassungskontrolle gar nicht passieren (z. B.
            // manuelle CLI-Starts im Aufgabenfenster), unterliegen dem Limit bewusst nicht.
            var waitReason = GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, await EffectiveOccupancyAsync(ct));
            if (waitReason is not null)
                return new CliStartSlotAdmission(waitReason, null);
            var reservation = reservationPool.Reserve(pluginPrefix,
                time.GetUtcNow().ToUnixTimeMilliseconds() + (long)ReservationLifetime.TotalMilliseconds);
            return new CliStartSlotAdmission(null, reservation);
        }
        finally
        {
            reservationPool.AdmissionGate.Release();
        }
    }

    /// <summary>
    /// Prüft, ob für einen neuen CLI-Start mit dem gegebenen Plugin-Prefix noch Platz ist,
    /// ohne einen Platz zu belegen. Das globale und das Plugin-Limit gelten gleichzeitig; das
    /// jeweils restriktivere entscheidet. Attribuiert werden lebende SCM-Sitzungen über ihr
    /// tatsächlich gestartetes Plugin (<see cref="IScmPromptSessionAdapter.PluginPrefix"/>),
    /// übrige laufende Handles über <c>Aufgabe.KiPluginPrefix</c>; noch nicht gestartete
    /// Reservierungen zählen wie ein laufender Platz.
    /// </summary>
    /// <param name="pluginPrefix">Prefix des zu startenden KI-Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><c>null</c>, wenn ein Platz frei ist; sonst ein benutzerlesbarer Wartegrund.</returns>
    public async Task<string?> GetStartBlockReasonAsync(string pluginPrefix, CancellationToken ct = default)
    {
        var (globalLimit, pluginLimit) = await GetLimitsAsync(pluginPrefix, ct);
        if (globalLimit is null && pluginLimit is null)
            return null;
        await reservationPool.AdmissionGate.WaitAsync(ct);
        try
        {
            return GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, await EffectiveOccupancyAsync(ct));
        }
        finally
        {
            reservationPool.AdmissionGate.Release();
        }
    }

    /// <summary>Reine Auswertung: globales Limit zuerst, danach das Plugin-Limit.</summary>
    /// <param name="pluginPrefix">Prefix des zu startenden KI-Plugins.</param>
    /// <param name="globalLimit">Konfiguriertes globales Limit oder <c>null</c> (unbegrenzt).</param>
    /// <param name="pluginLimit">Konfiguriertes Plugin-Limit oder <c>null</c> (unbegrenzt).</param>
    /// <param name="occupancy">Aktuelle Belegung laufender Ausführungen.</param>
    /// <returns><c>null</c>, wenn ein Platz frei ist; sonst ein benutzerlesbarer Wartegrund.</returns>
    internal static string? GetStartBlockReason(string? pluginPrefix, int? globalLimit, int? pluginLimit,
        CliExecutionOccupancy occupancy)
    {
        if (globalLimit is { } g && occupancy.Global >= g)
            return $"Das globale Limit paralleler CLI-Ausführungen ({g}) ist erreicht; der Start erfolgt, sobald ein Platz frei wird.";
        if (pluginLimit is { } p && !string.IsNullOrWhiteSpace(pluginPrefix) && occupancy.ForPlugin(pluginPrefix) >= p)
            return $"Das Limit paralleler CLI-Ausführungen für die CLI '{pluginPrefix}' ({p}) ist erreicht; der Start erfolgt, sobald ein Platz frei wird.";
        return null;
    }

    /// <summary>Liest beide wirksamen Grenzen für einen Prefix in einem einzigen Einstellungszugriff.</summary>
    private async Task<(int? Global, int? Plugin)> GetLimitsAsync(string? pluginPrefix, CancellationToken ct)
    {
        var keys = new List<string> { GlobalLimitKey };
        if (!string.IsNullOrWhiteSpace(pluginPrefix))
            keys.Add(PluginLimitKey(pluginPrefix));
        var values = await settings.GetSettingsAsync(keys, ct);
        var globalLimit = ParseLimit(values.TryGetValue(GlobalLimitKey, out var g) ? g : null);
        var pluginLimit = !string.IsNullOrWhiteSpace(pluginPrefix)
            ? ParseLimit(values.TryGetValue(PluginLimitKey(pluginPrefix), out var p) ? p : null)
            : null;
        return (globalLimit, pluginLimit);
    }

    /// <summary>Handle-Belegung plus offene Reservierungen; verfallene Reservierungen werden
    /// vor der Zählung entfernt. Muss unter <see cref="ScmStartSlotReservationPool.AdmissionGate"/>
    /// aufgerufen werden.</summary>
    private async Task<CliExecutionOccupancy> EffectiveOccupancyAsync(CancellationToken ct)
    {
        var occupancy = await GetOccupancyAsync(ct);
        var active = reservationPool.ActiveReservations(time.GetUtcNow().ToUnixTimeMilliseconds());
        if (active.Count == 0)
            return occupancy;
        var proPlugin = new Dictionary<string, int>(occupancy.ProPlugin, StringComparer.OrdinalIgnoreCase);
        foreach (var reservation in active)
        {
            if (string.IsNullOrWhiteSpace(reservation.PluginPrefix)) continue;
            proPlugin.TryGetValue(reservation.PluginPrefix, out var count);
            proPlugin[reservation.PluginPrefix] = count + 1;
        }
        return new(occupancy.Global + active.Count, proPlugin);
    }

    private static int? ParseLimit(string? value)
        => int.TryParse(value, out var limit) && limit >= 1 ? limit : null;

    /// <summary>Zählt die laufenden CLI-Handles global und je Plugin. Pausierte Aufgaben
    /// (<c>PausiertBisUtc</c> in der Zukunft — ihr Prozess läuft bewusst weiter) belegen keinen
    /// Platz und werden vor der globalen Zählung und der Plugin-Attribution herausgefiltert;
    /// abgelaufene oder fehlende Pausenzeiten sowie Handles ohne Aufgabenzeile zählen voll.</summary>
    private async Task<CliExecutionOccupancy> GetOccupancyAsync(CancellationToken ct)
    {
        var runningIds = cli.GetRunningWorkItemIds();
        if (runningIds.Count == 0)
            return new(0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        var workItems = await db.WorkItems.AsNoTracking()
            .Where(x => runningIds.Contains(x.Id))
            .Select(x => new { x.Id, x.AiPluginPrefix, x.PausedUntilUtc })
            .ToListAsync(ct);
        var now = time.GetUtcNow();
        var active = workItems.Where(x => !x.PausedUntilUtc.IsCurrentlyPaused(now)).ToList();
        var proPlugin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var workItem in active)
        {
            var session = cli.GetScmPromptSession(workItem.Id);
            var prefix = session?.IsAlive == true && !string.IsNullOrWhiteSpace(session.PluginPrefix)
                ? session.PluginPrefix
                : workItem.AiPluginPrefix;
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            proPlugin.TryGetValue(prefix, out var count);
            proPlugin[prefix] = count + 1;
        }
        // Global = active Aufgaben plus laufende Handles ohne auflösbare Aufgabenzeile
        // (letztere haben kein PausiertBisUtc und zählen bewusst weiterhin voll).
        return new(active.Count + (runningIds.Count - workItems.Count), proPlugin);
    }
}
