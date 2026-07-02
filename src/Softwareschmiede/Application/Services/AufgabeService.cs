using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Service für Aufgabenverwaltung (CRUD + Lebenszyklus).</summary>
public sealed class AufgabeService
{
    // AktivOderWartendStatus ist die einzige Quelle der Wahrheit für die Regel "Aufgabe ist aktiv oder
    // wartend" (siehe AufgabeStatusExtensions.IstAktivOderWartend). Für EF-Core-Queries wird die Array-
    // Variante referenziert (Contains wird nach SQL IN übersetzt); außerhalb von Queries wird direkt
    // AufgabeStatus.IstAktivOderWartend() verwendet (z. B. in DeleteAsync).
    private static readonly Expression<Func<Aufgabe, bool>> IstAktivOderWartendPredicate =
        a => AufgabeStatusExtensions.AktivOderWartendStatus.Contains(a.Status);

    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<AufgabeService> _logger;

    /// <inheritdoc cref="AufgabeService"/>
    public AufgabeService(SoftwareschmiededDbContext db, ILogger<AufgabeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück.</summary>
    public async Task<IReadOnlyList<Aufgabe>> GetByProjektAsync(Guid projektId, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgaben für Projekt {ProjektId} abrufen.", projektId);
        return await _db.Aufgaben
            .AsNoTracking()
            .Include(a => a.IssueReferenz)
            .Where(a => a.ProjektId == projektId && a.Status != AufgabeStatus.Archiviert)
            .OrderByDescending(a => a.ErstellungsDatum)
            .ToListAsync(ct);
    }

    /// <summary>Gibt alle archivierten Aufgaben eines Projekts zurück.</summary>
    public async Task<IReadOnlyList<Aufgabe>> GetArchiviertByProjektAsync(Guid projektId, CancellationToken ct = default)
    {
        _logger.LogInformation("Archivierte Aufgaben für Projekt {ProjektId} abrufen.", projektId);
        return await _db.Aufgaben
            .AsNoTracking()
            .Where(a => a.ProjektId == projektId && a.Status == AufgabeStatus.Archiviert)
            .OrderByDescending(a => a.ErstellungsDatum)
            .ToListAsync(ct);
    }

    /// <summary>Gibt die Anzahl aktiver (Gestartet) und wartender (Wartend) Aufgaben als Tupel zurück — eine einzige Abfrage.</summary>
    public async Task<(int Aktiv, int Wartend)> GetAktiveUndWartendeCountAsync(CancellationToken ct = default)
    {
        var counts = await _db.Aufgaben
            .AsNoTracking()
            .Where(IstAktivOderWartendPredicate)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var aktiv = counts.FirstOrDefault(c => c.Status == AufgabeStatus.Gestartet)?.Count ?? 0;
        var wartend = counts.FirstOrDefault(c => c.Status == AufgabeStatus.Wartend)?.Count ?? 0;
        return (aktiv, wartend);
    }

    /// <summary>Gibt eine Aufgabe anhand ihrer ID zurück.</summary>
    public async Task<Aufgabe?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abrufen.", id);
        return await _db.Aufgaben
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <summary>Gibt eine Aufgabe mit IssueReferenz und Protokolleinträgen zurück.</summary>
    public async Task<Aufgabe?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} mit Details abrufen.", id);
        return await _db.Aufgaben
            .AsNoTracking()
            .Include(a => a.Projekt)
            .Include(a => a.IssueReferenz)
            .Include(a => a.GitRepository)
                .ThenInclude(r => r!.StartKonfiguration)
            .Include(a => a.Protokolleintraege)
                .ThenInclude(p => p.TestErgebnisse)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <summary>Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Aufgabe zurück.</summary>
    public async Task<Guid?> GetLatestDiffResultIdAsync(Guid aufgabeId, CancellationToken ct = default)
    {
        _logger.LogInformation("Letzte DiffResult-ID für Aufgabe {AufgabeId} abrufen.", aufgabeId);

        return await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId)
            .OrderByDescending(dr => dr.GeneratedAt)
            .Select(dr => (Guid?)dr.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Gibt die ID des zuletzt generierten Diff-Ergebnisses einer Datei innerhalb einer Aufgabe zurück.</summary>
    public async Task<Guid?> GetLatestDiffResultIdForFileAsync(Guid aufgabeId, string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedLookupPath = NormalizeRelativePathForLookup(relativePath);

        _logger.LogInformation(
            "Dateispezifische DiffResult-ID für Aufgabe {AufgabeId} und Datei {RelativePath} abrufen.",
            aufgabeId,
            normalizedLookupPath);

        return await _db.DiffResults
            .AsNoTracking()
            .Where(dr => dr.AufgabeId == aufgabeId
                && dr.FilePath != null
                && dr.FilePath.Replace("\\", "/").ToLower() == normalizedLookupPath)
            .OrderByDescending(dr => dr.GeneratedAt)
            .Select(dr => (Guid?)dr.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static string NormalizeRelativePathForLookup(string relativePath)
    {
        var normalized = relativePath.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/').ToLowerInvariant();
    }

    /// <summary>Erstellt eine neue Aufgabe mit Status <see cref="AufgabeStatus.Neu"/>.</summary>
    public async Task<Aufgabe> CreateAsync(
        Guid projektId,
        string titel,
        string? anforderungsBeschreibung,
        Guid? gitRepositoryId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe '{Titel}' für Projekt {ProjektId} erstellen.", titel, projektId);

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            GitRepositoryId = gitRepositoryId,
            Titel = titel,
            AnforderungsBeschreibung = anforderungsBeschreibung,
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe '{Titel}' mit ID {AufgabeId} erstellt.", titel, aufgabe.Id);
        return aufgabe;
    }

    /// <summary>Erstellt eine neue Aufgabe aus einem Issue.</summary>
    public async Task<Aufgabe> CreateFromIssueAsync(
        Guid projektId,
        Issue issue,
        Guid? gitRepositoryId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe aus Issue #{IssueNummer} für Projekt {ProjektId} erstellen.", issue.Nummer, projektId);

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            GitRepositoryId = gitRepositoryId,
            Titel = issue.Titel,
            AnforderungsBeschreibung = issue.Body,
            Status = AufgabeStatus.Neu,
            ErstellungsDatum = DateTimeOffset.UtcNow
        };

        var issueReferenz = new IssueReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            IssueNummer = issue.Nummer,
            Titel = issue.Titel,
            Body = issue.Body,
            LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels),
            Milestone = issue.Milestone,
            IssueUrl = issue.IssueUrl
        };

        aufgabe.IssueReferenz = issueReferenz;

        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe aus Issue #{IssueNummer} mit ID {AufgabeId} erstellt.", issue.Nummer, aufgabe.Id);
        return aufgabe;
    }

