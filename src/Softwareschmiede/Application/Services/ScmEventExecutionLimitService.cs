using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Belegung laufender CLI-Ausführungen für die Ausführungsbegrenzung.
/// Pausierte Aufgaben (<c>Aufgabe.PausiertBisUtc</c> in der Zukunft) belegen trotz
/// weiterlaufendem Prozess keinen Platz und fehlen in beiden Zählern.</summary>
/// <param name="Global">Anzahl aller Aufgaben mit laufender, nicht pausierter CLI-Ausführung
/// (inklusive laufender Handles ohne auflösbare Aufgabenzeile).</param>
/// <param name="ProPlugin">Anzahl laufender, nicht pausierter Ausführungen je zugeordnetem KI-Plugin-Prefix.</param>
public sealed record CliAusfuehrungsBelegung(int Global, IReadOnlyDictionary<string, int> ProPlugin)
{
    /// <summary>Anzahl laufender Ausführungen, die dem angegebenen Plugin-Prefix zugeordnet sind.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix (Groß-/Kleinschreibung wird ignoriert).</param>
    /// <returns>Anzahl laufender Ausführungen des Plugins; <c>0</c>, wenn keine bekannt sind.</returns>
    public int FuerPlugin(string pluginPrefix)
        => !string.IsNullOrWhiteSpace(pluginPrefix) && ProPlugin.TryGetValue(pluginPrefix, out var anzahl) ? anzahl : 0;
}

/// <summary>Ergebnis der atomaren Zulassungsprüfung für einen neuen CLI-Start.</summary>
/// <param name="Wartegrund">Benutzerlesbarer Wartegrund bei erreichtem Limit; <c>null</c>, wenn ein Platz frei ist.</param>
/// <param name="Reservierung">
/// Belegter Startplatz für die Dauer bis zur Prozess-Handle-Registrierung; <c>null</c> bei
/// vollem Limit (<paramref name="Wartegrund"/> gesetzt) oder bei unbegrenzter Konfiguration.
/// </param>
public sealed record CliStartPlatzZulassung(string? Wartegrund, IDisposable? Reservierung);

