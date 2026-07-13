using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Ruft die Verzeichnisstruktur externer Repositories über Git-Plugins ab und cacht die Ergebnisse.</summary>
public sealed class DirectoryStructureBrowserService
{
    private readonly IMemoryCache _cache;
    private readonly DirectoryStructureOptions _options;
    private readonly ILogger<DirectoryStructureBrowserService> _logger;

    /// <inheritdoc cref="DirectoryStructureBrowserService"/>
    public DirectoryStructureBrowserService(
        IMemoryCache cache,
        IOptions<DirectoryStructureOptions> options,
        ILogger<DirectoryStructureBrowserService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Ruft die Verzeichnisse eines externen Repositories ab. Ergebnisse werden mit TTL gecacht.</summary>
    /// <param name="gitPlugin">Das zu verwendende Git-Plugin.</param>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Liste der relativen Verzeichnis-Pfade, oder eine leere Liste bei Fehlern.</returns>
    public async Task<List<string>> GetDirectoriesAsync(IGitPlugin gitPlugin, string repositoryUrl, CancellationToken ct = default)
    {
        var result = await GetDirectoryLoadResultAsync(gitPlugin, repositoryUrl, ct).ConfigureAwait(false);
        return result.Status == RepositoryStructureLoadStatus.Success
            ? result.Entries.Select(entry => entry.Path).ToList()
            : [];
    }

    /// <summary>Ruft die Verzeichnisstruktur eines externen Repositories mit explizitem Lade-Status ab.</summary>
    /// <param name="gitPlugin">Das zu verwendende Git-Plugin.</param>
    /// <param name="repositoryUrl">URL des Repositories.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Erfolgreich geladene, sortierte Verzeichnisse oder einen Fehler-/Nicht-unterstützt-Status.</returns>
    public async Task<RepositoryStructureLoadResult> GetDirectoryLoadResultAsync(IGitPlugin gitPlugin, string repositoryUrl, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return RepositoryStructureLoadResult.NotSupported("Der Abruf der Verzeichnisstruktur ist deaktiviert.");
        }

        var pluginPrefix = string.IsNullOrWhiteSpace(gitPlugin.PluginPrefix) ? gitPlugin.GetType().FullName : gitPlugin.PluginPrefix;
        var cacheKey = $"dirs:{pluginPrefix}:{_options.MaxDepth}:{repositoryUrl}";
        if (_cache.TryGetValue(cacheKey, out RepositoryStructureLoadResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var loadResult = await gitPlugin.GetRepositoryStructureLoadResultAsync(repositoryUrl, _options.MaxDepth, ct).ConfigureAwait(false);
            if (loadResult.Status != RepositoryStructureLoadStatus.Success)
            {
                _logger.LogWarning(
                    "Verzeichnisstruktur für {RepositoryUrl} konnte nicht geladen werden ({Status}): {Message}",
                    repositoryUrl,
                    loadResult.Status,
                    loadResult.Message);
                return loadResult;
            }

            var directories = loadResult.Entries
                .Where(item => item.IsDirectory)
                .Select(item => item.Path)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new RepositoryDirectoryEntry(path, IsDirectory: true))
                .ToList();

            var result = RepositoryStructureLoadResult.Success(directories);
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.CacheDurationSeconds));
            return result;
        }
        catch (OperationCanceledException)
        {
            // Abbruch (z. B. durch Repository-/Plugin-Wechsel in der UI) ist kein Fehler und muss
            // an den Aufrufer durchgereicht werden, statt als Warnung geloggt und geschluckt zu werden.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur für {RepositoryUrl}.", repositoryUrl);
            return RepositoryStructureLoadResult.Failed(ex.Message);
        }
    }
}
