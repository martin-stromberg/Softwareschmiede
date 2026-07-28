namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Art einer offenen SCM-Anforderung.</summary>
public enum ScmRequirementKind
{
    /// <summary>Normales Provider-Issue.</summary>
    Issue = 0,

    /// <summary>Provider-Alert.</summary>
    Alert = 1
}
