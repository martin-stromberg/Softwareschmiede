using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Eingabedaten des Initialisierungsdialogs zur Erstellung einer Autonomen Aufgabe.</summary>
/// <param name="ProjektBranchName">Name des dedizierten Projektbranches.</param>
/// <param name="InitialPrompt">Initialprompt für den Projektleiter.</param>
/// <param name="ArbeitsverzeichnisPfad">Absoluter Pfad zum Arbeitsverzeichnis.</param>
/// <param name="TokenBudget">Token-Budget für die Gesamtaufgabe.</param>
/// <param name="TokenBudgetErweitert">Optionales erweitertes Token-Budget.</param>
/// <param name="LaufzeitLimitMinuten">Nettozeit-Limit in Minuten.</param>
/// <param name="PersistenzModus">Persistenz-Modus.</param>
/// <param name="SkillAutogeneration">Flag: Skills automatisch generieren?</param>
/// <param name="PermissionsQuelle">Quelle der permissions.json.</param>
public sealed record AutonomAufgabeInitialisierungsAnfrage(
    string ProjektBranchName,
    string InitialPrompt,
    string ArbeitsverzeichnisPfad,
    int TokenBudget,
    int? TokenBudgetErweitert,
    int LaufzeitLimitMinuten,
    PersistenzModus PersistenzModus,
    bool SkillAutogeneration,
    PermissionsJsonOption PermissionsQuelle = PermissionsJsonOption.Generate
);
