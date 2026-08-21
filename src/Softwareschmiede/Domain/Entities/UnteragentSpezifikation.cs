using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Metadaten eines von einem Projektleiter-Agenten erzeugten Unteragenten.</summary>
public sealed class UnteragentSpezifikation
{
    /// <summary>Eindeutige Unteragenten-ID.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public Guid AutonomAufgabeId { get; set; }

    /// <summary>Externe Kennung des Agenten (z. B. vom CLI-Tool vergebene Sub-Agenten-Kennung), nicht identisch mit <see cref="Id"/>.</summary>
    public string ExterneAgentId { get; set; } = string.Empty;

    /// <summary>Task-Identifier.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>Geltungsbereich des Agenten (z. B. "feature-backend").</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Task-Prompt für den Agenten.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Pfad zum Agent-Arbeitsbereich (tasks/task_XXX/).</summary>
    public string VerzeichnisPfad { get; set; } = string.Empty;

    /// <summary>Git-Branch für diesen Agenten.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>Pfad zum Clone für diesen Agenten (clones/repo_feature_X/).</summary>
    public string ClonePfad { get; set; } = string.Empty;

    /// <summary>Erstellungszeitpunkt des Unteragenten.</summary>
    public DateTimeOffset ErzeugungsDatum { get; set; }

    /// <summary>Abschlusszeitpunkt des Unteragenten (null wenn noch aktiv).</summary>
    public DateTimeOffset? AbschlussDatum { get; set; }

    /// <summary>Status des Unteragenten.</summary>
    public UnteragentStatus Status { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public AutonomAufgabeKonfiguration AutonomAufgabe { get; set; } = null!;
}
