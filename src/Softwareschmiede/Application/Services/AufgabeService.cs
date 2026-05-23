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
            .Include(a => a.IssueReferenz)
            .Include(a => a.GitRepository)
                .ThenInclude(r => r.StartKonfiguration)
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

    /// <summary>Erstellt eine neue Aufgabe.</summary>
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
            Status = AufgabeStatus.Offen,
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
            Status = AufgabeStatus.Offen,
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

    /// <summary>Aktualisiert Titel, Beschreibung und Agenteninformationen einer Aufgabe.</summary>
    public async Task UpdateAsync(
        Guid id,
        string titel,
        string? anforderungsBeschreibung,
        string? agentenpaketName,
        string? agentenName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisieren.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Titel = titel;
        aufgabe.AnforderungsBeschreibung = anforderungsBeschreibung;
        aufgabe.AgentenpaketName = agentenpaketName;
        aufgabe.AgentenName = agentenName;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} aktualisiert.", id);
    }

    /// <summary>Löscht eine Aufgabe. Nur für archivierte, fehlgeschlagene oder abgeschlossene Aufgaben.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} löschen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        _db.Aufgaben.Remove(aufgabe);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} gelöscht.", id);
    }

    /// <summary>Verwirft eine offene Aufgabe durch Archivieren oder Löschen.</summary>
    public async Task VerwerfenAsync(Guid id, VerwerfenAktion aktion, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} verwerfen (Aktion: {Aktion}).", id, aktion);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.Status != AufgabeStatus.Offen)
        {
            throw new InvalidOperationException("Nur offene Aufgaben können verworfen werden.");
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

    /// <summary>Archiviert eine Aufgabe. Nur für abgeschlossene oder fehlgeschlagene Aufgaben möglich.</summary>
    public async Task ArchivierenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} archivieren.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        if (aufgabe.Status is not (AufgabeStatus.Abgeschlossen or AufgabeStatus.Fehlgeschlagen))
        {
            throw new InvalidOperationException(
                "Nur abgeschlossene oder fehlgeschlagene Aufgaben können archiviert werden.");
        }

        aufgabe.Status = AufgabeStatus.Archiviert;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId} archiviert.", id);
    }

    /// <summary>Startet eine Aufgabe: Status → InBearbeitung, Branch und Klonpfad setzen.</summary>
    public async Task StartenAsync(Guid id, string branchName, string lokalerKlonPfad, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} starten (Branch: {BranchName}).", id, branchName);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.InBearbeitung;
        aufgabe.BranchName = branchName;
        aufgabe.LokalerKlonPfad = lokalerKlonPfad;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} gestartet.", id);
    }

    /// <summary>Setzt den Status auf KiAktiv.</summary>
    public async Task KiAktiviertAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: KI aktiviert.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.KiAktiv;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Setzt den Status zurück auf InBearbeitung nach KI-Abschluss.</summary>
    public async Task KiAbgeschlossenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: KI abgeschlossen, zurück auf InBearbeitung.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.InBearbeitung;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Schließt eine Aufgabe ab: Status → Abgeschlossen, AbschlussDatum setzen, Branch und Klonpfad leeren.</summary>
    public async Task AbschliessenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abschließen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.Abgeschlossen;
        aufgabe.AbschlussDatum = DateTimeOffset.UtcNow;
        aufgabe.BranchName = null;
        aufgabe.LokalerKlonPfad = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} abgeschlossen.", id);
    }

    /// <summary>Bricht eine Aufgabe ab: Status → Offen, Branch und Klonpfad leeren.</summary>
    public async Task AbbrechenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId} abbrechen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.Offen;
        aufgabe.BranchName = null;
        aufgabe.LokalerKlonPfad = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Aufgabe {AufgabeId} abgebrochen, Status zurück auf Offen.", id);
    }

    /// <summary>Setzt den Status auf Fehlgeschlagen.</summary>
    public async Task FehlgeschlagenAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Fehlgeschlagen.", id);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = AufgabeStatus.Fehlgeschlagen;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Setzt den Status einer Aufgabe generisch.</summary>
    public async Task StatusSetzenAsync(Guid id, AufgabeStatus status, CancellationToken ct = default)
    {
        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} setzen.", id, status);

        var aufgabe = await _db.Aufgaben.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Aufgabe {id} nicht gefunden.");

        aufgabe.Status = status;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Aufgabe {AufgabeId}: Status auf {Status} gesetzt.", id, status);
    }
}
