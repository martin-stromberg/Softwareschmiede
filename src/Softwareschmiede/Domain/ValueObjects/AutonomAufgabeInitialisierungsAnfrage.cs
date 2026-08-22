using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Eingabedaten des Initialisierungsdialogs zur Erstellung einer Autonomen Aufgabe.</summary>
/// <param name="ProjektBranchName">Name des dedizierten Projektbranches.</param>
/// <param name="InitialPrompt">Initialprompt für den Projektleiter.</param>
/// <param name="ArbeitsverzeichnisPfad">Absoluter Pfad zum Arbeitsverzeichnis.</param>
/// <param name="RessourcenLimits">Token-Budget und Laufzeitbegrenzung.</param>
/// <param name="PersistenzModus">Persistenz-Modus.</param>
/// <param name="SkillAutogeneration">Flag: Skills automatisch generieren?</param>
/// <param name="PermissionsQuelle">Quelle der permissions.json.</param>
public sealed record AutonomAufgabeInitialisierungsAnfrage(
    string ProjektBranchName,
    string InitialPrompt,
    string ArbeitsverzeichnisPfad,
    RessourcenLimits RessourcenLimits,
    PersistenzModus PersistenzModus,
    bool SkillAutogeneration,
    PermissionsJsonOption PermissionsQuelle = PermissionsJsonOption.Generieren
);
