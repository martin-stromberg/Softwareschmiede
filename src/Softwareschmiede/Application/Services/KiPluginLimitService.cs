using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service Layer für KI-Plugin-Session-Limits. Persistiert den vom Plugin gemeldeten
/// Reset-Zeitpunkt pro <see cref="Aufgabe.KiPluginPrefix"/> als <see cref="AppEinstellung"/>
/// (Schlüssel <c>plugins.sessionlimit.&lt;Prefix&gt;</c>) und wendet die Pause auf alle
/// aktiv ausgeführten regulären Aufgaben desselben Prefix an — ohne laufende CLI-Prozesse
/// zu unterbrechen.
/// </summary>
public sealed class KiPluginLimitService
{
    /// <summary>Schlüsselpräfix für persistierte Session-Limit-Reset-Zeitpunkte in <see cref="AppEinstellung"/>.</summary>
    public const string SessionLimitKeyPrefix = "plugins.sessionlimit.";

    private readonly SoftwareschmiededDbContext _db;
    private readonly AppEinstellungService _appEinstellungService;
    private readonly AufgabeLaufdatenChangedNotifier _laufdatenChangedNotifier;
    private readonly ILogger<KiPluginLimitService> _logger;

    /// <inheritdoc cref="KiPluginLimitService"/>
    public KiPluginLimitService(
        SoftwareschmiededDbContext db,
        AppEinstellungService appEinstellungService,
        AufgabeLaufdatenChangedNotifier laufdatenChangedNotifier,
        ILogger<KiPluginLimitService> logger)
    {
        _db = db;
        _appEinstellungService = appEinstellungService;
        _laufdatenChangedNotifier = laufdatenChangedNotifier;
        _logger = logger;
    }

    /// <summary>
    /// Verarbeitet einen erkannten Session-Limit-Marker: Persistiert den Reset-Zeitpunkt
    /// unter <c>plugins.sessionlimit.&lt;KiPluginPrefix&gt;</c> der auslösenden Aufgabe und
    /// pausiert bei zukünftigem Zeitpunkt alle aktiv laufenden regulären Aufgaben mit
    /// demselben Prefix (pro pausierter Aufgabe ein <see cref="ProtokollTyp.SystemMeldung"/>-Eintrag).
    /// Laufende CLI-Prozesse werden nicht angefasst.
    /// </summary>
    /// <param name="aufgabeId">ID der Aufgabe, in deren CLI-Ausgabe der Marker erkannt wurde.</param>
    /// <param name="resetUtc">Gemeldeter Reset-Zeitpunkt des Session-Limits.</param>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task VerarbeiteRateLimitAsync(Guid aufgabeId, DateTimeOffset resetUtc, CancellationToken ct = default)
    {
        var ausloesendeAufgabe = await _db.Aufgaben
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == aufgabeId, ct);

        var prefix = ausloesendeAufgabe?.KiPluginPrefix;
        if (ausloesendeAufgabe is null || string.IsNullOrWhiteSpace(prefix))
        {
            _logger.LogWarning(
                "Rate-Limit-Marker für Aufgabe {AufgabeId} ohne KiPluginPrefix erkannt — kein Plugin-Limit wird persistiert.",
                aufgabeId);
            return;
        }

        var resetUtcNormalized = resetUtc.ToUniversalTime();
        await _appEinstellungService.SetSettingAsync(
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

        var kandidaten = await _db.Aufgaben
            .Where(a => AufgabeStatusExtensions.AktivOderWartendStatus.Contains(a.Status)
                && a.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv
                && a.AutonomKonfiguration == null
                && a.KiPluginPrefix != null)
            .ToListAsync(ct);

        var betroffene = kandidaten
            .Where(a => string.Equals(a.KiPluginPrefix, prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (betroffene.Count == 0)
        {
            _logger.LogInformation(
                "Session-Limit für Plugin {Prefix} auf {ResetUtc} persistiert — keine aktiv laufenden Aufgaben betroffen.",
                prefix,
                resetUtcNormalized.ToString("O"));
            return;
        }

        foreach (var aufgabe in betroffene)
        {
            // Max-Semantik: Eine manuell gesetzte, später endende Pause darf durch einen
            // früheren Session-Limit-Reset nicht verkürzt werden.
            var effektiv = aufgabe.PausiertBisUtc is { } vorhanden && vorhanden > resetUtcNormalized
                ? vorhanden
                : resetUtcNormalized;
            aufgabe.PausiertBisUtc = effektiv;
            _db.Protokolleintraege.Add(new Protokolleintrag
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabe.Id,
                Typ = ProtokollTyp.SystemMeldung,
                Inhalt = $"Aufgabe pausiert bis {effektiv:O} (Session-Limit des KI-Plugins '{prefix}').",
                Zeitstempel = now
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Session-Limit für Plugin {Prefix} auf {ResetUtc} persistiert; {Count} Aufgabe(n) pausiert.",
            prefix,
            resetUtcNormalized.ToString("O"),
            betroffene.Count);

        foreach (var aufgabe in betroffene)
        {
            _laufdatenChangedNotifier.NotifyLaufdatenChanged(aufgabe.Id);
        }
    }

    /// <summary>
    /// Liest die persistierten Session-Limits mehrerer Plugin-Prefixe in einer Abfrage.
    /// Abgelaufene oder ungültig gespeicherte Werte werden nicht zurückgegeben.
    /// </summary>
    /// <param name="prefixes">Die zu prüfenden <see cref="Aufgabe.KiPluginPrefix"/>-Werte.</param>
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

        var einstellungen = await _appEinstellungService.GetSettingsAsync(keys, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var (schluessel, wert) in einstellungen)
        {
            if (TryParseLimitWert(wert, now, out var limit))
            {
                result[schluessel[SessionLimitKeyPrefix.Length..]] = limit;
            }
        }

        return result;
    }

    private static bool TryParseLimitWert(string? wert, DateTimeOffset now, out DateTimeOffset limit)
    {
        limit = default;
        if (string.IsNullOrWhiteSpace(wert)
            || !DateTimeOffset.TryParse(
                wert,
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
