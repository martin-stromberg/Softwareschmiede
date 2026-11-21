using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet Promptvorlagen inklusive initialer Standardvorlagen.</summary>
public sealed class PromptTemplateService
{
    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<PromptTemplateService> _logger;

    /// <inheritdoc cref="PromptTemplateService"/>
    public PromptTemplateService(SoftwareschmiedeDbContext db, ILogger<PromptTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lädt alle Promptvorlagen sortiert fuer die Anzeige.</summary>
    public async Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.PromptTemplates
            .AsNoTracking()
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);
    }

    /// <summary>Legt eine neue Promptvorlage an.</summary>
    public async Task<PromptTemplate> CreateAsync(string name, string promptText, CancellationToken ct = default)
    {
        Validate(name, promptText);

        var sortOrder = await _db.PromptTemplates.AnyAsync(ct)
            ? await _db.PromptTemplates.MaxAsync(v => v.SortOrder, ct) + 1
            : 0;
        var now = DateTimeOffset.UtcNow;
        var template = new PromptTemplate
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            PromptText = promptText,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.PromptTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    /// <summary>Aktualisiert eine bestehende Promptvorlage.</summary>
    public async Task UpdateAsync(Guid id, string name, string promptText, CancellationToken ct = default)
    {
        Validate(name, promptText);

        var template = await _db.PromptTemplates.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new InvalidOperationException($"Promptvorlage '{id}' wurde nicht gefunden.");

        template.Name = name.Trim();
        template.PromptText = promptText;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Loescht eine Promptvorlage.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.PromptTemplates.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (template is null)
            return;

        _db.PromptTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Legt die initialen Promptvorlagen einmalig an, falls noch keine Vorlage existiert.</summary>
    public async Task EnsureInitialPromptTemplatesAsync(CancellationToken ct = default)
    {
        if (await _db.PromptTemplates.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        _db.PromptTemplates.AddRange(
            new PromptTemplate
            {
                Name = "Initialanforderung senden",
                PromptText = "Die Anforderung zum Thema '%TaskName%' ist in issue.md beschrieben.",
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now
            },
            new PromptTemplate
            {
                Name = "Weitermachen",
                PromptText = "Mach bitte weiter",
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new PromptTemplate
            {
                Name = "Pullrequest",
                PromptText = "Push nun alle Commits und erstelle einen PR",
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Initiale Promptvorlagen wurden angelegt.");
    }

    private static void Validate(string name, string promptText)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Der Name der Promptvorlage darf nicht leer sein.", nameof(name));
        if (string.IsNullOrWhiteSpace(promptText))
            throw new ArgumentException("Der Prompttext darf nicht leer sein.", nameof(promptText));
    }
}
