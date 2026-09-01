namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Unveraenderlicher lokaler und externer Repository-Kontext eines SCM-Vorschlags.</summary>
public sealed record ScmRepositoryContext
{
    /// <summary>Erstellt einen vollstaendigen Repository-Snapshot.</summary>
    public ScmRepositoryContext(Guid gitRepositoryId, string pluginPrefix, string repositoryId)
    {
        if (gitRepositoryId == Guid.Empty)
            throw new ArgumentException("Die lokale Repository-ID darf nicht leer sein.", nameof(gitRepositoryId));
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        GitRepositoryId = gitRepositoryId;
        PluginPrefix = pluginPrefix.Trim();
        RepositoryId = repositoryId.Trim();
    }

    /// <summary>ID des lokal persistierten Projekt-Repositories.</summary>
    public Guid GitRepositoryId { get; }

    /// <summary>Plugin-Prefix des Repository-Providers.</summary>
    public string PluginPrefix { get; }

    /// <summary>Kanonische API-Repository-ID des Providers.</summary>
    public string RepositoryId { get; }
}
