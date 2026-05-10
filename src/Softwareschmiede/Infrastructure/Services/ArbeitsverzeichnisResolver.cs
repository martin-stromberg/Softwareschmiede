using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Services;

/// <summary>Resolver für das effektive Arbeitsverzeichnis inkl. Runtime-Fallback.</summary>
public sealed class ArbeitsverzeichnisResolver : IArbeitsverzeichnisResolver
{
    private readonly ArbeitsverzeichnisSettingsService _settingsService;
    private readonly ILogger<ArbeitsverzeichnisResolver> _logger;

    /// <inheritdoc cref="ArbeitsverzeichnisResolver"/>
    public ArbeitsverzeichnisResolver(
        ArbeitsverzeichnisSettingsService settingsService,
        ILogger<ArbeitsverzeichnisResolver> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ArbeitsverzeichnisResolutionResult> ResolveAsync(CancellationToken ct = default)
    {
        var configuredPath = await _settingsService.GetArbeitsverzeichnisAsync(ct);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return CreateFallback("no-configured-path", configuredPath);
        }

        string fullPath;
        try
        {
            fullPath = ArbeitsverzeichnisSettingsService.NormalizePath(configuredPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WorkdirValidationFailed: configuredPath={ConfiguredPath}, reasonCode={ReasonCode}", configuredPath, "invalid-path");
            return CreateFallback("invalid-path", configuredPath);
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            var probeFile = Path.Combine(fullPath, $".workdir-probe-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probeFile, "probe", ct);
            File.Delete(probeFile);

            _logger.LogInformation("WorkdirResolved: configuredPath={ConfiguredPath}, resolvedPath={ResolvedPath}, reasonCode={ReasonCode}, isFallback={IsFallback}",
                configuredPath, fullPath, "configured", false);
            return new ArbeitsverzeichnisResolutionResult(fullPath, false, "configured", configuredPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WorkdirValidationFailed: configuredPath={ConfiguredPath}, reasonCode={ReasonCode}", configuredPath, "not-writable-or-unavailable");
            return CreateFallback("not-writable-or-unavailable", configuredPath);
        }
    }

    private ArbeitsverzeichnisResolutionResult CreateFallback(string reasonCode, string? configuredPath)
    {
        var fallbackPath = Path.GetTempPath();
        _logger.LogWarning("WorkdirFallbackUsed: configuredPath={ConfiguredPath}, resolvedPath={ResolvedPath}, reasonCode={ReasonCode}, isFallback={IsFallback}",
            configuredPath, fallbackPath, reasonCode, true);
        return new ArbeitsverzeichnisResolutionResult(fallbackPath, true, reasonCode, configuredPath);
    }
}
