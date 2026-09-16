using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services.Updates;
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

    /// <summary>Schlüssel für das Standard-SCM-Plugin.</summary>
    public const string DefaultScmPluginKey = "scm.plugin.default";

    /// <summary>Schlüssel für das Log-Level.</summary>
    public const string LogLevelKey = "logging.level";

    /// <summary>Schlüssel für die Prioritäts-Reihenfolge der IDE-Plugins (kommagetrennte Liste von Plugin-Prefixen).</summary>
    public const string IdePluginOrderKey = "plugins.ide.order";

    /// <summary>Schlüssel für das Feature-Flag "Autonome Aufgaben aktiviert".</summary>
    public const string AutonomAufgabenEnabledKey = "autonomeaufgaben.enabled";

    /// <summary>Schlüssel für den Update-Modus (<see cref="UpdateMode"/>-Wert als Zahl gespeichert).</summary>
    public const string UpdateModeKey = "updates.mode";

    /// <summary>Schlüssel für die Prerelease-Auswahl bei Updates.</summary>
    public const string IncludePrereleasesKey = "updates.includePrereleases";

    /// <summary>Fester EF-Abfragetag der gemeinsamen Update-Einstellungs-Leseabfrage für gezielte Testinterception.</summary>
    public const string UpdateSettingsReadTag = "UpdateSettings.Read";

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

    /// <summary>
    /// Ermittelt, ob das Feature "Autonome Aufgaben" aktuell aktiv ist: Der über <see cref="SetBoolSettingAsync"/>
    /// unter <see cref="AutonomAufgabenEnabledKey"/> persistierte Laufzeit-Schalter (GUI-Einstellung) hat Vorrang,
    /// sofern der Anwender ihn bereits explizit gesetzt hat; existiert kein DB-Eintrag (Rückgabe <c>null</c> von
    /// <see cref="GetBoolSettingAsync"/>), wird <paramref name="deploymentDefault"/> (der aus appsettings.json/
    /// Umgebungsvariablen gebundene <c>AutonomAufgabenOptions.Enabled</c>-Deployment-Zeit-Default) als Fallback
    /// verwendet. Bündelt dieses Dual-Layer-Fallback-Muster zentral für alle Guard-Klauseln und UI-Sichtbarkeits-
    /// Checks rund um Autonome Aufgaben (Issue 205), statt es an jeder Aufrufstelle zu duplizieren.
    /// </summary>
    /// <param name="deploymentDefault">Der zu verwendende Fallback-Wert, wenn kein DB-Eintrag existiert (typischerweise <c>IOptions&lt;AutonomAufgabenOptions&gt;.Value.Enabled</c>).</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn Autonome Aufgaben aktiv sind (DB-Wert, sonst <paramref name="deploymentDefault"/>).</returns>
    public async Task<bool> GetAutonomAufgabenEnabledAsync(bool deploymentDefault, CancellationToken ct = default)
    {
        var dbWert = await GetBoolSettingAsync(AutonomAufgabenEnabledKey, ct);
        return dbWert ?? deploymentDefault;
    }

    /// <summary>Liest die Werte mehrerer Einstellungen anhand ihrer Schlüssel in einer einzigen Datenbankabfrage.</summary>
    /// <param name="schluessel">Die zu lesenden Schlüssel.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Eine Zuordnung von Schlüssel zu Wert; Schlüssel ohne gespeicherten Eintrag fehlen im Ergebnis.</returns>
    public async Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(IReadOnlyCollection<string> schluessel, CancellationToken ct = default)
    {
        if (schluessel.Count == 0)
            return new Dictionary<string, string?>();

        return await _db.AppEinstellungen
            .AsNoTracking()
            .Where(s => schluessel.Contains(s.Schluessel))
            .ToDictionaryAsync(s => s.Schluessel, s => s.Wert, ct);
    }

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

    /// <summary>Speichert alle Fenstergeometrie-Einstellungen in einer einzigen Transaktion.</summary>
    public async Task SetWindowGeometryAsync(WindowGeometrySettings geometry, CancellationToken ct = default)
    {
        var updates = new[]
        {
            (WindowPositionXKey, (geometry.X ?? 0).ToString()),
            (WindowPositionYKey, (geometry.Y ?? 0).ToString()),
            (WindowWidthKey,     (geometry.Width ?? 1280).ToString()),
            (WindowHeightKey,    (geometry.Height ?? 800).ToString()),
        };

        var keys = updates.Select(u => u.Item1).ToArray();

        var bestehende = await _db.AppEinstellungen
            .Where(s => keys.Contains(s.Schluessel))
            .ToDictionaryAsync(s => s.Schluessel, ct);

        foreach (var (schluessel, wert) in updates)
        {
            if (bestehende.TryGetValue(schluessel, out var einstellung))
            {
                einstellung.Wert = wert;
                einstellung.AktualisiertAm = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.AppEinstellungen.Add(new AppEinstellung
                {
                    Id = Guid.NewGuid(),
                    Schluessel = schluessel,
                    Wert = wert,
                    AktualisiertAm = DateTimeOffset.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Fenstergeometrie gespeichert.");
    }

    /// <summary>
    /// Liest Update-Modus und Prerelease-Auswahl gemeinsam in einer einzigen Datenbankabfrage
    /// (Abfragetag <see cref="UpdateSettingsReadTag"/>). Fehlende oder ungültige gespeicherte Werte
    /// fallen auf die Defaults <see cref="UpdateMode.NurPruefen"/> bzw. <see langword="false"/> zurück;
    /// Datenbank-Lesefehler werden nicht abgefangen und propagieren an den Aufrufer.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die gelesenen Update-Einstellungen.</returns>
    public async Task<UpdateSettings> GetUpdateSettingsAsync(CancellationToken ct = default)
    {
        var keys = new[] { UpdateModeKey, IncludePrereleasesKey };

        var werte = await _db.AppEinstellungen
            .AsNoTracking()
            .TagWith(UpdateSettingsReadTag)
            .Where(s => keys.Contains(s.Schluessel))
            .ToDictionaryAsync(s => s.Schluessel, s => s.Wert, ct);

        var modus = UpdateMode.NurPruefen;
        if (werte.TryGetValue(UpdateModeKey, out var modusWert)
            && int.TryParse(modusWert, out var modusZahl)
            && Enum.IsDefined((UpdateMode)modusZahl))
        {
            modus = (UpdateMode)modusZahl;
        }

        var includePrereleases = werte.TryGetValue(IncludePrereleasesKey, out var prereleaseWert)
            && bool.TryParse(prereleaseWert, out var prereleaseFlag)
            && prereleaseFlag;

        return new UpdateSettings(modus, includePrereleases);
    }

    /// <summary>
    /// Validiert den Update-Modus und schreibt Modus und Prerelease-Auswahl gemeinsam
    /// in einem einzigen Speichervorgang.
    /// </summary>
    /// <param name="settings">Die zu speichernden Update-Einstellungen.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settings"/> enthält einen nicht definierten <see cref="UpdateMode"/>-Wert.</exception>
    public async Task SetUpdateSettingsAsync(UpdateSettings settings, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(settings.Modus))
            throw new ArgumentOutOfRangeException(nameof(settings), settings.Modus, "Unbekannter Update-Modus.");

        var updates = new[]
        {
            (UpdateModeKey,         ((int)settings.Modus).ToString()),
            (IncludePrereleasesKey, settings.IncludePrereleases.ToString()),
        };

        var keys = updates.Select(u => u.Item1).ToArray();

        var bestehende = await _db.AppEinstellungen
            .Where(s => keys.Contains(s.Schluessel))
            .ToDictionaryAsync(s => s.Schluessel, ct);

        foreach (var (schluessel, wert) in updates)
        {
            if (bestehende.TryGetValue(schluessel, out var einstellung))
            {
                einstellung.Wert = wert;
                einstellung.AktualisiertAm = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.AppEinstellungen.Add(new AppEinstellung
                {
                    Id = Guid.NewGuid(),
                    Schluessel = schluessel,
                    Wert = wert,
                    AktualisiertAm = DateTimeOffset.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Update-Einstellungen gespeichert.");
    }
}

/// <summary>Fenstergeometrie-Einstellungen (Position und Größe des Hauptfensters).</summary>
public sealed record WindowGeometrySettings(int? X, int? Y, int? Width, int? Height);