    /// <summary>Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix einer Aufgabe.</summary>
    public async Task UpdateAsync(
        Guid id,
        string titel,
        string? anforderungsBeschreibung,
        string? kiPluginPrefix = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisieren.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Titel = titel;
        aufgabe.AnforderungsBeschreibung = anforderungsBeschreibung;
        aufgabe.KiPluginPrefix = kiPluginPrefix;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisiert.", id);
    }

    /// <summary>Setzt die IssueReferenz einer Aufgabe. Übergabe von null entfernt die bestehende Referenz.</summary>
    public async Task UpdateIssueReferenzAsync(Guid id, Issue? issue, CancellationToken ct = default)
    {
        _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} aktualisieren.", id);

        var aufgabe = await _db.Aufgaben
            .Include(a => a.IssueReferenz)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (issue == null)
        {
            if (aufgabe.IssueReferenz != null)
                _db.Remove(aufgabe.IssueReferenz);
            aufgabe.IssueReferenz = null;
        }
        else
        {
            if (aufgabe.IssueReferenz != null)
            {
                aufgabe.IssueReferenz.IssueNummer = issue.Nummer;
                aufgabe.IssueReferenz.Titel = issue.Titel;
                aufgabe.IssueReferenz.Body = issue.Body;
                aufgabe.IssueReferenz.LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels);
                aufgabe.IssueReferenz.Milestone = issue.Milestone;
                aufgabe.IssueReferenz.IssueUrl = issue.IssueUrl;
            }
            else
            {
                var neueReferenz = new IssueReferenz
                {
                    Id = Guid.NewGuid(),
                    AufgabeId = aufgabe.Id,
                    IssueNummer = issue.Nummer,
                    Titel = issue.Titel,
                    Body = issue.Body,
                    LabelsJson = System.Text.Json.JsonSerializer.Serialize(issue.Labels),
                    Milestone = issue.Milestone,
                    IssueUrl = issue.IssueUrl
                };
                aufgabe.IssueReferenz = neueReferenz;
                _db.IssueReferenzen.Add(neueReferenz);
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        _logger.LogInformation("IssueReferenz für Aufgabe {AufgabeId} aktualisiert.", id);
    }

    /// <summary>Löscht eine Aufgabe.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} löschen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.Status.IstAktivOderWartend())
        {
            throw new InvalidOperationException(
                $"Aufgabe {id} kann nicht gelöscht werden, da sie aktiv ist (Status: {aufgabe.Status}). Bitte zuerst abbrechen oder abschließen.");
        }

