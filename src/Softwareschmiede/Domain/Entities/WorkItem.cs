using System.ComponentModel.DataAnnotations.Schema;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Aufgabe innerhalb eines Projekts, die durch einen KI-Agenten bearbeitet werden kann.</summary>
public sealed class WorkItem
{
    /// <summary>Eindeutige ID der Aufgabe.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Projekts.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Optionale ID des verknüpften Git-Repositories.</summary>
    public Guid? GitRepositoryId { get; set; }

    /// <summary>Titel der Aufgabe.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Anforderungsbeschreibung für den KI-Agenten.</summary>
    public string? RequirementDescription { get; set; }

    /// <summary>Aktueller Status der Aufgabe.</summary>
    public WorkItemStatus Status { get; set; }

    /// <summary>Persistierter Status der KI-Ausführung dieser Aufgabe.</summary>
    public WorkItemExecutionStatus ExecutionStatus { get; set; } = WorkItemExecutionStatus.NotStarted;

    /// <summary>Name des Git-Branches für diese Aufgabe.</summary>
    public string? BranchName { get; set; }

    /// <summary>Lokaler Pfad des geklonten Repositories.</summary>
    public string? LocalClonePath { get; set; }

    /// <summary>Urspruenglicher Start-Branch, von dem der Feature-Branch abgezweigt wurde.</summary>
    public string? BaseBranchName { get; set; }

    /// <summary>Convenience-Zugriff auf <see cref="BranchName"/>, <see cref="LocalClonePath"/> und <see cref="BaseBranchName"/> als Value Object. Nicht von EF Core gemappt; die Werte bleiben einzeln als flache Spalten persistiert. Null, solange kein Branch/Klon-Pfad gesetzt ist.</summary>
    [NotMapped]
    public GitWorkspace? GitWorkspace
    {
        get => BranchName is null || LocalClonePath is null
            ? null
            : new GitWorkspace(BranchName, LocalClonePath, BaseBranchName);
        set
        {
            BranchName = value?.BranchName;
            LocalClonePath = value?.ClonePath;
            BaseBranchName = value?.BaseBranchName;
        }
    }

    /// <summary>Name des verwendeten Agentenpakets.</summary>
    public string? AgentPackageName { get; set; }

    /// <summary>Name des verwendeten Agenten.</summary>
    public string? AgentName { get; set; }

    /// <summary>Prefix des für diese Aufgabe verwendeten KI-Plugins.</summary>
    public string? AiPluginPrefix { get; set; }

    /// <summary>Erstellungsdatum der Aufgabe.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Abschlussdatum der Aufgabe (null wenn noch nicht abgeschlossen).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Optional: Aktive Lauf-ID einer KI-Ausführung.</summary>
    public string? ActiveRunId { get; set; }

    /// <summary>Optional: Zeitstempel des letzten Heartbeats einer Ausführung.</summary>
    public DateTimeOffset? LastHeartbeatUtc { get; set; }

    /// <summary>Optional: Zeitstempel des letzten echten CLI-Prozessstarts.</summary>
    public DateTimeOffset? LastCliStartUtc { get; set; }

    /// <summary>
    /// Optional: Laufzeit-Substatus der aktiven CLI-Ausführung (nur relevant, solange
    /// <see cref="ActiveRunId"/> gesetzt ist). Wird von <c>CliProcessManager</c> anhand des
    /// <c>PseudoConsoleSession.RuntimeStatusChanged</c>-Ereignisses aktualisiert, damit die Seitenleisten-/
    /// Dashboard-Kachel (<c>KiAusfuehrungsStatusConverter</c>) zwischen "▶ Läuft" und "⏸ Wartet"
    /// unterscheiden kann, während der CLI-Prozess noch lebt. Null, solange kein aktiver Lauf bekannt ist.
    /// </summary>
    public WorkItemRunStatus? RunStatus { get; set; }