/// <summary>
/// Zulassungskontrolle für neue CLI-Ausführungen aus dem SCM-Ereignis-Dispatch:
/// Zählt die aktuell laufenden CLI-Ausführungen global und je zugeordnetem KI-Plugin und
/// vergleicht sie mit den konfigurierten Grenzen. Laufende Ausführungen werden niemals
/// unterbrochen; die Prüfung entscheidet ausschließlich über den Start neuer Ausführungen.
/// Grenzen: leer/unset/ungültig = unbegrenzt; gültige Werte sind Ganzzahlen ≥ 1.
/// </summary>
/// <remarks>
/// <see cref="PruefeUndReserviereStartPlatzAsync"/> verbindet Limit-Prüfung und Platzbelegung
/// zu einer atomaren Operation: Der reservierte Platz zählt sofort für jeden weiteren
/// Zulassungsversuch, sodass nebenläufige Dispatches verschiedener Aufgaben (Polling, Queue,
/// UI-Aktionen) einander nicht zwischen Prüfung und Prozessstart überholen können. Die
/// Reservierung wird nach erfolgreicher Handle-Registrierung von der echten Laufzählung
/// abgelöst, bei jedem Fehlschlag vor dem Start freigegeben und verfällt spätestens nach
/// <see cref="ReservierungsLebensdauer"/>, falls der Start hängenbleibt. Reservierungen liegen
/// prozessweit (der Service selbst ist scoped), damit alle Dispatch-Pfade denselben Bestand sehen.
/// </remarks>
public sealed class ScmEventExecutionLimitService(
    SoftwareschmiededDbContext db,
    AppEinstellungService einstellungen,
    ICliExecutionLimitSource cli,
    TimeProvider time)
{
    /// <summary>Einstellungsschlüssel des globalen Limits paralleler CLI-Ausführungen.</summary>
    public const string GlobalLimitKey = "scm.execution.maxparallel";

    /// <summary>Schlüsselpräfix der Plugin-Limits paralleler CLI-Ausführungen (<c>plugins.maxparallel.&lt;prefix&gt;</c>).</summary>
    public const string PluginLimitKeyPrefix = "plugins.maxparallel.";

    /// <summary>Lebensdauer einer Startplatz-Reservierung; danach verfällt der Platz,
    /// falls der Start zwischen Reservierung und Handle-Registrierung hängenbleibt.</summary>
    internal static readonly TimeSpan ReservierungsLebensdauer = TimeSpan.FromMinutes(5);

    // Prozessweiter Bestand und Sperren: Der Service ist scoped, die Zulassung muss aber
    // über alle DI-Scopes und Dispatch-Pfade hinweg denselben Platzbestand sehen.
    // ZulassungsGate serialisiert den gesamten Check-und-Claim-Abschnitt über die
    // Belegungsabfrage hinweg; ReservierungenSync schützt die Map selbst, damit die
    // Freigabe in Dispose non-blocking bleibt (kein Warten auf dem UI-Kontext).
    private static readonly SemaphoreSlim ZulassungsGate = new(1, 1);
    private static readonly object ReservierungenSync = new();
    private static readonly Dictionary<Guid, StartPlatzReservierung> Reservierungen = new();

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
    /// <returns>Entweder eine freizugebende <see cref="CliStartPlatzZulassung.Reservierung"/> oder
    /// einen benutzerlesbaren <see cref="CliStartPlatzZulassung.Wartegrund"/>; bei unbegrenzter
    /// Konfiguration ist beides <c>null</c>.</returns>
    public async Task<CliStartPlatzZulassung> PruefeUndReserviereStartPlatzAsync(string pluginPrefix, CancellationToken ct = default)
    {
        var (globalLimit, pluginLimit) = await GetLimitsAsync(pluginPrefix, ct);
        if (globalLimit is null && pluginLimit is null)
            return new CliStartPlatzZulassung(null, null);
        await ZulassungsGate.WaitAsync(ct);
        try
        {
            // Check + Claim unter derselben Sperre: Auch die Handle-Belegung wird erst hier
            // gelesen, damit die Momentaufnahme nicht zwischen Prüfung und Platzbelegung
            // veralten kann. Starts, die die Zulassungskontrolle gar nicht passieren (z. B.
            // manuelle CLI-Starts im Aufgabenfenster), unterliegen dem Limit bewusst nicht.
            var wartegrund = GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, await WirksameBelegungAsync(ct));
            if (wartegrund is not null)
                return new CliStartPlatzZulassung(wartegrund, null);
            var reservierung = new StartPlatzReservierung(pluginPrefix,
                time.GetUtcNow().ToUnixTimeMilliseconds() + (long)ReservierungsLebensdauer.TotalMilliseconds);
            lock (ReservierungenSync)
                Reservierungen[reservierung.Id] = reservierung;
            return new CliStartPlatzZulassung(null, reservierung);
        }
        finally
        {
            ZulassungsGate.Release();
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
        await ZulassungsGate.WaitAsync(ct);
        try
        {
            return GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, await WirksameBelegungAsync(ct));
        }
        finally
        {
            ZulassungsGate.Release();
        }
    }

    /// <summary>Reine Auswertung: globales Limit zuerst, danach das Plugin-Limit.</summary>
    /// <param name="pluginPrefix">Prefix des zu startenden KI-Plugins.</param>
    /// <param name="globalLimit">Konfiguriertes globales Limit oder <c>null</c> (unbegrenzt).</param>
    /// <param name="pluginLimit">Konfiguriertes Plugin-Limit oder <c>null</c> (unbegrenzt).</param>
    /// <param name="belegung">Aktuelle Belegung laufender Ausführungen.</param>
    /// <returns><c>null</c>, wenn ein Platz frei ist; sonst ein benutzerlesbarer Wartegrund.</returns>
    internal static string? GetStartBlockReason(string? pluginPrefix, int? globalLimit, int? pluginLimit,
        CliAusfuehrungsBelegung belegung)
    {
        if (globalLimit is { } g && belegung.Global >= g)
            return $"Das globale Limit paralleler CLI-Ausführungen ({g}) ist erreicht; der Start erfolgt, sobald ein Platz frei wird.";
        if (pluginLimit is { } p && !string.IsNullOrWhiteSpace(pluginPrefix) && belegung.FuerPlugin(pluginPrefix) >= p)
            return $"Das Limit paralleler CLI-Ausführungen für die CLI '{pluginPrefix}' ({p}) ist erreicht; der Start erfolgt, sobald ein Platz frei wird.";
        return null;
    }

    /// <summary>Test-Hilfsmittel: Gibt alle offenen Reservierungen prozessweit frei.</summary>
    internal static void ReservierungenZuruecksetzen()
    {
        lock (ReservierungenSync)
            Reservierungen.Clear();
    }

    /// <summary>Liest beide wirksamen Grenzen für einen Prefix in einem einzigen Einstellungszugriff.</summary>
    private async Task<(int? Global, int? Plugin)> GetLimitsAsync(string? pluginPrefix, CancellationToken ct)
    {
        var keys = new List<string> { GlobalLimitKey };
        if (!string.IsNullOrWhiteSpace(pluginPrefix))
            keys.Add(PluginLimitKey(pluginPrefix));
        var werte = await einstellungen.GetSettingsAsync(keys, ct);
        var globalLimit = ParseLimit(werte.TryGetValue(GlobalLimitKey, out var g) ? g : null);
        var pluginLimit = !string.IsNullOrWhiteSpace(pluginPrefix)
            ? ParseLimit(werte.TryGetValue(PluginLimitKey(pluginPrefix), out var p) ? p : null)
            : null;
        return (globalLimit, pluginLimit);
    }

    /// <summary>Handle-Belegung plus offene Reservierungen; verfallene Reservierungen werden
    /// vor der Zählung entfernt. Muss unter <see cref="ZulassungsGate"/> aufgerufen werden.</summary>
    private async Task<CliAusfuehrungsBelegung> WirksameBelegungAsync(CancellationToken ct)
    {
        var belegung = await GetBelegungAsync(ct);
        List<StartPlatzReservierung> aktive;
        lock (ReservierungenSync)
        {
            var jetzt = time.GetUtcNow().ToUnixTimeMilliseconds();
            foreach (var id in Reservierungen.Where(x => x.Value.GueltigBisUtcMs <= jetzt).Select(x => x.Key).ToList())
                Reservierungen.Remove(id);
            aktive = Reservierungen.Values.ToList();
        }
        if (aktive.Count == 0)
            return belegung;
        var proPlugin = new Dictionary<string, int>(belegung.ProPlugin, StringComparer.OrdinalIgnoreCase);
        foreach (var reservierung in aktive)
        {
            if (string.IsNullOrWhiteSpace(reservierung.PluginPrefix)) continue;
            proPlugin.TryGetValue(reservierung.PluginPrefix, out var anzahl);
            proPlugin[reservierung.PluginPrefix] = anzahl + 1;
        }
        return new(belegung.Global + aktive.Count, proPlugin);
    }

    private static int? ParseLimit(string? wert)
        => int.TryParse(wert, out var limit) && limit >= 1 ? limit : null;

    /// <summary>Zählt die laufenden CLI-Handles global und je Plugin. Pausierte Aufgaben
    /// (<c>PausiertBisUtc</c> in der Zukunft — ihr Prozess läuft bewusst weiter) belegen keinen
    /// Platz und werden vor der globalen Zählung und der Plugin-Attribution herausgefiltert;
    /// abgelaufene oder fehlende Pausenzeiten sowie Handles ohne Aufgabenzeile zählen voll.</summary>
    private async Task<CliAusfuehrungsBelegung> GetBelegungAsync(CancellationToken ct)
    {
        var laufendeIds = cli.GetLaufendeAufgabenIds();
        if (laufendeIds.Count == 0)
            return new(0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        var aufgaben = await db.Aufgaben.AsNoTracking()
            .Where(x => laufendeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.KiPluginPrefix, x.PausiertBisUtc })
            .ToListAsync(ct);
        var jetzt = time.GetUtcNow();
        var aktive = aufgaben.Where(x => !x.PausiertBisUtc.IstAktuellPausiert(jetzt)).ToList();
        var proPlugin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var aufgabe in aktive)
        {
            var session = cli.GetScmPromptSession(aufgabe.Id);
            var prefix = session?.IsAlive == true && !string.IsNullOrWhiteSpace(session.PluginPrefix)
                ? session.PluginPrefix
                : aufgabe.KiPluginPrefix;
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            proPlugin.TryGetValue(prefix, out var anzahl);
            proPlugin[prefix] = anzahl + 1;
        }
        // Global = aktive Aufgaben plus laufende Handles ohne auflösbare Aufgabenzeile
        // (letztere haben kein PausiertBisUtc und zählen bewusst weiterhin voll).
        return new(aktive.Count + (laufendeIds.Count - aufgaben.Count), proPlugin);
    }

    /// <summary>Reservierter Startplatz; die Freigabe über <see cref="Dispose"/> entfernt den
    /// Platz aus der Zählung, sobald die echte Laufzählung ihn übernommen hat oder der Start
    /// fehlgeschlagen ist. Die Freigabe ist bewusst non-blocking (kurzer Lock, kein Warten).</summary>
    private sealed class StartPlatzReservierung(string pluginPrefix, long gueltigBisUtcMs) : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string PluginPrefix { get; } = pluginPrefix;
        public long GueltigBisUtcMs { get; } = gueltigBisUtcMs;

        public void Dispose()
        {
            lock (ReservierungenSync)
                Reservierungen.Remove(Id);
        }
    }
}
