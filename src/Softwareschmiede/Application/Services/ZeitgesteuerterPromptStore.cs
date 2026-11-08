using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Scoped Service für den EF-Zugriff auf persistierte zeitgesteuerte Prompts —
/// wird vom Singleton <see cref="PromptZeitVersandService"/> über einen Scope aufgelöst.
/// </summary>
public sealed class ZeitgesteuerterPromptStore
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<ZeitgesteuerterPromptStore> _logger;

    /// <inheritdoc cref="ZeitgesteuerterPromptStore"/>
    public ZeitgesteuerterPromptStore(SoftwareschmiededDbContext db, ILogger<ZeitgesteuerterPromptStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liefert alle persistierten geplanten Prompts.</summary>
    public async Task<IReadOnlyList<ZeitgesteuerterPrompt>> GetAlleAsync(CancellationToken ct = default)
        => await _db.ZeitgesteuertePrompts.AsNoTracking().ToListAsync(ct);

    /// <summary>Schreibt den geplanten Prompt der Aufgabe oder ersetzt einen vorhandenen Datensatz (Ersetzungsregel).</summary>
    public async Task UpsertAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, CancellationToken ct = default)
    {
        var vorhanden = await _db.ZeitgesteuertePrompts
            .FirstOrDefaultAsync(z => z.AufgabeId == aufgabeId, ct);
        var jetzt = DateTimeOffset.UtcNow;

        if (vorhanden is null)
        {
            _db.ZeitgesteuertePrompts.Add(new ZeitgesteuerterPrompt
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                PromptText = promptText,
                ZielZeitpunktUtc = zielZeitpunktUtc,
                ErstelltAmUtc = jetzt,
                AktualisiertAmUtc = jetzt
            });
        }
        else
        {
            vorhanden.PromptText = promptText;
            vorhanden.ZielZeitpunktUtc = zielZeitpunktUtc;
            vorhanden.AktualisiertAmUtc = jetzt;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Entfernt den persistierten Plan der Aufgabe, falls vorhanden.</summary>
    public async Task LoeschenAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        var vorhanden = await _db.ZeitgesteuertePrompts
            .FirstOrDefaultAsync(z => z.AufgabeId == aufgabeId, ct);
        if (vorhanden is null)
            return;

        _db.ZeitgesteuertePrompts.Remove(vorhanden);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} gelöscht.", aufgabeId);
    }

    /// <summary>
    /// Schreibt den geplanten Prompt nur, wenn der vorhandene Datensatz noch dem erwarteten
    /// Vorgängerstand entspricht — bzw. wenn kein Datensatz existiert und auch keiner erwartet
    /// wird (<paramref name="erwartet"/> ist <c>null</c>). Lost-Update-Schutz für den
    /// Sofortversand-Fehlerpfad: Wurde während des in-flight-Versands bereits ein neuerer Plan
    /// persistiert, bleibt dessen Datensatz erhalten statt vom alten Plan überschrieben zu werden.
    /// </summary>
    /// <returns><c>true</c>, wenn der Datensatz geschrieben wurde; <c>false</c>, wenn ein abweichender Datensatz den Schreibzugriff verweigerte.</returns>
    public async Task<bool> UpsertWennUebereinstimmendAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, ScheduledPromptInfo? erwartet, CancellationToken ct = default)
    {
        var vorhanden = await _db.ZeitgesteuertePrompts
            .FirstOrDefaultAsync(z => z.AufgabeId == aufgabeId, ct);

        // ZielZeitpunktUtc wird als Unix-Millisekunden persistiert — den Vergleich auf dieselbe
        // Granularität normieren, sonst scheitert er an der Sub-Millisekunden-Genauigkeit.
        var stimmtUeberein = vorhanden is null
            ? erwartet is null
            : erwartet is not null
                && vorhanden.PromptText == erwartet.PromptText
                && vorhanden.ZielZeitpunktUtc.ToUnixTimeMilliseconds() == erwartet.TargetTime.ToUnixTimeMilliseconds();

        if (!stimmtUeberein)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} wird nicht mit dem Alt-Plan \"{AltText}\" ({AltZiel} UTC) überschrieben — der Datensatz wurde zwischenzeitlich durch einen neueren Plan ersetzt.",
                aufgabeId,
                promptText,
                zielZeitpunktUtc);
            return false;
        }

        await UpsertAsync(aufgabeId, promptText, zielZeitpunktUtc, ct);
        return true;
    }

    /// <summary>
    /// Entfernt den persistierten Plan der Aufgabe nach einem erfolgreichen Versand.
    /// Entspricht der Datensatz dem versendeten Plan, wird er immer gelöscht. Weicht er ab,
    /// wird er nur behalten, wenn <paramref name="neuererPlanAktiv"/> gesetzt ist — also zum
    /// Löschzeitpunkt noch ein lebender In-Memory-Plan für die Aufgabe existiert, dem der
    /// abweichende Datensatz gehören kann (Lost-Update-Schutz). Ohne aktiven Plan ist ein
    /// abweichender Datensatz eine Leiche des versendeten Plans (z. B. nach einem
    /// fehlgeschlagenen Store-Schreibzugriff) und wird mitgelöscht, damit der bereits
    /// zugestellte Prompt nach einem Neustart nicht erneut zugestellt wird.
    /// </summary>
    public async Task LoeschenNachVersandAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc, bool neuererPlanAktiv, CancellationToken ct = default)
    {
        var vorhanden = await _db.ZeitgesteuertePrompts
            .FirstOrDefaultAsync(z => z.AufgabeId == aufgabeId, ct);
        if (vorhanden is null)
            return;

        // ZielZeitpunktUtc wird als Unix-Millisekunden persistiert — den Vergleich auf dieselbe
        // Granularität normieren, sonst scheitert er an der Sub-Millisekunden-Genauigkeit.
        var stimmtUeberein = vorhanden.PromptText == promptText
            && vorhanden.ZielZeitpunktUtc.ToUnixTimeMilliseconds() == zielZeitpunktUtc.ToUnixTimeMilliseconds();

        if (!stimmtUeberein && neuererPlanAktiv)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} weicht vom versendeten Plan ab (Datensatz: \"{DatensatzText}\" / {DatensatzZiel} UTC, versendet: \"{VersendeterText}\" / {VersendetZiel} UTC) und bleibt erhalten — ein neuerer Plan ist aktiv.",
                aufgabeId,
                vorhanden.PromptText,
                vorhanden.ZielZeitpunktUtc,
                promptText,
                zielZeitpunktUtc);
            return;
        }

        if (!stimmtUeberein)
        {
            _logger.LogWarning(
                "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} weicht vom versendeten Plan ab (Datensatz: \"{DatensatzText}\" / {DatensatzZiel} UTC, versendet: \"{VersendeterText}\" / {VersendetZiel} UTC) und wird trotzdem gelöscht — ohne aktiven Plan kann er nur eine Leiche des versendeten Plans sein.",
                aufgabeId,
                vorhanden.PromptText,
                vorhanden.ZielZeitpunktUtc,
                promptText,
                zielZeitpunktUtc);
        }

        _db.ZeitgesteuertePrompts.Remove(vorhanden);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} gelöscht.", aufgabeId);
    }

    /// <summary>Aktualisiert den Zielzeitpunkt des persistierten Plans (z. B. Pausenverschiebung), falls vorhanden.</summary>
    public async Task UpdateZielZeitpunktAsync(Guid aufgabeId, DateTimeOffset zielZeitpunktUtc, CancellationToken ct = default)
    {
        var vorhanden = await _db.ZeitgesteuertePrompts
            .FirstOrDefaultAsync(z => z.AufgabeId == aufgabeId, ct);
        if (vorhanden is null)
            return;

        vorhanden.ZielZeitpunktUtc = zielZeitpunktUtc;
        vorhanden.AktualisiertAmUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
