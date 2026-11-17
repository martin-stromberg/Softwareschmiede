using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Belegung laufender CLI-Ausführungen für die Ausführungsbegrenzung.</summary>
/// <param name="Global">Anzahl aller Aufgaben mit laufender CLI-Ausführung.</param>
/// <param name="ProPlugin">Anzahl laufender Ausführungen je zugeordnetem KI-Plugin-Prefix.</param>
public sealed record CliAusfuehrungsBelegung(int Global, IReadOnlyDictionary<string, int> ProPlugin)
{
    /// <summary>Anzahl laufender Ausführungen, die dem angegebenen Plugin-Prefix zugeordnet sind.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix (Groß-/Kleinschreibung wird ignoriert).</param>
    /// <returns>Anzahl laufender Ausführungen des Plugins; <c>0</c>, wenn keine bekannt sind.</returns>
    public int FuerPlugin(string pluginPrefix)
        => !string.IsNullOrWhiteSpace(pluginPrefix) && ProPlugin.TryGetValue(pluginPrefix, out var anzahl) ? anzahl : 0;
}

/// <summary>
/// Zulassungskontrolle für neue CLI-Ausführungen aus dem SCM-Ereignis-Dispatch:
/// Zählt die aktuell laufenden CLI-Ausführungen global und je zugeordnetem KI-Plugin und
/// vergleicht sie mit den konfigurierten Grenzen. Laufende Ausführungen werden niemals
/// unterbrochen; die Prüfung entscheidet ausschließlich über den Start neuer Ausführungen.
/// Grenzen: leer/unset/ungültig = unbegrenzt; gültige Werte sind Ganzzahlen ≥ 1.
/// </summary>
public sealed class ScmEventExecutionLimitService(
    SoftwareschmiededDbContext db,
    AppEinstellungService einstellungen,
    ICliExecutionLimitSource cli)
{
    /// <summary>Einstellungsschlüssel des globalen Limits paralleler CLI-Ausführungen.</summary>
    public const string GlobalLimitKey = "scm.execution.maxparallel";

    /// <summary>Schlüsselpräfix der Plugin-Limits paralleler CLI-Ausführungen (<c>plugins.maxparallel.&lt;prefix&gt;</c>).</summary>
    public const string PluginLimitKeyPrefix = "plugins.maxparallel.";

    /// <summary>Einstellungsschlüssel des Parallel-Limits eines konkreten KI-Plugins.</summary>
    /// <param name="pluginPrefix">KI-Plugin-Prefix.</param>
    /// <returns>Vollständiger Einstellungsschlüssel.</returns>
    public static string PluginLimitKey(string pluginPrefix) => PluginLimitKeyPrefix + pluginPrefix;

    /// <summary>
    /// Prüft, ob für einen neuen CLI-Start mit dem gegebenen Plugin-Prefix noch Platz ist.
    /// Das globale und das Plugin-Limit gelten gleichzeitig; das jeweils restriktivere
    /// entscheidet. Attribuiert werden lebende SCM-Sitzungen über ihr tatsächlich gestartetes
    /// Plugin (<see cref="IScmPromptSessionAdapter.PluginPrefix"/>), übrige laufende Handles
    /// über <c>Aufgabe.KiPluginPrefix</c>.
    /// </summary>
    /// <param name="pluginPrefix">Prefix des zu startenden KI-Plugins.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><c>null</c>, wenn ein Platz frei ist; sonst ein benutzerlesbarer Wartegrund.</returns>
    public async Task<string?> GetStartBlockReasonAsync(string pluginPrefix, CancellationToken ct = default)
    {
        var keys = new List<string> { GlobalLimitKey };
        if (!string.IsNullOrWhiteSpace(pluginPrefix))
            keys.Add(PluginLimitKey(pluginPrefix));
        var werte = await einstellungen.GetSettingsAsync(keys, ct);
        var globalLimit = ParseLimit(werte.TryGetValue(GlobalLimitKey, out var g) ? g : null);
        var pluginLimit = !string.IsNullOrWhiteSpace(pluginPrefix)
            ? ParseLimit(werte.TryGetValue(PluginLimitKey(pluginPrefix), out var p) ? p : null)
            : null;
        if (globalLimit is null && pluginLimit is null)
            return null;
        return GetStartBlockReason(pluginPrefix, globalLimit, pluginLimit, await GetBelegungAsync(ct));
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

    private static int? ParseLimit(string? wert)
        => int.TryParse(wert, out var limit) && limit >= 1 ? limit : null;

    private async Task<CliAusfuehrungsBelegung> GetBelegungAsync(CancellationToken ct)
    {
        var laufendeIds = cli.GetLaufendeAufgabenIds();
        if (laufendeIds.Count == 0)
            return new(0, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        var prefixes = await db.Aufgaben.AsNoTracking()
            .Where(x => laufendeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.KiPluginPrefix })
            .ToListAsync(ct);
        var proPlugin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var aufgabe in prefixes)
        {
            var session = cli.GetScmPromptSession(aufgabe.Id);
            var prefix = session?.IsAlive == true && !string.IsNullOrWhiteSpace(session.PluginPrefix)
                ? session.PluginPrefix
                : aufgabe.KiPluginPrefix;
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            proPlugin.TryGetValue(prefix, out var anzahl);
            proPlugin[prefix] = anzahl + 1;
        }
        return new(laufendeIds.Count, proPlugin);
    }
}