        _db.Aufgaben.Remove(aufgabe);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} gelöscht.", id);
    }

    /// <summary>Verwirft eine Aufgabe im Status Neu durch Archivieren oder Löschen.</summary>
    public async Task VerwerfenAsync(Guid id, VerwerfenAktion aktion, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} verwerfen (Aktion: {Aktion}).", id, aktion);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.Status != AufgabeStatus.Neu)
        {
            throw new InvalidOperationException("Nur neue Aufgaben können verworfen werden.");
        }

        if (aktion == VerwerfenAktion.Archivieren)
        {
            aufgabe.Status = AufgabeStatus.Archiviert;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Aufgabe {AufgabeId} verworfen und archiviert.", id);
            return;
        }

        await DeleteAsync(id, ct);
        _logger.LogInformation("Aufgabe {AufgabeId} verworfen und dauerhaft gelöscht.", id);
    }

    /// <summary>Archiviert eine Aufgabe. Nur für Aufgaben im Status Beendet möglich.</summary>
    public async Task ArchivierenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} archivieren.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.Status is not AufgabeStatus.Beendet)
        {
            throw new InvalidOperationException("Nur beendete Aufgaben können archiviert werden.");
        }

        aufgabe.Status = AufgabeStatus.Archiviert;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} archiviert.", id);
    }

    /// <summary>Startet eine Aufgabe: Status → Gestartet, Branch und Arbeitsverzeichnis setzen.</summary>
    public async Task StartenAsync(Guid id, string branchName, string lokalerKlonPfad, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} starten (Branch: {BranchName}).", id, branchName);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.Gestartet;
        aufgabe.BranchName = branchName;
        aufgabe.LokalerKlonPfad = lokalerKlonPfad;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} gestartet (Status: Gestartet).", id);
    }

    /// <summary>Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt für die Aufgabe.</summary>
    public async Task SavePromptVorschlagAsync(Guid id, string? prompt, DateTimeOffset? ausfuehrenAbUtc, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Vorschlagsprompt speichern.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        var normalizedPrompt = string.IsNullOrWhiteSpace(prompt)
            ? null
            : prompt.Trim();

        aufgabe.VorschlagPrompt = normalizedPrompt;
        aufgabe.VorschlagAusfuehrenAbUtc = normalizedPrompt is null
            ? null
            : ausfuehrenAbUtc;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Entfernt den gespeicherten Vorschlagsprompt und den geplanten Ausführungszeitpunkt.</summary>
    public async Task ClearPromptVorschlagAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Vorschlagsprompt löschen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.VorschlagPrompt = null;
        aufgabe.VorschlagAusfuehrenAbUtc = null;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Schließt eine Aufgabe ab: Status → Beendet, AbschlussDatum setzen, Branch- und Klonpfad-Felder leeren.</summary>
    public async Task AbschliessenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.Beendet;
        aufgabe.AbschlussDatum = DateTimeOffset.UtcNow;
        aufgabe.BranchName = null;
        aufgabe.LokalerKlonPfad = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} abgeschlossen (Status: Beendet).", id);
    }

    /// <summary>Setzt den Status einer Aufgabe mit Validierung der erlaubten Übergänge.</summary>
    public async Task SetStatusAsync(Guid id, AufgabeStatus newStatus, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} setzen.", id, newStatus);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        ValidateStatusTransition(aufgabe.Status, newStatus);

        aufgabe.Status = newStatus;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} gesetzt.", id, newStatus);
    }

    /// <summary>Setzt den Status einer Aufgabe generisch (ohne Transitions-Validierung).</summary>
    public async Task StatusSetzenAsync(Guid id, AufgabeStatus status, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} setzen.", id, status);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = status;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} gesetzt.", id, status);
    }

    /// <summary>Aktualisiert LastHeartbeatUtc der Aufgabe.</summary>
    public async Task UpdateHeartbeatAsync(Guid id, CancellationToken ct = default)
    {
        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gibt die Minuten seit dem letzten Heartbeat zurück (null wenn kein Heartbeat gesetzt).</summary>
    public async Task<int?> GetHeartbeatAgeMinutesAsync(Guid id, CancellationToken ct = default)
    {
        var aufgabe = await _db.Aufgaben
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.LastHeartbeatUtc is null)
        {
            return null;
        }

        var ageMinutes = (int)(DateTimeOffset.UtcNow - aufgabe.LastHeartbeatUtc.Value).TotalMinutes;
        return ageMinutes;
    }

    /// <summary>Gibt alle aktiven Aufgaben (Status Gestartet oder Wartend) zurück, sortiert nach letzter Aktivität.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    /// <returns>Die aktiven Aufgaben, absteigend nach letzter Aktivität sortiert (maximal 20).</returns>
    public async Task<List<Aufgabe>> GetAktiveAufgabenAsync(CancellationToken ct = default)
    {
        return await _db.Aufgaben
            .AsNoTracking()
            .Include(a => a.Projekt)
            .Where(IstAktivOderWartendPredicate)
            .OrderByDescending(a => a.LastHeartbeatUtc ?? a.ErstellungsDatum)
            .Take(20)
            .ToListAsync(ct);
    }

    private static void ValidateStatusTransition(AufgabeStatus current, AufgabeStatus next)
    {
        if (next == AufgabeStatus.Archiviert)
        {
            return;
        }

        var allowed = current switch
        {
            AufgabeStatus.Neu => new[] { AufgabeStatus.Gestartet },
            AufgabeStatus.Gestartet => new[] { AufgabeStatus.Wartend, AufgabeStatus.Beendet },
            AufgabeStatus.Wartend => new[] { AufgabeStatus.Gestartet, AufgabeStatus.Beendet },
            AufgabeStatus.Beendet => Array.Empty<AufgabeStatus>(),
            AufgabeStatus.Archiviert => Array.Empty<AufgabeStatus>(),
            _ => Array.Empty<AufgabeStatus>()
        };

        if (!allowed.Contains(next))
        {
            throw new InvalidStatusTransitionException(current, next);
        }
    }
}
