using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für Projektverwaltung (CRUD + Archivieren).</summary>
public sealed class ProjektService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<ProjektService> _logger;

    /// <inheritdoc cref="ProjektService"/>
    public ProjektService(SoftwareschmiededDbContext db, ILogger<ProjektService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Gibt alle Projekte zurück.</summary>
    public async Task<IReadOnlyList<Projekt>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Alle Projekte abrufen.");
        return await _db.Projekte
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    /// <summary>Gibt ein Projekt anhand seiner ID zurück.</summary>
    public async Task<Projekt?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} abrufen.", id);
        return await _db.Projekte
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>Gibt ein Projekt mit Repositories und Aufgaben zurück.</summary>
    public async Task<Projekt?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} mit Details abrufen.", id);
        return await _db.Projekte
            .AsNoTracking()
            .Include(p => p.Repositories)
            .Include(p => p.Aufgaben)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>Erstellt ein neues Projekt.</summary>
    public async Task<Projekt> CreateAsync(string name, string? beschreibung, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt '{Name}' erstellen.", name);

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = name,
            Beschreibung = beschreibung,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        };

        _db.Projekte.Add(projekt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt '{Name}' mit ID {ProjektId} erstellt.", name, projekt.Id);
        return projekt;
    }

    /// <summary>Aktualisiert Name und Beschreibung eines Projekts.</summary>
    public async Task<Projekt> UpdateAsync(Guid id, string name, string? beschreibung, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} aktualisieren.", id);

        var projekt = await _db.Projekte.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        projekt.Name = name;
        projekt.Beschreibung = beschreibung;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Projekt {ProjektId} aktualisiert.", id);
        return projekt;
    }

    /// <summary>Archiviert ein Projekt.</summary>
    public async Task ArchivierenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} archivieren.", id);

        var projekt = await _db.Projekte.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        projekt.Status = ProjektStatus.Archiviert;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt {ProjektId} archiviert.", id);
    }

    /// <summary>Löscht ein Projekt inkl. aller verknüpften Daten.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} löschen.", id);

        var projekt = await _db.Projekte.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        _db.Projekte.Remove(projekt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt {ProjektId} gelöscht.", id);
    }

    /// <summary>Fügt ein Git-Repository zu einem Projekt hinzu.</summary>
    public async Task<GitRepository> AddRepositoryAsync(
        Guid projektId,
        string pluginTyp,
        string repositoryUrl,
        string repositoryName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Repository '{RepositoryName}' zu Projekt {ProjektId} hinzufügen.", repositoryName, projektId);

        var projekt = await _db.Projekte.FindAsync([projektId], ct)
            ?? throw new InvalidOperationException($"Projekt {projektId} nicht gefunden.");

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            PluginTyp = pluginTyp,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            Aktiv = true
        };

        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Repository '{RepositoryName}' mit ID {RepositoryId} zu Projekt {ProjektId} hinzugefügt.", repositoryName, repository.Id, projektId);
        return repository;
    }

    /// <summary>Entfernt ein Git-Repository aus einem Projekt.</summary>
    public async Task RemoveRepositoryAsync(Guid repositoryId, CancellationToken ct = default)
    {
        _logger.LogInformation("Repository {RepositoryId} entfernen.", repositoryId);

        var repository = await _db.GitRepositories.FindAsync([repositoryId], ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        _db.GitRepositories.Remove(repository);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Repository {RepositoryId} entfernt.", repositoryId);
    }
}
