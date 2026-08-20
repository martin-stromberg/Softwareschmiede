namespace Softwareschmiede.Domain.Enums;

/// <summary>Lifecycle-Status einer <see cref="Entities.SkillDefinition"/>.</summary>
public enum SkillStatus
{
    /// <summary>Der Skill befindet sich im Entwurf.</summary>
    Entwurf,

    /// <summary>Der Skill wird überprüft.</summary>
    Review,

    /// <summary>Der Skill ist freigegeben und aktiv nutzbar.</summary>
    Freigegeben,

    /// <summary>Der Skill wurde archiviert und ist nicht mehr aktiv nutzbar.</summary>
    Archiviert
}
