using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Interfaces;

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
        if (!_options.Enabled)
        {
            return [];
        }

        var cacheKey = $"dirs:{repositoryUrl}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var structure = await gitPlugin.GetRepositoryStructureAsync(repositoryUrl, _options.MaxDepth, ct).ConfigureAwait(false);
            var directories = structure
                .Where(item => item.IsDirectory)
                .Select(item => item.Path)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            _cache.Set(cacheKey, directories, TimeSpan.FromSeconds(_options.CacheDurationSeconds));
            return directories;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur für {RepositoryUrl}.", repositoryUrl);
            return [];
        }
    }
}
