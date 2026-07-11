using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet Promptvorlagen inklusive initialer Standardvorlagen.</summary>
public sealed class PromptVorlagenService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<PromptVorlagenService> _logger;

    /// <inheritdoc cref="PromptVorlagenService"/>
    public PromptVorlagenService(SoftwareschmiededDbContext db, ILogger<PromptVorlagenService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lädt alle Promptvorlagen sortiert fuer die Anzeige.</summary>
    public async Task<IReadOnlyList<PromptVorlage>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.PromptVorlagen
            .AsNoTracking()
            .OrderBy(v => v.Sortierung)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);
    }

    /// <summary>Legt eine neue Promptvorlage an.</summary>
    public async Task<PromptVorlage> CreateAsync(string name, string prompttext, CancellationToken ct = default)
    {
        Validate(name, prompttext);

        var sortierung = await _db.PromptVorlagen.AnyAsync(ct)
            ? await _db.PromptVorlagen.MaxAsync(v => v.Sortierung, ct) + 1
            : 0;
        var now = DateTimeOffset.UtcNow;
        var vorlage = new PromptVorlage
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Prompttext = prompttext,
            Sortierung = sortierung,
            ErstelltAm = now,
            AktualisiertAm = now
        };

        _db.PromptVorlagen.Add(vorlage);
        await _db.SaveChangesAsync(ct);
        return vorlage;
    }

    /// <summary>Aktualisiert eine bestehende Promptvorlage.</summary>
    public async Task UpdateAsync(Guid id, string name, string prompttext, CancellationToken ct = default)
    {
        Validate(name, prompttext);

        var vorlage = await _db.PromptVorlagen.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new InvalidOperationException($"Promptvorlage '{id}' wurde nicht gefunden.");

        vorlage.Name = name.Trim();
        vorlage.Prompttext = prompttext;
        vorlage.AktualisiertAm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Loescht eine Promptvorlage.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var vorlage = await _db.PromptVorlagen.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vorlage is null)
            return;

        _db.PromptVorlagen.Remove(vorlage);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Legt die initialen Promptvorlagen einmalig an, falls noch keine Vorlage existiert.</summary>
    public async Task EnsureInitialPromptVorlagenAsync(CancellationToken ct = default)
    {
        if (await _db.PromptVorlagen.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        _db.PromptVorlagen.AddRange(
            new PromptVorlage
            {
                Name = "Initialanforderung senden",
                Prompttext = "Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.",
                Sortierung = 0,
                ErstelltAm = now,
                AktualisiertAm = now
            },
            new PromptVorlage
            {
                Name = "Weitermachen",
                Prompttext = "Mach bitte weiter",
                Sortierung = 1,
                ErstelltAm = now,
                AktualisiertAm = now
            },
            new PromptVorlage
            {
                Name = "Pullrequest",
                Prompttext = "Push nun alle Commits und erstelle einen PR",
                Sortierung = 2,
                ErstelltAm = now,
                AktualisiertAm = now
            });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Initiale Promptvorlagen wurden angelegt.");
    }

    private static void Validate(string name, string prompttext)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Der Name der Promptvorlage darf nicht leer sein.", nameof(name));
        if (string.IsNullOrWhiteSpace(prompttext))
            throw new ArgumentException("Der Prompttext darf nicht leer sein.", nameof(prompttext));
    }
}
