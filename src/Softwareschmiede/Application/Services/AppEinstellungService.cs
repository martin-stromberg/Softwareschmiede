using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Generischer Service zum Lesen und Schreiben von Anwendungseinstellungen (Key-Value-Paare).
/// </summary>
public sealed class AppEinstellungService
{
    /// <summary>Schlüssel für die X-Koordinate des Hauptfensters.</summary>
    public const string WindowPositionXKey = "window.position.x";

    /// <summary>Schlüssel für die Y-Koordinate des Hauptfensters.</summary>
    public const string WindowPositionYKey = "window.position.y";

    /// <summary>Schlüssel für die Breite des Hauptfensters.</summary>
    public const string WindowWidthKey = "window.size.width";

    /// <summary>Schlüssel für die Höhe des Hauptfensters.</summary>
    public const string WindowHeightKey = "window.size.height";

    /// <summary>Schlüssel für den Dark-Mode-Status.</summary>
    public const string DesignModeKey = "ui.designmode.name";

    /// <summary>Schlüssel für das Standard-KI-Plugin.</summary>
    public const string DefaultKiPluginKey = "ki.plugin.default";

    /// <summary>Schlüssel für das Log-Level.</summary>
    public const string LogLevelKey = "logging.level";

    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<AppEinstellungService> _logger;

    /// <inheritdoc cref="AppEinstellungService"/>
    public AppEinstellungService(
        SoftwareschmiededDbContext db,
        ILogger<AppEinstellungService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liest den Wert einer Einstellung. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist.</summary>
    public async Task<string?> GetSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var einstellung = await _db.AppEinstellungen
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Schluessel == schluessel, ct);

        return einstellung?.Wert;
    }

    /// <summary>Liest den Wert einer Einstellung als <see cref="int"/>. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist oder der Wert kein gültiger Integer ist.</summary>
    public async Task<int?> GetIntSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(schluessel, ct);
        if (int.TryParse(wert, out var result))
            return result;
        return null;
    }

    /// <summary>Liest den Wert einer Einstellung als <see cref="bool"/>. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist oder der Wert kein gültiger Boolean ist.</summary>
    public async Task<bool?> GetBoolSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(schluessel, ct);
        if (bool.TryParse(wert, out var result))
            return result;
        return null;
    }

    /// <summary>Speichert oder überschreibt eine Einstellung. Übergibt man <c>null</c>, wird der Wert auf null gesetzt (nicht gelöscht).</summary>
    public async Task SetSettingAsync(string schluessel, string? wert, CancellationToken ct = default)
    {
        var einstellung = await _db.AppEinstellungen
            .FirstOrDefaultAsync(s => s.Schluessel == schluessel, ct);

        if (einstellung is null)
        {
            einstellung = new AppEinstellung
            {
                Id = Guid.NewGuid(),
                Schluessel = schluessel
            };
            _db.AppEinstellungen.Add(einstellung);
        }

        einstellung.Wert = wert;
        einstellung.AktualisiertAm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Einstellung '{Schluessel}' gespeichert.", schluessel);
    }

    /// <summary>Speichert eine Ganzzahl-Einstellung.</summary>
    public Task SetIntSettingAsync(string schluessel, int wert, CancellationToken ct = default)
        => SetSettingAsync(schluessel, wert.ToString(), ct);

    /// <summary>Speichert eine Boolean-Einstellung.</summary>
    public Task SetBoolSettingAsync(string schluessel, bool wert, CancellationToken ct = default)
        => SetSettingAsync(schluessel, wert.ToString(), ct);

    /// <summary>Liest alle Fenstergeometrie-Einstellungen in einer einzigen Datenbankabfrage.</summary>
    public async Task<WindowGeometrySettings> GetWindowGeometryAsync(CancellationToken ct = default)
    {
        var keys = new[]
        {
            WindowPositionXKey,
            WindowPositionYKey,
            WindowWidthKey,
            WindowHeightKey
        };

        var werte = await _db.AppEinstellungen
            .AsNoTracking()
            .Where(s => keys.Contains(s.Schluessel))
            .ToDictionaryAsync(s => s.Schluessel, s => s.Wert, ct);

        static int? ParseInt(Dictionary<string, string?> d, string key)
            => d.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;

        return new WindowGeometrySettings(
            ParseInt(werte, WindowPositionXKey),
            ParseInt(werte, WindowPositionYKey),
            ParseInt(werte, WindowWidthKey),
            ParseInt(werte, WindowHeightKey));
    }

    /// <summary>Speichert alle Fenstergeometrie-Einstellungen auf einmal.</summary>
    public async Task SetWindowGeometryAsync(WindowGeometrySettings geometry, CancellationToken ct = default)
    {
        await SetIntSettingAsync(WindowPositionXKey, geometry.X ?? 0, ct);
        await SetIntSettingAsync(WindowPositionYKey, geometry.Y ?? 0, ct);
        await SetIntSettingAsync(WindowWidthKey, geometry.Width ?? 1280, ct);
        await SetIntSettingAsync(WindowHeightKey, geometry.Height ?? 800, ct);
    }
}

/// <summary>Fenstergeometrie-Einstellungen (Position und Größe des Hauptfensters).</summary>
public sealed record WindowGeometrySettings(int? X, int? Y, int? Width, int? Height);
