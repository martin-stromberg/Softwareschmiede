using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für Projektverwaltung (CRUD + Archivieren).</summary>
public sealed class ProjectService
{
    private const string LocalDirectoryPluginPrefix = "LocalDirectoryPlugin";
    private const string LegacyGitHubPluginType = "GitHub";
    private const string GitHubPluginPrefix = "Softwareschmiede.GitHub";
    private const string BitbucketPluginPrefix = "Softwareschmiede.Bitbucket";
    private const string SourceDirectoryFieldKey = "SourceDirectory";
    private const string RepositoryUrlFieldKey = "RepositoryUrl";
    private const string RepositoryNameFieldKey = "RepositoryName";

    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<ProjectService> _logger;
    private readonly IPluginManager _pluginManager;

    /// <inheritdoc cref="ProjectService"/>
    public ProjectService(SoftwareschmiedeDbContext db, ILogger<ProjectService> logger, IPluginManager? pluginManager = null)
    {
        _db = db;
        _logger = logger;
        _pluginManager = pluginManager ?? NullPluginManager.Instance;
    }

    /// <summary>Gibt alle Projekte zurück.</summary>
    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Alle Projekte abrufen.");
        return await _db.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    /// <summary>Gibt ein Projekt anhand seiner ID zurück.</summary>
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} abrufen.", id);
        return await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>Gibt ein Projekt mit Repositories und Aufgaben zurück.</summary>
    public async Task<Project?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} mit Details abrufen.", id);
        return await _db.Projects
            .AsNoTracking()
            .Include(p => p.Repositories)
                .ThenInclude(r => r.StartConfiguration)
            .Include(p => p.Repositories)
                .ThenInclude(r => r.InitializationConfiguration)
            .Include(p => p.WorkItems)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>Erstellt ein neues Projekt.</summary>
    public async Task<Project> CreateAsync(string name, string? description, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt '{Name}' erstellen.", name);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt '{Name}' mit ID {ProjektId} erstellt.", name, project.Id);
        return project;
    }

    /// <summary>Aktualisiert Name und Beschreibung eines Projekts.</summary>
    public async Task<Project> UpdateAsync(Guid id, string name, string? description, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} aktualisieren.", id);

        var project = await _db.Projects.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        project.Name = name;
        project.Description = description;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Projekt {ProjektId} aktualisiert.", id);
        return project;
    }

    /// <summary>Archiviert ein Projekt.</summary>
    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} archivieren.", id);

        var project = await _db.Projects.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        project.Status = ProjectStatus.Archived;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt {ProjektId} archiviert.", id);
    }

    /// <summary>Löscht ein Projekt inkl. aller verknüpften Daten.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Projekt {ProjektId} löschen.", id);

        var project = await _db.Projects.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Projekt {id} nicht gefunden.");

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Projekt {ProjektId} gelöscht.", id);
    }

    /// <summary>Fügt ein Git-Repository zu einem Projekt hinzu.</summary>
    public async Task<GitRepository> AddRepositoryAsync(
        Guid projectId,
        string pluginTyp,
        string repositoryUrl,
        string repositoryName,
        string? defaultSourceBranchName = null,
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

        var repository = await AddRepositoryAsync(projectId, pluginTyp, fieldValues, ct);

        if (!string.IsNullOrWhiteSpace(defaultSourceBranchName))
        {
            repository.DefaultSourceBranchName = defaultSourceBranchName.Trim();
            await _db.SaveChangesAsync(ct);
        }

        return repository;
    }

    /// <summary>Fügt ein Git-Repository über pluginabhängige Eingabefelder zu einem Projekt hinzu.</summary>
    public async Task<GitRepository> AddRepositoryAsync(
        Guid projectId,
        string pluginTyp,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken ct = default)
    {
        var normalizedPluginType = NormalizeRequiredValue(pluginTyp, nameof(pluginTyp));
        var normalizedFieldValues = NormalizeFieldValues(fieldValues);

        ValidateRequiredFields(normalizedPluginType, normalizedFieldValues);
        var repositoryUrl = ResolveRepositoryUrl(normalizedPluginType, normalizedFieldValues);
        var repositoryName = ResolveRepositoryName(normalizedPluginType, normalizedFieldValues, repositoryUrl);

        _logger.LogInformation("Repository '{RepositoryName}' zu Projekt {ProjektId} hinzufügen.", repositoryName, projectId);

        var project = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new InvalidOperationException($"Projekt {projectId} nicht gefunden.");

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PluginType = normalizedPluginType,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            Active = true
        };

        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Repository '{RepositoryName}' mit ID {RepositoryId} zu Projekt {ProjektId} hinzugefügt.", repositoryName, repository.Id, projectId);
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

    /// <summary>Setzt das Issue-Aufbereitungs-Flag eines Repositories (wirkt nur gemeinsam mit
    /// dem globalen Funktionsschalter <c>scm.issueaufbereitung.enabled</c>).</summary>
    /// <param name="repositoryId">Die Repository-ID.</param>
    /// <param name="aktiv">Der neue Flag-Wert.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task SetIssuePreparationEnabledAsync(Guid repositoryId, bool aktiv, CancellationToken ct = default)
    {
        var repository = await _db.GitRepositories.FindAsync([repositoryId], ct)
            ?? throw new InvalidOperationException("Repository nicht gefunden.");
        repository.IssuePreparationEnabled = aktiv;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Issue-Aufbereitung für Repository {RepositoryId} auf {Aktiv} gesetzt.", repositoryId, aktiv);
    }

    /// <summary>Gibt alle unzugeordneten Repositories aus allen SCM-Plugins zurück, sortiert nach UpdatedAt absteigend, dann Name aufsteigend.</summary>
    public async Task<IEnumerable<AvailableRepository>> GetUnassignedRepositoriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Unzugeordnete Repositories aus allen SCM-Plugins laden.");

        var assignedUrls = await _db.GitRepositories
            .AsNoTracking()
            .Select(r => r.RepositoryUrl)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        var allRepositories = new List<AvailableRepository>();
        foreach (var plugin in _pluginManager.GetSourceCodeManagementPlugins())
        {
            try
            {
                var repos = await plugin.GetAvailableRepositoriesAsync(ct);
                allRepositories.AddRange(repos);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginName}' konnte keine Repositories liefern.", plugin.PluginName);
            }
        }

        return allRepositories
            .Where(r => !string.IsNullOrEmpty(r.Url) && !assignedUrls.Contains(r.Url))
            .OrderByDescending(r => r.UpdatedAt)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
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
    public async Task<RepositoryStartConfiguration> SaveRepositoryStartConfigurationAsync(
        Guid repositoryId,
        string startScriptRelativePath,
        bool aktiv,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} speichern.", repositoryId);

        ValidateStartConfiguration(startScriptRelativePath);

        var repository = await _db.GitRepositories
            .Include(r => r.StartConfiguration)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        var configuration = repository.StartConfiguration ?? new RepositoryStartConfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repositoryId
        };

        configuration.StartScriptRelativePath = NormalizeRequiredValue(startScriptRelativePath, nameof(startScriptRelativePath));
        configuration.Active = aktiv;

        if (repository.StartConfiguration is null)
        {
            _db.Add(configuration);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} gespeichert.", repositoryId);
        return configuration;
    }

    /// <summary>Liefert die Startkonfiguration eines Repositories.</summary>
    public async Task<RepositoryStartConfiguration?> GetRepositoryStartConfigurationAsync(Guid repositoryId, CancellationToken ct = default)
    {
        _logger.LogInformation("Startkonfiguration für Repository {RepositoryId} abrufen.", repositoryId);

        return await _db.Set<RepositoryStartConfiguration>()
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.GitRepositoryId == repositoryId, ct);
    }

    /// <summary>
    /// Speichert den relativen Pfad zum Initialisierungsskript für ein Repository. <c>null</c> oder ein leerer
    /// Wert löschen eine bestehende Konfiguration.
    /// </summary>
    public async Task<RepositoryInitializationConfiguration?> SaveRepositoryInitializationScriptAsync(
        Guid repositoryId,
        string? initializationScriptRelativePath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Initialisierungskonfiguration für Repository {RepositoryId} speichern.", repositoryId);

        var repository = await _db.GitRepositories
            .Include(r => r.InitializationConfiguration)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        if (string.IsNullOrWhiteSpace(initializationScriptRelativePath))
        {
            if (repository.InitializationConfiguration is not null)
            {
                _db.Remove(repository.InitializationConfiguration);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Initialisierungskonfiguration für Repository {RepositoryId} gelöscht.", repositoryId);
            }

            return null;
        }

        ValidateInitializationConfiguration(initializationScriptRelativePath);

        var configuration = repository.InitializationConfiguration ?? new RepositoryInitializationConfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repositoryId
        };

        configuration.InitializationScriptRelativePath = NormalizeRequiredValue(initializationScriptRelativePath, nameof(initializationScriptRelativePath));

        if (repository.InitializationConfiguration is null)
        {
            _db.Add(configuration);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Initialisierungskonfiguration für Repository {RepositoryId} gespeichert.", repositoryId);
        return configuration;
    }

    /// <summary>
    /// Speichert das relative Arbeitsverzeichnis für ein Repository (z. B. bei der Zuweisung zu einem Projekt).
    /// <c>null</c> oder <c>"."</c> werden als Repository-Root behandelt und als <c>null</c> persistiert.
    /// Eine bereits vorhandene Startkonfiguration (Startskript) bleibt dabei unverändert.
    /// </summary>
    /// <param name="repositoryId">ID des Repositories.</param>
    /// <param name="workingDirectoryRelativePath">Relativer Pfad zum Arbeitsverzeichnis, oder <c>null</c>/<c>"."</c> für das Repository-Root.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task SaveRepositoryWorkingDirectoryAsync(Guid repositoryId, string? workingDirectoryRelativePath, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(workingDirectoryRelativePath) || workingDirectoryRelativePath == "."
            ? null
            : workingDirectoryRelativePath;

        var repository = await _db.GitRepositories
            .Include(r => r.StartConfiguration)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        if (repository.StartConfiguration is null && normalized is null)
        {
            return;
        }

        var configuration = repository.StartConfiguration ?? new RepositoryStartConfiguration
        {
            Id = Guid.NewGuid(),
            GitRepositoryId = repositoryId
        };

        configuration.WorkingDirectoryRelativePath = normalized;

        if (repository.StartConfiguration is null)
        {
            _db.Add(configuration);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Arbeitsverzeichnis für Repository {RepositoryId} gespeichert: {WorkingDirectory}", repositoryId, normalized ?? ".");
    }

    /// <summary>
    /// Aktualisiert den konfigurierten Basis-Branch eines Repositories, von dem neue Feature-Branches
    /// abgezweigt werden. <c>null</c> oder ein leerer Wert setzen die Konfiguration zurück auf den
    /// Remote-Standard-Branch (Fallback).
    /// </summary>
    /// <param name="repositoryId">ID des Repositories.</param>
    /// <param name="defaultSourceBranchName">Name des konfigurierten Basis-Branch, oder <c>null</c> für den Remote-Standard-Branch.</param>
    /// <param name="ct">Abbruch-Token.</param>
    public async Task<GitRepository> UpdateRepositorySourceBranchAsync(Guid repositoryId, string? defaultSourceBranchName, CancellationToken ct = default)
    {
        _logger.LogInformation("Basis-Branch für Repository {RepositoryId} aktualisieren.", repositoryId);

        var repository = await _db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} nicht gefunden.");

        repository.DefaultSourceBranchName = string.IsNullOrWhiteSpace(defaultSourceBranchName)
            ? null
            : defaultSourceBranchName.Trim();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Basis-Branch für Repository {RepositoryId} aktualisiert: {DefaultSourceBranchName}", repositoryId, repository.DefaultSourceBranchName ?? "(Standard)");
        return repository;
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

        if (IsBitbucketPlugin(pluginType))
        {
            if (!fieldValues.TryGetValue(RepositoryUrlFieldKey, out var repositoryUrl)
                || string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new InvalidOperationException("Für Bitbucket ist 'RepositoryUrl' ein Pflichtfeld.");
            }

            if (!fieldValues.TryGetValue(RepositoryNameFieldKey, out var repositoryName)
                || string.IsNullOrWhiteSpace(repositoryName))
            {
                throw new InvalidOperationException("Für Bitbucket ist 'RepositoryName' ein Pflichtfeld.");
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
        => ValidateRelativeScriptPath(startScriptRelativePath, "Startskript");

    private static void ValidateInitializationConfiguration(string initializationScriptRelativePath)
        => ValidateRelativeScriptPath(initializationScriptRelativePath, "Initialisierungsskript");

    private static void ValidateRelativeScriptPath(string relativePath, string scriptLabel)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"Der relative Pfad zum {scriptLabel} ist erforderlich.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Das {scriptLabel} muss relativ zum Repository angegeben werden.");
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

    private static bool IsBitbucketPlugin(string pluginType)
        => string.Equals(pluginType, BitbucketPluginPrefix, StringComparison.OrdinalIgnoreCase);

    private sealed class NullPluginManager : IPluginManager
    {
        public static readonly NullPluginManager Instance = new();

        public IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins() => [];
        public IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins() => [];
        public IGitPlugin GetDefaultSourceCodeManagementPlugin() => throw new InvalidOperationException("Kein SCM-Plugin verfügbar.");
        public IKiPlugin GetDefaultDevelopmentAutomationPlugin() => throw new InvalidOperationException("Kein KI-Plugin verfügbar.");
        public IReadOnlyList<IIdePlugin> GetIdePlugins() => [];
        public IIdePlugin GetDefaultIdePlugin() => throw new InvalidOperationException("Kein IDE-Plugin verfügbar.");
    }
}
