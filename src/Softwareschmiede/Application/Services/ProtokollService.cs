using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service zum Speichern und Abrufen von Protokolleinträgen.</summary>
public sealed class ProtokollService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<ProtokollService> _logger;

    /// <inheritdoc cref="ProtokollService"/>
    public ProtokollService(SoftwareschmiededDbContext db, ILogger<ProtokollService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Gibt alle Protokolleinträge einer Aufgabe chronologisch sortiert zurück (inkl. TestErgebnisse).</summary>
    public async Task<IReadOnlyList<Protokolleintrag>> GetByAufgabeAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleinträge für Aufgabe {AufgabeId} abrufen.", aufgabeId);
        return await _db.Protokolleintraege
            .AsNoTracking()
            .Include(p => p.TestErgebnisse)
            .Where(p => p.AufgabeId == aufgabeId)
            .OrderBy(p => p.Zeitstempel)
            .ToListAsync(ct);
    }

    /// <summary>Fügt einen neuen Protokolleintrag hinzu.</summary>
    public async Task<Protokolleintrag> AddEintragAsync(
        Guid aufgabeId,
        ProtokollTyp typ,
        string inhalt,
        string? agentName = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleintrag (Typ: {Typ}) für Aufgabe {AufgabeId} hinzufügen.", typ, aufgabeId);

        var eintrag = new Protokolleintrag
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            Typ = typ,
            Inhalt = inhalt,
            AgentName = agentName,
            Zeitstempel = DateTimeOffset.UtcNow
        };

        _db.Protokolleintraege.Add(eintrag);
        await _db.SaveChangesAsync(ct);

        return eintrag;
    }

    /// <summary>Erstellt einen Protokolleintrag vom Typ TestErgebnis und fügt TestErgebnis-Einträge hinzu.</summary>
    public async Task<Protokolleintrag> AddTestErgebnisseAsync(
        Guid aufgabeId,
        TestResult testResult,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "TestErgebnis-Protokolleintrag für Aufgabe {AufgabeId} hinzufügen. Bestanden: {Bestanden}, Anzahl: {Anzahl}.",
            aufgabeId,
            testResult.Bestanden,
            testResult.Ergebnisse.Count);

        var zusammenfassung = testResult.Bestanden
            ? $"Alle {testResult.Ergebnisse.Count} Tests bestanden."
            : $"{testResult.Ergebnisse.Count(e => e.Status == TestStatus.Fehlgeschlagen)} von {testResult.Ergebnisse.Count} Tests fehlgeschlagen.";

        var eintrag = new Protokolleintrag
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            Typ = ProtokollTyp.TestErgebnis,
            Inhalt = zusammenfassung,
            Zeitstempel = DateTimeOffset.UtcNow
        };

        eintrag.TestErgebnisse = testResult.Ergebnisse.Select(e => new TestErgebnis
        {
            Id = Guid.NewGuid(),
            ProtokollEintragId = eintrag.Id,
            TestName = e.TestName,
            Status = e.Status,
            Fehlermeldung = e.Fehlermeldung,
            Dauer = e.Dauer
        }).ToList();

        _db.Protokolleintraege.Add(eintrag);
        await _db.SaveChangesAsync(ct);

        return eintrag;
    }

    /// <summary>Erstellt einen Protokolleintrag für einen Statusübergang.</summary>
    public async Task<Protokolleintrag> AddStatusUebergangAsync(
        Guid aufgabeId,
        AufgabeStatus vonStatus,
        AufgabeStatus nachStatus,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Statusübergang für Aufgabe {AufgabeId}: {VonStatus} → {NachStatus}.",
            aufgabeId,
            vonStatus,
            nachStatus);

        var inhalt = $"Status geändert: {vonStatus} → {nachStatus}";

        return await AddEintragAsync(aufgabeId, ProtokollTyp.StatusUebergang, inhalt, null, ct);
    }

    private const string RateLimitMarkerPrefix = "[[SOFTWARESCHMIEDE_RATE_LIMIT";

    /// <summary>
    /// Speichert eine einzelne CLI-Ausgabezeile als Protokolleintrag vom Typ <see cref="ProtokollTyp.CliOutput"/>.
    /// Wenn die Zeile einen Rate-Limit-Marker enthält, wird zusätzlich ein Eintrag vom Typ
    /// <see cref="ProtokollTyp.RateLimit"/> gespeichert.
    /// </summary>
    public async Task AddCliOutputAsync(Guid aufgabeId, string outputLine, CancellationToken ct = default)
    {
        var eintrag = new Protokolleintrag
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            Typ = ProtokollTyp.CliOutput,
            Inhalt = outputLine,
            Zeitstempel = DateTimeOffset.UtcNow
        };

        _db.Protokolleintraege.Add(eintrag);

        if (TryParseRateLimitMarker(outputLine, out var resetUtc))
        {
            var rateLimitInhalt = resetUtc.HasValue
                ? $"Rate-Limit erkannt. Weiter ab: {resetUtc.Value:O}"
                : "Rate-Limit erkannt (kein Zeitstempel).";

            var rateLimitEintrag = new Protokolleintrag
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                Typ = ProtokollTyp.RateLimit,
                Inhalt = rateLimitInhalt,
                Zeitstempel = DateTimeOffset.UtcNow
            };
            _db.Protokolleintraege.Add(rateLimitEintrag);

            _logger.LogInformation(
                "Rate-Limit-Marker für Aufgabe {AufgabeId} erkannt. Reset: {ResetUtc}",
                aufgabeId,
                resetUtc?.ToString("O") ?? "(unbekannt)");
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Parst einen Rate-Limit-Marker aus einer CLI-Ausgabezeile.
    /// Format: <c>[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]</c>
    /// </summary>
    /// <returns><c>true</c> wenn ein Marker gefunden wurde; <c>resetUtc</c> enthält den Zeitstempel oder <c>null</c> wenn kein gültiger Zeitstempel vorliegt.</returns>
    public static bool TryParseRateLimitMarker(string outputLine, out DateTimeOffset? resetUtc)
    {
        resetUtc = null;

        if (string.IsNullOrWhiteSpace(outputLine))
            return false;

        var startIndex = outputLine.IndexOf(RateLimitMarkerPrefix, StringComparison.Ordinal);
        if (startIndex < 0)
            return false;

        var markerStart = startIndex + RateLimitMarkerPrefix.Length;
        var endIndex = outputLine.IndexOf("]]", markerStart, StringComparison.Ordinal);
        if (endIndex < 0)
            return false;

        var payload = outputLine[markerStart..endIndex];

        if (payload.StartsWith(":", StringComparison.Ordinal))
        {
            var timestampRaw = payload[1..].Trim();
            if (DateTimeOffset.TryParse(
                    timestampRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                resetUtc = parsed;
            }
        }

        return true;
    }

    /// <summary>Sucht in Inhalt und AgentName der Protokolleinträge einer Aufgabe.</summary>
    public async Task<IReadOnlyList<Protokolleintrag>> SuchenAsync(
        Guid aufgabeId,
        string suchbegriff,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleinträge für Aufgabe {AufgabeId} nach '{Suchbegriff}' durchsuchen.", aufgabeId, suchbegriff);

        return await _db.Protokolleintraege
            .AsNoTracking()
            .Include(p => p.TestErgebnisse)
            .Where(p => p.AufgabeId == aufgabeId
                && (p.Inhalt.Contains(suchbegriff) || (p.AgentName != null && p.AgentName.Contains(suchbegriff))))
            .OrderBy(p => p.Zeitstempel)
            .ToListAsync(ct);
    }
}
