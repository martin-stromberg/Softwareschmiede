namespace Softwareschmiede.Domain.Enums;

/// <summary>Von einem Unteragenten angeforderte Aktion, die vom <see cref="Application.Services.UnteragentGovernanceService"/> geprüft wird.</summary>
public enum UnteragentAktion
{
    /// <summary>Das Agent-Arbeitsverzeichnis wird erstellt.</summary>
    ArbeitsverzeichnisErstellen,

    /// <summary>Ein Pull Request wird erstellt (Unteragenten grundsätzlich verboten).</summary>
    PullRequestErstellen,

    /// <summary>Ein Skill wird modifiziert (Unteragenten grundsätzlich verboten).</summary>
    SkillModifizieren
}
