namespace Softwareschmiede.Domain.Enums;

/// <summary>Aktivierungsmodus für die automatische Bestätigung von Backmerge-Pull-Requests.</summary>
public enum ScmBackmergeMode
{
    /// <summary>Backmerge-Pull-Requests werden nicht automatisch gemergt.</summary>
    Disabled = 0,

    /// <summary>Backmerge-Pull-Requests werden in allen Projekt-Repositories automatisch gemergt.</summary>
    Global = 1,

    /// <summary>Backmerge-Pull-Requests werden nur in Projekten mit aktivierter Projektoption automatisch gemergt.</summary>
    ProjectScoped = 2
}
