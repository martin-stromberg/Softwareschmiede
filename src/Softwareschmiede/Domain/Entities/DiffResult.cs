using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>
/// Diff-Ergebnis: Übergeordneter Container für einen Diff-Vergleich zwischen zwei Dateiversionen.
/// </summary>
public sealed class DiffResult
{
    /// <summary>Eindeutige ID des Diff-Ergebnisses.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe (NOT NULL).</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Optionale ID des Git-Repositories (für Kontext).</summary>
    public Guid? GitRepositoryId { get; set; }

    /// <summary>Optionaler Verweis auf Protokolleintrag (wenn als Ereignis protokolliert).</summary>
    public Guid? ProtokollEintragId { get; set; }

    /// <summary>Relative Dateipfad im Repository (z.B. "src/App.razor").</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Quellversion (Branch/Commit-Hash/Tag).</summary>
    public string SourceVersion { get; set; } = string.Empty;

    /// <summary>Zielversion (Branch/Commit-Hash/Tag).</summary>
    public string TargetVersion { get; set; } = string.Empty;

    /// <summary>Diff-Renderingtyp (Full, SideBySide, Split).</summary>
    public DiffType DiffType { get; set; } = DiffType.Full;

    /// <summary>Gesamtzahl der Zeilen im Diff.</summary>
    public int LineCount { get; set; }

    /// <summary>Anzahl hinzugefügter Zeilen.</summary>
    public int AddedLines { get; set; }

    /// <summary>Anzahl gelöschter Zeilen.</summary>
    public int RemovedLines { get; set; }

    /// <summary>Anzahl modifizierter Zeilen.</summary>
    public int ModifiedLines { get; set; }

    /// <summary>Status des Diff-Ergebnisses (Pending, Generated, Cached, Error).</summary>
    public DiffResultStatus Status { get; set; } = DiffResultStatus.Pending;

    /// <summary>Zeitstempel der Diff-Generierung.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Name des Services/Agenten, der den Diff generiert hat.</summary>
    public string GeneratedBy { get; set; } = string.Empty;

    /// <summary>Optionaler Vollinhalt der Quelldatei (nur für kleine Dateien).</summary>
    public string? SourceContent { get; set; }

    /// <summary>Optionaler Vollinhalt der Zieldatei (nur für kleine Dateien).</summary>
    public string? TargetContent { get; set; }

    /// <summary>Ablaufzeit für Caching (TTL-Invalidierung); null = keine Expiration.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;

    /// <summary>Optionale Navigationseigenschaft zum Git-Repository.</summary>
    public GitRepository? GitRepository { get; set; }

    /// <summary>Optionale Navigationseigenschaft zum Protokolleintrag.</summary>
    public Protokolleintrag? ProtokollEintrag { get; set; }

    /// <summary>Diff-Blöcke (kaskadierendes Löschen).</summary>
    public List<DiffBlock> DiffBlocks { get; set; } = [];

    /// <summary>Zugehöriger Diff-Cache-Eintrag (1:1-Beziehung).</summary>
    public DiffCache? DiffCache { get; set; }
}
