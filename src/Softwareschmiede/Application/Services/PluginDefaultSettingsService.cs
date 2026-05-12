using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet persistente Standard-Plugins je <see cref="PluginType"/>.</summary>
public sealed class PluginDefaultSettingsService
{
    private const string DefaultKeyPrefix = "plugins.default.";

    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<PluginDefaultSettingsService> _logger;

    /// <inheritdoc cref="PluginDefaultSettingsService"/>
    public PluginDefaultSettingsService(
        SoftwareschmiededDbContext db,
        ILogger<PluginDefaultSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liest die gespeicherte Standard-Plugin-ID (PluginPrefix) für den angegebenen Typ.</summary>
    public async Task<string?> GetDefaultPluginPrefixAsync(PluginType pluginType, CancellationToken ct = default)
    {
        var key = BuildKey(pluginType);
        var setting = await _db.AppEinstellungen
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Schluessel == key, ct);

        return string.IsNullOrWhiteSpace(setting?.Wert)
            ? null
            : setting.Wert.Trim();
    }

    /// <summary>Speichert (oder entfernt) die Standard-Plugin-ID (PluginPrefix) für den angegebenen Typ.</summary>
    public async Task SaveDefaultPluginPrefixAsync(PluginType pluginType, string? pluginPrefix, CancellationToken ct = default)
    {
        var key = BuildKey(pluginType);
        var normalizedPrefix = string.IsNullOrWhiteSpace(pluginPrefix) ? null : pluginPrefix.Trim();
        var setting = await _db.AppEinstellungen
            .FirstOrDefaultAsync(s => s.Schluessel == key, ct);

        if (setting is null)
        {
            setting = new AppEinstellung
            {
                Id = Guid.NewGuid(),
                Schluessel = key
            };
            _db.AppEinstellungen.Add(setting);
        }

        setting.Wert = normalizedPrefix;
        setting.AktualisiertAm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Standard-Plugin gespeichert: {PluginType} => {PluginPrefix}",
            pluginType,
            normalizedPrefix ?? "<none>");
    }

    private static string BuildKey(PluginType pluginType) => $"{DefaultKeyPrefix}{pluginType}";
}
