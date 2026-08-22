using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Versionierte Skill-Definition für einen Projektleiter-Agenten oder Unteragenten.</summary>
public sealed class SkillDefinition
{
    /// <summary>Eindeutige ID der Skill-Definition.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public Guid AutonomAufgabeId { get; set; }

    /// <summary>Name des Skills (z. B. "projektleiter-v1").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Versionsnummer des Skills.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Markdown-Inhalt des Skills.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Lifecycle-Status des Skills.</summary>
    public SkillStatus Status { get; set; }

    /// <summary>Erstellungszeitpunkt der Skill-Definition.</summary>
    public DateTimeOffset ErstellungsDatum { get; set; }

    /// <summary>Freigabezeitpunkt der Skill-Definition (null solange nicht freigegeben).</summary>
    public DateTimeOffset? FreigabeDatum { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Autonomen Aufgabe (Konfiguration).</summary>
    public AutonomAufgabeKonfiguration AutonomAufgabe { get; set; } = null!;
}
