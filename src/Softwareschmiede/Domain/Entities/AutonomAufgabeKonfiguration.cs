using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Konfiguration einer Autonomen Aufgabe unter Steuerung eines Projektleiter-Agenten.</summary>
public sealed class AutonomAufgabeKonfiguration
{
    /// <summary>Eindeutige ID der Konfiguration.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Name des dedizierten Projektbranches.</summary>
    public string ProjektBranchName { get; set; } = string.Empty;

    /// <summary>Initialprompt für den Projektleiter.</summary>
    public string InitialPrompt { get; set; } = string.Empty;

    /// <summary>Pfad zur permissions.json.</summary>
    public string PermissionsJsonPfad { get; set; } = string.Empty;

    /// <summary>Token-Budget für die Gesamtaufgabe.</summary>
    public int TokenBudget { get; set; }

    /// <summary>Optionales erweitertes Token-Budget.</summary>
    public int? TokenBudgetErweitert { get; set; }

    /// <summary>Nettozeit-Limit in Minuten.</summary>
    public int LaufzeitLimitMinuten { get; set; }

    /// <summary>Persistenz-Modus.</summary>
    public PersistenzModus PersistenzModus { get; set; }

    /// <summary>Flag: Skills automatisch generieren?</summary>
    public bool SkillAutogeneration { get; set; }

    /// <summary>Pfad zum Arbeitsverzeichnis der Autonomen Aufgabe.</summary>
    public string ArbeitsverzeichnisPfad { get; set; } = string.Empty;

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;

    /// <summary>Unteragenten dieser Autonomen Aufgabe.</summary>
    public List<UnteragentSpezifikation> Unteragenten { get; set; } = [];

    /// <summary>Skills dieser Autonomen Aufgabe.</summary>
    public List<SkillDefinition> Skills { get; set; } = [];
}