    /// <summary>Concurrency-Token für Recovery-relevante Statusänderungen.</summary>
    public int RecoveryVersion { get; set; }

    /// <summary>Persistierter Vorschlag für den nächsten Prompt.</summary>
    public string? SuggestedPrompt { get; set; }

    /// <summary>Geplanter Ausführungszeitpunkt für den nächsten Prompt.</summary>
    public DateTimeOffset? SuggestedExecuteAfterUtc { get; set; }

    /// <summary>
    /// Optionaler UTC-Zeitpunkt, bis zu dem die Aufgabe pausiert ist. Ein Wert in der Zukunft
    /// blockiert neue Starts, CLI-Neustarts, Recovery und den Versand zeitgesteuerter Prompts;
    /// ein bereits laufender CLI-Prozess wird dadurch nicht unterbrochen. <c>null</c> oder ein
    /// vergangener Zeitpunkt bedeutet „nicht pausiert".
    /// </summary>
    public DateTimeOffset? PausedUntilUtc { get; set; }

    /// <summary>
    /// UTC-Zeitpunkt, ab dem die Abschlussüberwachung für ereignisgesteuerte Aufgaben die verknüpfte
    /// SCM-Referenz (Pull Request oder Issue) erstmals als geschlossen/gemergt beobachtet hat.
    /// Persistierter Startpunkt der einstellbaren Abschluss-Wartezeit — überlebt App-Neustarts;
    /// <c>null</c>, solange die Referenz offen ist oder noch nicht geprüft wurde.
    /// </summary>
    public DateTimeOffset? ScmCompletionDetectedUtc { get; set; }

    /// <summary>Anzeigetext der für den Abschluss beobachteten SCM-Referenz (z. B. „Pull Request #7"
    /// oder „Issue #3") — Diagnose-/Protokollwert zu <see cref="ScmCompletionDetectedUtc"/>.</summary>
    public string? ScmCompletionReference { get; set; }

    /// <summary>Zweck-Markierung der Aufgabe; <see langword="null"/> kennzeichnet eine reguläre
    /// Arbeitsaufgabe. Als Zweck markierte Aufgaben (z. B. <c>"IssueAufbereitung"</c>) werden von der
    /// kanonischen SCM-Ereignisauflösung ausdrücklich ausgeschlossen.</summary>
    public string? Purpose { get; set; }

    /// <summary>Auftragsart der Aufgabe — ihr fachlicher Ursprung (Issue #451). Getrennt von
    /// <see cref="Purpose"/>: Die Aufbereitungs-Aufgabe behält ihren Purpose zur Isolation,
    /// wechselt aber nach erfolgreicher Zustellung des Analyse-Prompts zu
    /// <see cref="WorkItemKind.Instruction"/>.</summary>
    public WorkItemKind Kind { get; set; } = WorkItemKind.Unspecified;

    /// <summary>Navigationseigenschaft zum übergeordneten Projekt.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>Navigationseigenschaft zum verknüpften Git-Repository.</summary>
    public GitRepository? GitRepository { get; set; }

    /// <summary>Verknüpfte Issue-Referenz aus dem Git-Provider.</summary>
    public IssueReference? IssueReference { get; set; }

    /// <summary>Verknüpfte Alert-Referenz aus dem SCM-Provider.</summary>
    public AlertReference? AlertReference { get; set; }

    /// <summary>Verknüpfte Pull Requests aus dem SCM-Provider.</summary>
    public List<PullRequestReference> PullRequests { get; set; } = [];

    /// <summary>Protokolleinträge des KI-Prozesses für diese Aufgabe.</summary>
    public List<LogEntry> LogEntries { get; set; } = [];

    /// <summary>Diff-Ergebnisse für diese Aufgabe (z.B. für verschiedene Dateien oder Vergleiche).</summary>
    public List<DiffResult> DiffResults { get; set; } = [];

    /// <summary>To-Do-Elemente dieser Aufgabe.</summary>
    public List<Todo> Todos { get; set; } = [];
}
