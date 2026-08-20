using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Metadaten eines von einem Projektleiter-Agenten erzeugten Unteragenten.</summary>
public sealed class UnteragentSpezifikation
{
    /// <summary>Eindeutige Unteragenten-ID.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public Guid AutonomAufgabeId { get; set; }

    /// <summary>Agent-Identifier.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Task-Identifier.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>Geltungsbereich des Agenten (z. B. "feature-backend").</summary>
    public string AgentScope { get; set; } = string.Empty;

    /// <summary>Task-Prompt für den Agenten.</summary>
    public string AgentPrompt { get; set; } = string.Empty;

    /// <summary>Pfad zum Agent-Arbeitsbereich (tasks/task_XXX/).</summary>
    public string AgentDirectory { get; set; } = string.Empty;

    /// <summary>Git-Branch für diesen Agenten.</summary>
    public string AgentBranch { get; set; } = string.Empty;

    /// <summary>Pfad zum Clone für diesen Agenten (clones/repo_feature_X/).</summary>
    public string AgentClone { get; set; } = string.Empty;

    /// <summary>Erstellungszeitpunkt des Unteragenten.</summary>
    public DateTimeOffset ErzeugungsDatum { get; set; }

    /// <summary>Abschlusszeitpunkt des Unteragenten (null wenn noch aktiv).</summary>
    public DateTimeOffset? AbschlussDatum { get; set; }

    /// <summary>Status des Unteragenten.</summary>
    public UnteragentStatus Status { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public AutonomAufgabeKonfiguration AutonomAufgabe { get; set; } = null!;
}
