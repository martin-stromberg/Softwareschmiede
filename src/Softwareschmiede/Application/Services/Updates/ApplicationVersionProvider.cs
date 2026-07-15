using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services.Updates;

/// <summary>Liest die lokal installierte Programmversion aus <c>version.json</c>.</summary>
public sealed class ApplicationVersionProvider : IApplicationVersionProvider
{
    private readonly string _baseDirectory;
    private readonly ILogger<ApplicationVersionProvider> _logger;

    /// <inheritdoc cref="ApplicationVersionProvider"/>
    public ApplicationVersionProvider(ILogger<ApplicationVersionProvider> logger)
        : this(AppContext.BaseDirectory, logger)
    {
    }

    /// <summary>Erstellt den Provider mit einem expliziten Basispfad.</summary>
    public ApplicationVersionProvider(string baseDirectory, ILogger<ApplicationVersionProvider> logger)
    {
        _baseDirectory = baseDirectory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<InstalledVersionInfo?> GetInstalledVersionAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_baseDirectory, "version.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("version.json wurde nicht gefunden: {VersionPath}", path);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<VersionJson>(stream, cancellationToken: ct);
            if (document is null || string.IsNullOrWhiteSpace(document.Version))
            {
                _logger.LogWarning("version.json enthält keine Version: {VersionPath}", path);
                return null;
            }

            if (!UpdateVersionComparer.TryParse(document.Version, out _))
            {
                _logger.LogWarning("version.json enthält eine ungültige Version: {Version}", document.Version);
                return null;
            }

            return new InstalledVersionInfo(
                UpdateVersionComparer.Normalize(document.Version),
                document.TagName,
                document.Commit,
                document.CreatedAtUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            _logger.LogWarning(ex, "version.json konnte nicht gelesen werden: {VersionPath}", path);
            return null;
        }
    }

    private sealed class VersionJson
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tagName")]
        public string? TagName { get; set; }

        [JsonPropertyName("commit")]
        public string? Commit { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public DateTimeOffset? CreatedAtUtc { get; set; }
    }
}
