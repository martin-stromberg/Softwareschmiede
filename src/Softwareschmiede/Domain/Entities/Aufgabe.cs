using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Aufgabe innerhalb eines Projekts, die durch einen KI-Agenten bearbeitet werden kann.</summary>
public sealed class Aufgabe
{
    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Projekts.</summary>
    public Guid ProjektId { get; set; }

    /// <summary>Optionale ID des verknüpften Git-Repositories.</summary>
    public Guid? GitRepositoryId { get; set; }

    /// <summary>Titel der Aufgabe.</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Anforderungsbeschreibung für den KI-Agenten.</summary>
    public string? AnforderungsBeschreibung { get; set; }

    /// <summary>Aktueller Status der Aufgabe.</summary>
    public AufgabeStatus Status { get; set; }

    /// <summary>Name des Git-Branches für diese Aufgabe.</summary>
    public string? BranchName { get; set; }

    /// <summary>Lokaler Pfad des geklonten Repositories.</summary>
    public string? LokalerKlonPfad { get; set; }

    /// <summary>Name des verwendeten Agentenpakets.</summary>
    public string? AgentenpaketName { get; set; }

    /// <summary>Name des verwendeten Agenten.</summary>
    public string? AgentenName { get; set; }

    /// <summary>Prefix des für diese Aufgabe verwendeten KI-Plugins.</summary>
    public string? KiPluginPrefix { get; set; }

    /// <summary>Erstellungsdatum der Aufgabe.</summary>
    public DateTimeOffset ErstellungsDatum { get; set; }

    /// <summary>Abschlussdatum der Aufgabe (null wenn noch nicht abgeschlossen).</summary>
    public DateTimeOffset? AbschlussDatum { get; set; }

    /// <summary>Optional: Aktive Lauf-ID einer KI-Ausführung.</summary>
    public string? AktiveRunId { get; set; }

    /// <summary>Optional: Zeitstempel des letzten Heartbeats einer Ausführung.</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; set; }

    /// <summary>Optional: Zeitstempel des letzten echten CLI-Prozessstarts.</summary>
    public DateTimeOffset? LetzterCliStartUtc { get; set; }

    /// <summary>
    /// Optional: Laufzeit-Substatus der aktiven CLI-Ausführung (nur relevant, solange
    /// <see cref="AktiveRunId"/> gesetzt ist). Wird von <c>CliProcessManager</c> anhand des
    /// <c>PseudoConsoleSession.RuntimeStatusChanged</c>-Ereignisses aktualisiert, damit die Seitenleisten-/
    /// Dashboard-Kachel (<c>KiAusfuehrungsStatusConverter</c>) zwischen "▶ Läuft" und "⏸ Wartet"
    /// unterscheiden kann, während der CLI-Prozess noch lebt. Null, solange kein aktiver Lauf bekannt ist.
    /// </summary>
    public AufgabeLaufStatus? LaufStatus { get; set; }

    /// <summary>Concurrency-Token für Recovery-relevante Statusänderungen.</summary>
    public int RecoveryVersion { get; set; }

    /// <summary>Persistierter Vorschlag für den nächsten Prompt.</summary>
    public string? VorschlagPrompt { get; set; }

    /// <summary>Geplanter Ausführungszeitpunkt für den nächsten Prompt.</summary>
    public DateTimeOffset? VorschlagAusfuehrenAbUtc { get; set; }

    /// <summary>Navigationseigenschaft zum übergeordneten Projekt.</summary>
    public Projekt Projekt { get; set; } = null!;

    /// <summary>Navigationseigenschaft zum verknüpften Git-Repository.</summary>
    public GitRepository? GitRepository { get; set; }

    /// <summary>Verknüpfte Issue-Referenz aus dem Git-Provider.</summary>
    public IssueReferenz? IssueReferenz { get; set; }

    /// <summary>Protokolleinträge des KI-Prozesses für diese Aufgabe.</summary>
    public List<Protokolleintrag> Protokolleintraege { get; set; } = [];

    /// <summary>Diff-Ergebnisse für diese Aufgabe (z.B. für verschiedene Dateien oder Vergleiche).</summary>
    public List<DiffResult> DiffResults { get; set; } = [];
}
