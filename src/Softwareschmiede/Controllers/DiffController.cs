using Microsoft.AspNetCore.Mvc;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Controllers;

/// <summary>
/// API Controller für Diff-Vergleiche von Dateiänderungen.
/// Bietet Endpoints zum Generieren, Abrufen, Suchen und Verwalten von Diffs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiffController : ControllerBase
{
    private readonly DiffService _diffService;
    private readonly ILogger<DiffController> _logger;

    public DiffController(DiffService diffService, ILogger<DiffController> logger)
    {
        _diffService = diffService;
        _logger = logger;
    }

    /// <summary>
    /// Generiert einen neuen Diff zwischen zwei Dateiinhalten.
    /// </summary>
    /// <param name="request">Diff-Generierungsanforderung mit Quell- und Zielinhalt</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>Generierter Diff mit Blöcken und Zeilen</returns>
    /// <response code="200">Diff erfolgreich generiert</response>
    /// <response code="400">Ungültige Anforderung oder fehlende Parameter</response>
    /// <response code="500">Fehler beim Generieren des Diffs</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(DiffResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateDiffAsync(
        [FromBody] GenerateDiffRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SourceContent) || string.IsNullOrWhiteSpace(request.TargetContent))
            {
                _logger.LogWarning("Diff-Anforderung mit leeren Inhalten abgelehnt.");
                return BadRequest(new ProblemDetails
                {
                    Title = "Ungültige Anforderung",
                    Detail = "Quell- und Zielinhalt dürfen nicht leer sein.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _diffService.GenerateDiffAsync(
                aufgabeId: request.AufgabeId,
                filePath: request.FilePath ?? "Unknown",
                sourceContent: request.SourceContent,
                targetContent: request.TargetContent,
                sourceVersion: request.SourceVersion ?? "v1",
                targetVersion: request.TargetVersion ?? "v2",
                diffType: request.DiffType ?? DiffType.Full,
                cachingStrategy: request.CachingStrategy ?? DiffCachingStrategy.TTL,
                ct: ct);

            _logger.LogInformation(
                "Diff erfolgreich generiert. DiffResultId: {DiffResultId}, FilePath: {FilePath}",
                result.Id, result.FilePath);

            return Ok(MapToDto(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Generieren des Diffs.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Generieren des Diffs",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Ruft einen vorhandenen Diff mit allen Blöcken und Zeilen ab.
    /// </summary>
    /// <param name="id">Eindeutige ID des Diffs</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>Diff mit vollständigen Daten</returns>
    /// <response code="200">Diff gefunden und zurückgegeben</response>
    /// <response code="404">Diff nicht gefunden</response>
    /// <response code="500">Fehler beim Abrufen des Diffs</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DiffResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDiffAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _diffService.GetDiffAsync(id, ct);
            if (result == null)
            {
                _logger.LogWarning("Diff mit ID {DiffResultId} nicht gefunden.", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Diff nicht gefunden",
                    Detail = $"Kein Diff mit ID '{id}' existiert.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(MapToDto(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen des Diffs mit ID {DiffResultId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Abrufen des Diffs",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Listet alle Diffs für eine Aufgabe mit Pagination auf.
    /// </summary>
    /// <param name="aufgabeId">Aufgaben-ID zum Filtern</param>
    /// <param name="page">Seitennummer (Standard: 1)</param>
    /// <param name="pageSize">Einträge pro Seite (Standard: 20, Maximum: 100)</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>Paginierte Liste von Diffs</returns>
    /// <response code="200">Diffs gefunden und zurückgegeben</response>
    /// <response code="400">Ungültige Paginations-Parameter</response>
    /// <response code="500">Fehler beim Abrufen der Diffs</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedDiffListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListDiffsAsync(
        [FromQuery] Guid aufgabeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Ungültige Paginations-Parameter. Page: {Page}, PageSize: {PageSize}", page, pageSize);
                return BadRequest(new ProblemDetails
                {
                    Title = "Ungültige Parameter",
                    Detail = "Page muss >= 1 sein und PageSize zwischen 1 und 100 liegen.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var results = await _diffService.SearchDiffsAsync(
                aufgabeId: aufgabeId,
                skipCount: (page - 1) * pageSize,
                takeCount: pageSize,
                ct: ct);

            // Zusätzliche Abfrage für Gesamtanzahl (kostspielig, könnte optimiert werden)
            var totalCount = await _diffService.GetDiffCountAsync(aufgabeId, ct);

            _logger.LogInformation(
                "Diffs für Aufgabe {AufgabeId} abgerufen. Page: {Page}, PageSize: {PageSize}, Total: {Total}",
                aufgabeId, page, pageSize, totalCount);

            return Ok(new PaginatedDiffListDto
            {
                Items = results.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen der Diffs für Aufgabe {AufgabeId}.", aufgabeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Abrufen der Diffs",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Ruft Statistiken für Diffs einer Aufgabe ab (Anzahl, durchschnittliche Größe, etc.).
    /// </summary>
    /// <param name="aufgabeId">Aufgaben-ID zum Filtern</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>Diff-Statistiken</returns>
    /// <response code="200">Statistiken abgerufen</response>
    /// <response code="500">Fehler beim Abrufen der Statistiken</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(DiffStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatisticsAsync(
        [FromQuery] Guid aufgabeId,
        CancellationToken ct = default)
    {
        try
        {
            var stats = await _diffService.GetStatisticsAsync(aufgabeId, ct);

            _logger.LogInformation(
                "Diff-Statistiken für Aufgabe {AufgabeId} abgerufen. Diff-Anzahl: {DiffCount}",
                aufgabeId, stats.TotalDiffCount);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von Diff-Statistiken für Aufgabe {AufgabeId}.", aufgabeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Abrufen der Statistiken",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Löscht einen Diff und all zugehörige Daten.
    /// </summary>
    /// <param name="id">Eindeutige ID des zu löschenden Diffs</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>204 No Content bei erfolgreichem Löschen</returns>
    /// <response code="204">Diff erfolgreich gelöscht</response>
    /// <response code="404">Diff nicht gefunden</response>
    /// <response code="500">Fehler beim Löschen des Diffs</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDiffAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var deleted = await _diffService.DeleteDiffAsync(id, ct);
            if (!deleted)
            {
                _logger.LogWarning("Diff mit ID {DiffResultId} konnte nicht gelöscht werden.", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Diff nicht gefunden",
                    Detail = $"Kein Diff mit ID '{id}' existiert.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Diff mit ID {DiffResultId} erfolgreich gelöscht.", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Löschen des Diffs mit ID {DiffResultId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Löschen des Diffs",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Invalidiert den Cache für einen Diff (z.B. nach Quelländerung).
    /// </summary>
    /// <param name="id">Eindeutige ID des Diffs</param>
    /// <param name="ct">Abbruchtoken</param>
    /// <returns>204 No Content bei erfolgreichem Invalidieren</returns>
    /// <response code="204">Cache erfolgreich invalidiert</response>
    /// <response code="404">Diff nicht gefunden</response>
    /// <response code="500">Fehler beim Invalidieren des Caches</response>
    [HttpPost("{id:guid}/invalidate-cache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateCacheAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var diffExists = await _diffService.GetDiffAsync(id, ct);
            if (diffExists == null)
            {
                _logger.LogWarning("Diff mit ID {DiffResultId} nicht gefunden.", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Diff nicht gefunden",
                    Detail = $"Kein Diff mit ID '{id}' existiert.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            await _diffService.InvalidateDiffCacheAsync(id, ct);

            _logger.LogInformation("Cache für Diff {DiffResultId} invalidiert.", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Invalidieren des Caches für Diff {DiffResultId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Fehler beim Invalidieren des Caches",
                Detail = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    #region Private Helper Methods

    private static DiffResultDto MapToDto(DiffResult result)
    {
        return new DiffResultDto
        {
            Id = result.Id,
            FilePath = result.FilePath,
            SourceVersion = result.SourceVersion,
            TargetVersion = result.TargetVersion,
            AddedLines = result.AddedLines,
            RemovedLines = result.RemovedLines,
            ModifiedLines = result.ModifiedLines,
            Status = result.Status,
            DiffType = result.DiffType,
            CachingStrategy = DiffCachingStrategy.TTL, // Default
            GeneratedAt = result.GeneratedAt,
            Blocks = result.DiffBlocks?.Select(block => new DiffBlockDto
            {
                Id = block.Id,
                BlockType = block.BlockType,
                StartLineSource = block.SourceStartLine,
                EndLineSource = block.SourceEndLine,
                StartLineTarget = block.TargetStartLine,
                EndLineTarget = block.TargetEndLine,
                SummaryContent = string.Empty, // Not available on entity
                Lines = block.DiffLines?.Select(line => new DiffLineDto
                {
                    Id = line.Id,
                    LineNumber = line.SourceLineNumber ?? line.TargetLineNumber ?? 0,
                    Content = line.Content,
                    Status = line.LineStatus,
                    IsContext = line.LineStatus == DiffLineStatus.Context
                }).ToList() ?? []
            }).ToList() ?? []
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Anforderung zum Generieren eines neuen Diffs.
/// </summary>
public class GenerateDiffRequest
{
    /// <summary>Aufgaben-ID (optional, für Kontextzuordnung)</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Dateiname oder Pfad (optional, Standard: "Unknown")</summary>
    public string? FilePath { get; set; }

    /// <summary>Quell-Dateiinhalt</summary>
    public required string SourceContent { get; set; }

    /// <summary>Ziel-Dateiinhalt</summary>
    public required string TargetContent { get; set; }

    /// <summary>Versionsnummer der Quelle (optional, Standard: "v1")</summary>
    public string? SourceVersion { get; set; }

    /// <summary>Versionsnummer des Ziels (optional, Standard: "v2")</summary>
    public string? TargetVersion { get; set; }

    /// <summary>Diff-Rendering-Typ (optional, Standard: Full)</summary>
    public DiffType? DiffType { get; set; }

    /// <summary>Cache-Strategie (optional, Standard: TTL)</summary>
    public DiffCachingStrategy? CachingStrategy { get; set; }
}

/// <summary>
/// DTO für Diff-Ergebnis.
/// </summary>
public class DiffResultDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public int AddedLines { get; set; }
    public int RemovedLines { get; set; }
    public int ModifiedLines { get; set; }
    public DiffResultStatus Status { get; set; }
    public DiffType DiffType { get; set; }
    public DiffCachingStrategy CachingStrategy { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public List<DiffBlockDto> Blocks { get; set; } = [];
}

/// <summary>
/// DTO für einen Diff-Block (Gruppe von zusammenhängenden Änderungen).
/// </summary>
public class DiffBlockDto
{
    public Guid Id { get; set; }
    public DiffBlockType BlockType { get; set; }
    public int StartLineSource { get; set; }
    public int EndLineSource { get; set; }
    public int StartLineTarget { get; set; }
    public int EndLineTarget { get; set; }
    public string SummaryContent { get; set; } = string.Empty;
    public List<DiffLineDto> Lines { get; set; } = [];
}

/// <summary>
/// DTO für eine einzelne Diff-Zeile.
/// </summary>
public class DiffLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DiffLineStatus Status { get; set; }
    public bool IsContext { get; set; }
}

/// <summary>
/// DTO für paginierte Diff-Liste.
/// </summary>
public class PaginatedDiffListDto
{
    public List<DiffResultDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// DTO für Diff-Statistiken.
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

#endregion
