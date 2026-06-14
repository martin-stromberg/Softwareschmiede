using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für Projektverwaltung (CRUD + Archivieren).</summary>
public sealed class ProjektService
{
    private const string LocalDirectoryPluginPrefix = "LocalDirectoryPlugin";
    private const string LegacyGitHubPluginType = "GitHub";
    private const string GitHubPluginPrefix = "Softwareschmiede.GitHub";
    private const string SourceDirectoryFieldKey = "SourceDirectory";
    private const string RepositoryUrlFieldKey = "RepositoryUrl";
    private const string RepositoryNameFieldKey = "RepositoryName";

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
                .ThenInclude(r => r.StartKonfiguration)
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
        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RepositoryUrlFieldKey] = repositoryUrl,
            [RepositoryNameFieldKey] = repositoryName
        };

        if (IsLocalDirectoryPlugin(pluginTyp))
        {
            fieldValues[SourceDirectoryFieldKey] = repositoryUrl;
        }

        return await AddRepositoryAsync(projektId, pluginTyp, fieldValues, ct);
    }

    /// <summary>Fügt ein Git-Repository über pluginabhängige Eingabefelder zu einem Projekt hinzu.</summary>
    public async Task<GitRepository> AddRepositoryAsync(
        Guid projektId,
        string pluginTyp,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken ct = default)
    {
        var normalizedPluginType = NormalizeRequiredValue(pluginTyp, nameof(pluginTyp));
        var normalizedFieldValues = NormalizeFieldValues(fieldValues);

        ValidateRequiredFields(normalizedPluginType, normalizedFieldValues);
        var repositoryUrl = ResolveRepositoryUrl(normalizedPluginType, normalizedFieldValues);
        var repositoryName = ResolveRepositoryName(normalizedPluginType, normalizedFieldValues, repositoryUrl);

        _logger.LogInformation("Repository '{RepositoryName}' zu Projekt {ProjektId} hinzufügen.", repositoryName, projektId);

        var projekt = await _db.Projekte.FindAsync([projektId], ct)
            ?? throw new InvalidOperationException($"Projekt {projektId} nicht gefunden.");

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            PluginTyp = normalizedPluginType,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            Aktiv = true
        };

        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Repository '{RepositoryName}' mit ID {RepositoryId} zu Projekt {ProjektId} hinzugefügt.", repositoryName, repository.Id, projektId);
        return repository;
    }

    /// <summary>Gibt alle bekannten Git-Repositories zurück.</summary>
    public async Task<IReadOnlyList<GitRepository>> GetAllRepositoriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Alle Repositories abrufen.");
        return await _db.GitRepositories
            .AsNoTracking()
            .OrderBy(r => r.RepositoryName)
            .ToListAsync(ct);
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

    /// <summary>Speichert die Startkonfiguration für ein Repository.</summary>
    public async Task<RepositoryStartKonfiguration> SaveRepositoryStartKonfigurationAsync(
        Guid repositoryId,
        string startScriptRelativePath,
        bool aktiv,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} speichern.", repositoryId);

        ValidateStartConfiguration(startScriptRelativePath);

        var repository = await _db.GitRepositories
            .Include(r => r.StartKonfiguration)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        var configuration = repository.StartKonfiguration ?? new RepositoryStartKonfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repositoryId
        };

        configuration.StartScriptRelativePath = NormalizeRequiredValue(startScriptRelativePath, nameof(startScriptRelativePath));
        configuration.Aktiv = aktiv;

        if (repository.StartKonfiguration is null)
        {
            _db.Add(configuration);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} gespeichert.", repositoryId);
        return configuration;
    }

    /// <summary>Liefert die Startkonfiguration eines Repositories.</summary>
    public async Task<RepositoryStartKonfiguration?> GetRepositoryStartKonfigurationAsync(Guid repositoryId, CancellationToken ct = default)
    {
        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} abrufen.", repositoryId);

        return await _db.Set<RepositoryStartKonfiguration>()
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.GitRepositoryId == repositoryId, ct);
    }

    private static Dictionary<string, string> NormalizeFieldValues(IReadOnlyDictionary<string, string> fieldValues)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fieldValues)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            normalized[pair.Key.Trim()] = pair.Value.Trim();
        }

        return normalized;
    }

    private static string NormalizeRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Der Wert '{parameterName}' ist erforderlich.");
        }

        return value.Trim();
    }

    private static void ValidateRequiredFields(string pluginType, IReadOnlyDictionary<string, string> fieldValues)
    {
        if (IsLocalDirectoryPlugin(pluginType))
        {
            if (!fieldValues.TryGetValue(SourceDirectoryFieldKey, out var sourceDirectory)
                || string.IsNullOrWhiteSpace(sourceDirectory))
            {
                throw new InvalidOperationException("Für LocalDirectoryPlugin ist 'SourceDirectory' ein Pflichtfeld.");
            }
        }

        if (IsGitHubPlugin(pluginType))
        {
            if (!fieldValues.TryGetValue(RepositoryUrlFieldKey, out var repositoryUrl)
                || string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new InvalidOperationException("Für GitHub ist 'RepositoryUrl' ein Pflichtfeld.");
            }

            if (!fieldValues.TryGetValue(RepositoryNameFieldKey, out var repositoryName)
                || string.IsNullOrWhiteSpace(repositoryName))
            {
                throw new InvalidOperationException("Für GitHub ist 'RepositoryName' ein Pflichtfeld.");
            }
        }
    }

    private static string ResolveRepositoryUrl(string pluginType, IReadOnlyDictionary<string, string> fieldValues)
    {
        if (IsLocalDirectoryPlugin(pluginType))
        {
            return fieldValues[SourceDirectoryFieldKey];
        }

        if (fieldValues.TryGetValue(RepositoryUrlFieldKey, out var repositoryUrl)
            && !string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return repositoryUrl;
        }

        throw new InvalidOperationException($"Für Plugin '{pluginType}' wurde kein gültiges RepositoryUrl-Feld übergeben.");
    }

    private static string ResolveRepositoryName(
        string pluginType,
        IReadOnlyDictionary<string, string> fieldValues,
        string repositoryUrl)
    {
        if (fieldValues.TryGetValue(RepositoryNameFieldKey, out var repositoryName)
            && !string.IsNullOrWhiteSpace(repositoryName))
        {
            return repositoryName;
        }

        var derivedName = DeriveRepositoryName(repositoryUrl);
        if (!string.IsNullOrWhiteSpace(derivedName))
        {
            return derivedName;
        }

        throw new InvalidOperationException($"Für Plugin '{pluginType}' konnte kein RepositoryName ermittelt werden.");
    }

    private static void ValidateStartConfiguration(string startScriptRelativePath)
    {
        if (string.IsNullOrWhiteSpace(startScriptRelativePath))
        {
            throw new InvalidOperationException("Der relative Pfad zum Startskript ist erforderlich.");
        }

        if (Path.IsPathRooted(startScriptRelativePath))
        {
            throw new InvalidOperationException("Das Startskript muss relativ zum Repository angegeben werden.");
        }
    }

    private static string DeriveRepositoryName(string repositoryValue)
    {
        var value = repositoryValue.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var repositoryUri))
        {
            var segment = repositoryUri.Segments.LastOrDefault()?.Trim('/').Trim();
            return string.IsNullOrWhiteSpace(segment) ? string.Empty : segment;
        }

        var path = Path.TrimEndingDirectorySeparator(value);
        return Path.GetFileName(path);
    }

    private static bool IsLocalDirectoryPlugin(string pluginType)
        => string.Equals(pluginType, LocalDirectoryPluginPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsGitHubPlugin(string pluginType)
        => string.Equals(pluginType, LegacyGitHubPluginType, StringComparison.OrdinalIgnoreCase)
           || string.Equals(pluginType, GitHubPluginPrefix, StringComparison.OrdinalIgnoreCase);
}
