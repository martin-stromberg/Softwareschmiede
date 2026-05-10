using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet das globale Arbeitsverzeichnis für lokale Repository-Klone.</summary>
public sealed class ArbeitsverzeichnisSettingsService
{
    /// <summary>Schlüssel der App-Einstellung für das Repository-Arbeitsverzeichnis.</summary>
    public const string RepositoriesWorkdirKey = "repositories.workdir";

    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<ArbeitsverzeichnisSettingsService> _logger;

    /// <inheritdoc cref="ArbeitsverzeichnisSettingsService"/>
    public ArbeitsverzeichnisSettingsService(
        SoftwareschmiededDbContext db,
        ILogger<ArbeitsverzeichnisSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liest den aktuell gespeicherten Arbeitsverzeichnis-Wert.</summary>
    public async Task<string?> GetArbeitsverzeichnisAsync(CancellationToken ct = default)
    {
        var setting = await _db.AppEinstellungen
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Schluessel == RepositoriesWorkdirKey, ct);

        return setting?.Wert;
    }

    /// <summary>Speichert das Arbeitsverzeichnis (null/leer = Default verwenden).</summary>
    public async Task SaveArbeitsverzeichnisAsync(string? arbeitsverzeichnis, CancellationToken ct = default)
    {
        var trimmed = arbeitsverzeichnis?.Trim();
        var normalized = string.IsNullOrWhiteSpace(trimmed)
            ? null
            : NormalizePath(trimmed);

        if (normalized is not null)
        {
            // Validiere den Pfad (ohne Nebeneffekte)
            ValidatePathForConfiguration(trimmed!);
            
            // Erstelle das Verzeichnis nur bei explizitem Speichern
            await EnsureDirectoryExistsAsync(normalized);
        }

        var setting = await _db.AppEinstellungen
            .FirstOrDefaultAsync(s => s.Schluessel == RepositoriesWorkdirKey, ct);

        if (setting is null)
        {
            setting = new AppEinstellung
            {
                Id = Guid.NewGuid(),
                Schluessel = RepositoriesWorkdirKey
            };
            _db.AppEinstellungen.Add(setting);
        }

        setting.Wert = normalized;
        setting.AktualisiertAm = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Arbeitsverzeichnis-Einstellung gespeichert: {Path}", normalized);
    }

    /// <summary>
    /// Validiert einen konfigurierten Pfad, ohne Verzeichnisse zu erstellen (reine Validierung ohne Nebeneffekte).
    /// </summary>
    /// <remarks>
    /// Diese Methode führt nur syntaktische und strukturelle Validierungen durch.
    /// Die Erstellung des Verzeichnisses erfolgt erst in <see cref="EnsureDirectoryExistsAsync(string, CancellationToken)"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">Wenn der Pfad ungültig ist.</exception>
    public static void ValidatePathForConfiguration(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException("Pfad muss absolut sein.");
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException("Pfad enthält ungültige Zeichen.");
        }

        try
        {
            Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Pfad ist ungültig.", ex);
        }
    }

    /// <summary>
    /// Erstellt das konfigurierte Arbeitsverzeichnis, wenn es nicht existiert.
    /// Diese Methode wird nur explizit beim Speichern aufgerufen (keine Nebeneffekte während Validierung).
    /// </summary>
    /// <exception cref="ArgumentException">Wenn das Verzeichnis nicht erstellt werden kann.</exception>
    private static async Task EnsureDirectoryExistsAsync(string fullPath, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Verzeichnis kann nicht erstellt oder erreicht werden.", ex);
            }
        }, ct);
    }

    /// <summary>
    /// Obsolete: Verwende stattdessen <see cref="ValidatePathForConfiguration(string)"/> für Validierung 
    /// und <see cref="SaveArbeitsverzeichnisAsync(string?, CancellationToken)"/> zum Speichern mit Verzeichniserstellung.
    /// </summary>
    [Obsolete("Verwende ValidatePathForConfiguration() stattdessen. Diese Methode wird in einer zukünftigen Version entfernt.")]
    public static void ValidatePathForSave(string path)
    {
        ValidatePathForConfiguration(path);
        
        // Versuche das Verzeichnis zu erstellen (Fallback für Legacy-Code)
        try
        {
            Directory.CreateDirectory(Path.GetFullPath(path));
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Verzeichnis kann nicht erstellt oder erreicht werden.", ex);
        }
    }

    /// <summary>Normalisiert einen konfigurierten Pfad in seine kanonische Form.</summary>
    public static string NormalizePath(string path) => Path.GetFullPath(path.Trim());
}
