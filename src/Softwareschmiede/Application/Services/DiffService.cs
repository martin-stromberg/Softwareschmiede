using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service für Diff-Verwaltung: Orchestriert Diff-Generierung, Caching und Persistierung.
/// </summary>
public sealed class DiffService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly DiffAlgorithmService _diffAlgorithmService;
    private readonly DiffCachingService _diffCachingService;
    private readonly ILogger<DiffService> _logger;

    // Maximale Dateigröße für inline Speicherung (100 KB)
    private const int MaxInlineContentSize = 100 * 1024;

    /// <inheritdoc cref="DiffService"/>
    public DiffService(
        SoftwareschmiededDbContext db,
        DiffAlgorithmService diffAlgorithmService,
        DiffCachingService diffCachingService,
        ILogger<DiffService> logger)
    {
        _db = db;
        _diffAlgorithmService = diffAlgorithmService;
        _diffCachingService = diffCachingService;
        _logger = logger;
    }

    /// <summary>
    /// Generiert einen Diff zwischen zwei Dateiinhalten (vereinfachte Überladung für den Controller).
    /// </summary>
    public async Task<DiffResult> GenerateDiffAsync(
        Guid aufgabeId,
        string filePath,
        string sourceContent,
        string targetContent,
        string sourceVersion = "v1",
        string targetVersion = "v2",
        DiffType diffType = DiffType.Full,
        DiffCachingStrategy cachingStrategy = DiffCachingStrategy.TTL,
        CancellationToken ct = default)
    {
        return await GenerateDiffAsync(
            aufgabeId: aufgabeId,
            gitRepositoryId: null,
            filePath: filePath,
            sourceVersion: sourceVersion,
            targetVersion: targetVersion,
            sourceContent: sourceContent,
            targetContent: targetContent,
            diffType: diffType,
            ct: ct);
    }

    /// <summary>
    /// Generiert einen Diff zwischen zwei Dateiversionen (mit Caching).
    /// </summary>
    public async Task<DiffResult> GenerateDiffAsync(
        Guid aufgabeId,
        Guid? gitRepositoryId,
        string filePath,
        string sourceVersion,
        string targetVersion,
        string sourceContent,
        string targetContent,
        DiffType diffType = DiffType.Full,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("FilePath darf nicht leer sein.", nameof(filePath));
        if (string.IsNullOrWhiteSpace(sourceVersion))
            throw new ArgumentException("SourceVersion darf nicht leer sein.", nameof(sourceVersion));
        if (string.IsNullOrWhiteSpace(targetVersion))
            throw new ArgumentException("TargetVersion darf nicht leer sein.", nameof(targetVersion));

        _logger.LogInformation(
            "Diff-Generierung gestartet. Aufgabe: {AufgabeId}, Datei: {FilePath}, {Source} → {Target}",
            aufgabeId, filePath, sourceVersion, targetVersion);

        // Prüfe, ob die Aufgabe existiert
        var aufgabe = await _db.Aufgaben.FindAsync(new object?[] { aufgabeId }, cancellationToken: ct);
        if (aufgabe == null)
            throw new InvalidOperationException($"Aufgabe {aufgabeId} nicht gefunden.");

        // Erstelle neues DiffResult
        var diffResult = new DiffResult
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            GitRepositoryId = gitRepositoryId,
            FilePath = filePath,
            SourceVersion = sourceVersion,
            TargetVersion = targetVersion,
            DiffType = diffType,
            Status = DiffResultStatus.Pending,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = nameof(DiffService),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) // 24h TTL
        };

        // Prüfe Cache
        var cachedResult = await _diffCachingService.GetFromCacheAsync(
            diffResult.Id, aufgabeId, filePath, sourceVersion, targetVersion, ct);

        if (cachedResult != null)
        {
            diffResult.Status = DiffResultStatus.Cached;
            return cachedResult;
        }

        try
        {
            // Generiere Diff
            var (blocks, addedLines, removedLines, modifiedLines) = await _diffAlgorithmService.GenerateDiffAsync(
                sourceContent,
                targetContent,
                diffResult.Id,
                ct);

            diffResult.DiffBlocks = blocks;
            diffResult.AddedLines = addedLines;
            diffResult.RemovedLines = removedLines;
            diffResult.ModifiedLines = modifiedLines;
            diffResult.LineCount = addedLines + removedLines + modifiedLines;
            diffResult.Status = DiffResultStatus.Generated;

            // Speichere Inline-Inhalte (nur wenn kleine Dateien)
            if (sourceContent.Length <= MaxInlineContentSize)
                diffResult.SourceContent = sourceContent;
            if (targetContent.Length <= MaxInlineContentSize)
                diffResult.TargetContent = targetContent;

            // Persistiere DiffResult
            _db.DiffResults.Add(diffResult);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Diff generiert und persistiert. DiffResult: {DiffResultId}, Blöcke: {BlockCount}, +{Added}, -{Removed}, ~{Modified}",
                diffResult.Id, blocks.Count, addedLines, removedLines, modifiedLines);

            // Cache speichern (async, ohne zu warten)
            _ = _diffCachingService.SetInCacheAsync(diffResult, aufgabeId, filePath, sourceVersion, targetVersion, ct)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.LogWarning(t.Exception, "Fehler beim Caching des Diff");
                }, TaskScheduler.Default);

            return diffResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Diff-Generierung");
            diffResult.Status = DiffResultStatus.Error;
            _db.DiffResults.Add(diffResult);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Ruft einen Diff mit allen Details ab.
    /// </summary>
    public async Task<DiffResult?> GetDiffAsync(Guid diffResultId, CancellationToken ct = default)
    {
        _logger.LogInformation("Diff abrufen. DiffResultId: {DiffResultId}", diffResultId);

        return await _db.DiffResults
            .AsNoTracking()
            .Include(dr => dr.DiffBlocks.OrderBy(db => db.BlockSequence))
            .ThenInclude(db => db.DiffLines.OrderBy(dl => dl.LineSequence))
            .FirstOrDefaultAsync(dr => dr.Id == diffResultId, cancellationToken: ct);
    }

    /// <summary>
    /// Ruft alle Diffs für eine Aufgabe ab.
    /// </summary>
    public async Task<IReadOnlyList<DiffResult>> GetDiffsByAufgabeAsync(
        Guid aufgabeId,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Diffs für Aufgabe abrufen. AufgabeId: {AufgabeId}, Skip: {Skip}, Take: {Take}",
            aufgabeId, skip, take);

        return await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId)
            .OrderByDescending(dr => dr.GeneratedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Löscht einen Diff und seinen Cache.
    /// </summary>
    public async Task<bool> DeleteDiffAsync(Guid diffResultId, CancellationToken ct = default)
    {
        _logger.LogInformation("Diff löschen. DiffResultId: {DiffResultId}", diffResultId);

        var diffResult = await _db.DiffResults.FindAsync(new object?[] { diffResultId }, cancellationToken: ct);
        if (diffResult != null)
        {
            _db.DiffResults.Remove(diffResult);
            await _db.SaveChangesAsync(ct);

            // Invalidiere auch Cache
            await _diffCachingService.InvalidateCacheAsync(diffResultId, ct);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sucht Diffs nach Aufgabe mit optionalem Suchterm.
    /// </summary>
    public async Task<IReadOnlyList<DiffResult>> SearchDiffsAsync(
        Guid aufgabeId,
        int skipCount = 0,
        int takeCount = 50,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Diffs durchsuchen. AufgabeId: {AufgabeId}, Skip: {Skip}, Take: {Take}",
            aufgabeId, skipCount, takeCount);

        var query = _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId);

        // Suche in Dateipfad (optional)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(dr => dr.FilePath.Contains(searchTerm)
                || dr.SourceVersion.Contains(searchTerm)
                || dr.TargetVersion.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(dr => dr.GeneratedAt)
            .Skip(skipCount)
            .Take(takeCount)
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Sucht Diffs nach Dateiname oder Inhalt (ältere Überladung für Rückwärtskompatibilität).
    /// </summary>
    public async Task<IReadOnlyList<DiffResult>> SearchDiffsAsync(
        Guid aufgabeId,
        string searchTerm,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Diffs durchsuchen. AufgabeId: {AufgabeId}, SearchTerm: {SearchTerm}",
            aufgabeId, searchTerm);

        var query = _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId);

        // Suche in Dateipfad
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(dr => dr.FilePath.Contains(searchTerm)
                || dr.SourceVersion.Contains(searchTerm)
                || dr.TargetVersion.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(dr => dr.GeneratedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Zählt die Anzahl der Diffs für eine Aufgabe.
    /// </summary>
    public async Task<int> GetDiffCountAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Diff-Anzahl abrufen. AufgabeId: {AufgabeId}", aufgabeId);

        return await _db.DiffResults
            .AsNoTracking()
            .CountAsync(dr => dr.AufgabeId == aufgabeId, cancellationToken: ct);
    }

    /// <summary>
    /// Invalidiert den Cache für einen Diff.
    /// </summary>
    public async Task InvalidateDiffCacheAsync(Guid diffResultId, CancellationToken ct = default)
    {
        _logger.LogInformation("Cache für Diff invalidieren. DiffResultId: {DiffResultId}", diffResultId);
        await _diffCachingService.InvalidateCacheAsync(diffResultId, ct);
    }

    /// <summary>
    /// Ruft Statistiken für eine Aufgabe ab (mit erweiterten Feldern für Controller).
    /// </summary>
    public async Task<DiffStatisticsDto> GetStatisticsAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Statistiken abrufen. AufgabeId: {AufgabeId}", aufgabeId);

        var diffs = await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId)
            .ToListAsync(cancellationToken: ct);

        if (diffs.Count == 0)
        {
            return new DiffStatisticsDto();
        }

        var totalAddedLines = diffs.Sum(d => d.AddedLines);
        var totalRemovedLines = diffs.Sum(d => d.RemovedLines);
        var totalModifiedLines = diffs.Sum(d => d.ModifiedLines);
        var totalLines = totalAddedLines + totalRemovedLines + totalModifiedLines;

        var statusBreakdown = new Dictionary<DiffResultStatus, int>();
        foreach (var status in diffs.Select(d => d.Status).Distinct())
        {
            statusBreakdown[status] = diffs.Count(d => d.Status == status);
        }

        return new DiffStatisticsDto
        {
            TotalDiffCount = diffs.Count,
            TotalAddedLines = totalAddedLines,
            TotalRemovedLines = totalRemovedLines,
            TotalModifiedLines = totalModifiedLines,
            AverageLinesPerDiff = diffs.Count > 0 ? (double)totalLines / diffs.Count : 0,
            OldestDiff = diffs.MinBy(d => d.GeneratedAt)?.GeneratedAt,
            NewestDiff = diffs.MaxBy(d => d.GeneratedAt)?.GeneratedAt,
            StatusBreakdown = statusBreakdown
        };
    }
}

/// <summary>
/// Statistiken über Diffs einer Aufgabe.
/// </summary>
public sealed record DiffStatistics(
    int TotalDiffs,
    int TotalAddedLines,
    int TotalRemovedLines,
    int TotalModifiedLines,
    int CachedDiffs,
    int FailedDiffs);

/// <summary>
/// DTO für erweiterte Diff-Statistiken (für API Controller).
/// </summary>
public class DiffStatisticsDto
{
    public int TotalDiffCount { get; set; }
    public int TotalAddedLines { get; set; }
    public int TotalRemovedLines { get; set; }
    public int TotalModifiedLines { get; set; }
    public double AverageLinesPerDiff { get; set; }
    public DateTimeOffset? OldestDiff { get; set; }
    public DateTimeOffset? NewestDiff { get; set; }
    public Dictionary<DiffResultStatus, int> StatusBreakdown { get; set; } = [];
}
