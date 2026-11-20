using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service zum Speichern und Abrufen von Protokolleinträgen.</summary>
public sealed class LogService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<LogService> _logger;

    /// <inheritdoc cref="LogService"/>
    public LogService(SoftwareschmiedeDbContext db, ILogger<LogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Gibt alle Protokolleinträge einer Aufgabe chronologisch sortiert zurück (inkl. TestErgebnisse).</summary>
    public async Task<IReadOnlyList<LogEntry>> GetByAufgabeAsync(Guid workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleinträge für Aufgabe {AufgabeId} abrufen.", workItemId);
        return await _db.LogEntries
            .AsNoTracking()
            .Include(p => p.TestResults)
            .Where(p => p.WorkItemId == workItemId)
            .OrderBy(p => p.Timestamp)
            .ToListAsync(ct);
    }

    /// <summary>Fügt einen neuen Protokolleintrag hinzu.</summary>
    public async Task<LogEntry> AddEntryAsync(
        Guid workItemId,
        LogEntryType typ,
        string inhalt,
        string? agentName = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleintrag (Typ: {Typ}) für Aufgabe {AufgabeId} hinzufügen.", typ, workItemId);

        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Type = typ,
            Content = inhalt,
            AgentName = agentName,
            Timestamp = DateTimeOffset.UtcNow
        };

        _db.LogEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return entry;
    }

    /// <summary>Erstellt einen Protokolleintrag vom Typ TestErgebnis und fügt TestErgebnis-Einträge hinzu.</summary>
    public async Task<LogEntry> AddTestResultsAsync(
        Guid workItemId,
        TestRunResult testResult,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "TestErgebnis-Protokolleintrag für Aufgabe {AufgabeId} hinzufügen. Bestanden: {Bestanden}, Anzahl: {Anzahl}.",
            workItemId,
            testResult.Passed,
            testResult.Results.Count);

        var summary = testResult.Passed
            ? $"Alle {testResult.Results.Count} Tests bestanden."
            : $"{testResult.Results.Count(e => e.Status == TestStatus.Failed)} von {testResult.Results.Count} Tests fehlgeschlagen.";

        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Type = LogEntryType.TestResult,
            Content = summary,
            Timestamp = DateTimeOffset.UtcNow
        };

        entry.TestResults = testResult.Results.Select(e => new TestResult
        {
            Id = Guid.NewGuid(),
            LogEntryId = entry.Id,
            TestName = e.TestName,
            Status = e.Status,
            ErrorMessage = e.ErrorMessage,
            Duration = e.Duration
        }).ToList();

        _db.LogEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return entry;
    }

    /// <summary>Erstellt einen Protokolleintrag für einen Statusübergang.</summary>
    public async Task<LogEntry> AddStatusUebergangAsync(
        Guid workItemId,
        WorkItemStatus vonStatus,
        WorkItemStatus nachStatus,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Statusübergang für Aufgabe {AufgabeId}: {VonStatus} → {NachStatus}.",
            workItemId,
            vonStatus,
            nachStatus);

        var inhalt = $"Status geändert: {vonStatus.ToDisplayText()} → {nachStatus.ToDisplayText()}";

        return await AddEntryAsync(workItemId, LogEntryType.StatusTransition, inhalt, null, ct);
    }

    private const string RateLimitMarkerPrefix = "[[SOFTWARESCHMIEDE_RATE_LIMIT";

    /// <summary>
    /// Speichert eine einzelne CLI-Ausgabezeile als Protokolleintrag vom Typ <see cref="LogEntryType.CliOutput"/>.
    /// Wenn die Zeile einen Rate-Limit-Marker enthält, wird zusätzlich ein Eintrag vom Typ
    /// <see cref="LogEntryType.RateLimit"/> gespeichert.
    /// </summary>
    public async Task AddCliOutputAsync(Guid workItemId, string outputLine, CancellationToken ct = default)
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            Type = LogEntryType.CliOutput,
            Content = outputLine,
            Timestamp = DateTimeOffset.UtcNow
        };

        _db.LogEntries.Add(entry);

        if (TryParseRateLimitMarker(outputLine, out var resetUtc))
        {
            var rateLimitInhalt = resetUtc.HasValue
                ? $"Rate-Limit erkannt. Weiter ab: {resetUtc.Value:O}"
                : "Rate-Limit erkannt (kein Zeitstempel).";

            var rateLimitEntry = new LogEntry
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
                Type = LogEntryType.RateLimit,
                Content = rateLimitInhalt,
                Timestamp = DateTimeOffset.UtcNow
            };
            _db.LogEntries.Add(rateLimitEntry);

            _logger.LogInformation(
                "Rate-Limit-Marker für Aufgabe {AufgabeId} erkannt. Reset: {ResetUtc}",
                workItemId,
                resetUtc?.ToString("O") ?? "(unbekannt)");
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Parst einen Rate-Limit-Marker aus einer CLI-Ausgabezeile.
    /// Format: <c>[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]</c>
    /// </summary>
    /// <returns>Tuple: gefunden, optionaler Vorschlagsprompt (null), optionaler Reset-Zeitpunkt.</returns>
    public static (bool Found, string? Prompt, DateTimeOffset? ResetUtc) ParseRateLimitMarker(string outputLine)
    {
        var found = TryParseRateLimitMarker(outputLine, out var resetUtc);
        return (found, null, resetUtc);
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
    public async Task<IReadOnlyList<LogEntry>> SearchAsync(
        Guid workItemId,
        string suchbegriff,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Protokolleinträge für Aufgabe {AufgabeId} nach '{Suchbegriff}' durchsuchen.", workItemId, suchbegriff);

        return await _db.LogEntries
            .AsNoTracking()
            .Include(p => p.TestResults)
            .Where(p => p.WorkItemId == workItemId
                && (p.Content.Contains(suchbegriff) || (p.AgentName != null && p.AgentName.Contains(suchbegriff))))
            .OrderBy(p => p.Timestamp)
            .ToListAsync(ct);
    }
}
