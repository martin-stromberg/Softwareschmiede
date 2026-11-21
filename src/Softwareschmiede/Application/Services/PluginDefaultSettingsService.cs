using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet persistente Standard-Plugins je <see cref="PluginType"/>.</summary>
public sealed class PluginDefaultSettingsService
{
    private const string DefaultKeyPrefix = "plugins.default.";

    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<PluginDefaultSettingsService> _logger;

    /// <inheritdoc cref="PluginDefaultSettingsService"/>
    public PluginDefaultSettingsService(
        SoftwareschmiedeDbContext db,
        ILogger<PluginDefaultSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liest die gespeicherte Standard-Plugin-ID (PluginPrefix) für den angegebenen Typ.</summary>
    public async Task<string?> GetDefaultPluginPrefixAsync(PluginType pluginType, CancellationToken ct = default)
    {
        var key = BuildKey(pluginType);
        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        return string.IsNullOrWhiteSpace(setting?.Value)
            ? null
            : setting.Value.Trim();
    }

    /// <summary>Speichert (oder entfernt) die Standard-Plugin-ID (PluginPrefix) für den angegebenen Typ.</summary>
    public async Task SaveDefaultPluginPrefixAsync(PluginType pluginType, string? pluginPrefix, CancellationToken ct = default)
    {
        var key = BuildKey(pluginType);
        var normalizedPrefix = string.IsNullOrWhiteSpace(pluginPrefix) ? null : pluginPrefix.Trim();
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = key
            };
            _db.AppSettings.Add(setting);
        }

        setting.Value = normalizedPrefix;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Standard-Plugin gespeichert: {PluginType} => {PluginPrefix}",
            pluginType,
            normalizedPrefix ?? "<none>");
    }

    /// <summary>Liest den gespeicherten Projekt-Standard-Plugin-Prefix für den angegebenen Typ.</summary>
    public async Task<string?> GetProjectDefaultPluginPrefixAsync(Guid projectId, PluginType pluginType, CancellationToken ct = default)
    {
        var key = BuildProjectKey(projectId, pluginType);
        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        return string.IsNullOrWhiteSpace(setting?.Value)
            ? null
            : setting.Value.Trim();
    }

    /// <summary>Speichert (oder entfernt) den Projekt-Standard-Plugin-Prefix für den angegebenen Typ.</summary>
    public async Task SaveProjectDefaultPluginPrefixAsync(Guid projectId, PluginType pluginType, string? pluginPrefix, CancellationToken ct = default)
    {
        var key = BuildProjectKey(projectId, pluginType);
        var normalizedPrefix = string.IsNullOrWhiteSpace(pluginPrefix) ? null : pluginPrefix.Trim();
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = key
            };
            _db.AppSettings.Add(setting);
        }

        setting.Value = normalizedPrefix;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Projekt-Standard-Plugin gespeichert: Projekt {ProjektId}, {PluginType} => {PluginPrefix}",
            projectId,
            pluginType,
            normalizedPrefix ?? "<none>");
    }

    private static string BuildKey(PluginType pluginType) => $"{DefaultKeyPrefix}{pluginType}";

    private static string BuildProjectKey(Guid projectId, PluginType pluginType) => $"{DefaultKeyPrefix}project.{projectId}.{pluginType}";
}
