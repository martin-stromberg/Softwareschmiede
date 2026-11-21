using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Scoped Service für den EF-Zugriff auf persistierte zeitgesteuerte Prompts —
/// wird vom Singleton <see cref="PromptScheduleDispatchService"/> über einen Scope aufgelöst.
/// </summary>
public sealed class ScheduledPromptStore
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<ScheduledPromptStore> _logger;

    /// <inheritdoc cref="ScheduledPromptStore"/>
    public ScheduledPromptStore(SoftwareschmiedeDbContext db, ILogger<ScheduledPromptStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liefert alle persistierten geplanten Prompts.</summary>
    public async Task<IReadOnlyList<ScheduledPrompt>> GetAllAsync(CancellationToken ct = default)
        => await _db.ScheduledPrompts.AsNoTracking().ToListAsync(ct);

    /// <summary>Schreibt den geplanten Prompt der Aufgabe oder ersetzt einen vorhandenen Datensatz (Ersetzungsregel).</summary>
    public async Task UpsertAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, CancellationToken ct = default)
    {
        var existing = await _db.ScheduledPrompts
            .FirstOrDefaultAsync(z => z.WorkItemId == workItemId, ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _db.ScheduledPrompts.Add(new ScheduledPrompt
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
                PromptText = promptText,
                TargetTimeUtc = targetTimeUtc,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            existing.PromptText = promptText;
            existing.TargetTimeUtc = targetTimeUtc;
            existing.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Entfernt den persistierten Plan der Aufgabe, falls vorhanden.</summary>
    public async Task DeleteAsync(Guid workItemId, CancellationToken ct = default)
    {
        var existing = await _db.ScheduledPrompts
            .FirstOrDefaultAsync(z => z.WorkItemId == workItemId, ct);
        if (existing is null)
            return;

        _db.ScheduledPrompts.Remove(existing);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} gelöscht.", workItemId);
    }

    /// <summary>
    /// Schreibt den geplanten Prompt nur, wenn der vorhandene Datensatz noch dem erwarteten
    /// Vorgängerstand entspricht — bzw. wenn kein Datensatz existiert und auch keiner erwartet
    /// wird (<paramref name="expected"/> ist <c>null</c>). Lost-Update-Schutz für den
    /// Sofortversand-Fehlerpfad: Wurde während des in-flight-Versands bereits ein neuerer Plan
    /// persistiert, bleibt dessen Datensatz erhalten statt vom alten Plan überschrieben zu werden.
    /// </summary>
    /// <returns><c>true</c>, wenn der Datensatz geschrieben wurde; <c>false</c>, wenn ein abweichender Datensatz den Schreibzugriff verweigerte.</returns>
    public async Task<bool> UpsertIfMatchingAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, ScheduledPromptInfo? expected, CancellationToken ct = default)
    {
        var existing = await _db.ScheduledPrompts
            .FirstOrDefaultAsync(z => z.WorkItemId == workItemId, ct);

        // ZielZeitpunktUtc wird als Unix-Millisekunden persistiert — den Vergleich auf dieselbe
        // Granularität normieren, sonst scheitert er an der Sub-Millisekunden-Genauigkeit.
        var matches = existing is null
            ? expected is null
            : expected is not null
                && existing.PromptText == expected.PromptText
                && existing.TargetTimeUtc.ToUnixTimeMilliseconds() == expected.TargetTime.ToUnixTimeMilliseconds();

        if (!matches)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} wird nicht mit dem Alt-Plan \"{AltText}\" ({AltZiel} UTC) überschrieben — der Datensatz wurde zwischenzeitlich durch einen neueren Plan ersetzt.",
                workItemId,
                promptText,
                targetTimeUtc);
            return false;
        }

        await UpsertAsync(workItemId, promptText, targetTimeUtc, ct);
        return true;
    }

    /// <summary>
    /// Entfernt den persistierten Plan der Aufgabe nach einem erfolgreichen Versand.
    /// Entspricht der Datensatz dem versendeten Plan, wird er immer gelöscht. Weicht er ab,
    /// wird er nur behalten, wenn <paramref name="newerScheduleActive"/> gesetzt ist — also zum
    /// Löschzeitpunkt noch ein lebender In-Memory-Plan für die Aufgabe existiert, dem der
    /// abweichende Datensatz gehören kann (Lost-Update-Schutz). Ohne aktiven Plan ist ein
    /// abweichender Datensatz eine Leiche des versendeten Plans (z. B. nach einem
    /// fehlgeschlagenen Store-Schreibzugriff) und wird mitgelöscht, damit der bereits
    /// zugestellte Prompt nach einem Neustart nicht erneut zugestellt wird.
    /// </summary>
    public async Task DeleteAfterDispatchAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc, bool newerScheduleActive, CancellationToken ct = default)
    {
        var existing = await _db.ScheduledPrompts
            .FirstOrDefaultAsync(z => z.WorkItemId == workItemId, ct);
        if (existing is null)
            return;

        // ZielZeitpunktUtc wird als Unix-Millisekunden persistiert — den Vergleich auf dieselbe
        // Granularität normieren, sonst scheitert er an der Sub-Millisekunden-Genauigkeit.
        var matches = existing.PromptText == promptText
            && existing.TargetTimeUtc.ToUnixTimeMilliseconds() == targetTimeUtc.ToUnixTimeMilliseconds();

        if (!matches && newerScheduleActive)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} weicht vom versendeten Plan ab (Datensatz: \"{DatensatzText}\" / {DatensatzZiel} UTC, versendet: \"{VersendeterText}\" / {VersendetZiel} UTC) und bleibt erhalten — ein neuerer Plan ist aktiv.",
                workItemId,
                existing.PromptText,
                existing.TargetTimeUtc,
                promptText,
                targetTimeUtc);
            return;
        }

        if (!matches)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} weicht vom versendeten Plan ab (Datensatz: \"{DatensatzText}\" / {DatensatzZiel} UTC, versendet: \"{VersendeterText}\" / {VersendetZiel} UTC) und wird trotzdem gelöscht — ohne aktiven Plan kann er nur eine Leiche des versendeten Plans sein.",
                workItemId,
                existing.PromptText,
                existing.TargetTimeUtc,
                promptText,
                targetTimeUtc);
        }

        _db.ScheduledPrompts.Remove(existing);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} gelöscht.", workItemId);
    }

    /// <summary>Aktualisiert den Zielzeitpunkt des persistierten Plans (z. B. Pausenverschiebung), falls vorhanden.</summary>
    public async Task UpdateTargetTimeAsync(Guid workItemId, DateTimeOffset targetTimeUtc, CancellationToken ct = default)
    {
        var existing = await _db.ScheduledPrompts
            .FirstOrDefaultAsync(z => z.WorkItemId == workItemId, ct);
        if (existing is null)
            return;

        existing.TargetTimeUtc = targetTimeUtc;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
