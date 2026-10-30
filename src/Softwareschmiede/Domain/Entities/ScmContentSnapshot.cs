namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistenter SCM-Ereignisdatensatz: ScmContentSnapshot.</summary>
public sealed class ScmContentSnapshot
{
    /// <summary>ContentKey des Ereignisprozesses.</summary>
    public string ContentKey { get; set; } = "";

    /// <summary>BodyHash des Ereignisprozesses.</summary>
    public string BodyHash { get; set; } = "";

    /// <summary>Revision des Ereignisprozesses.</summary>
    public string Revision { get; set; } = "";

    /// <summary>IdentityResolved des Ereignisprozesses.</summary>
    public bool IdentityResolved { get; set; }

    /// <summary>Diagnostic des Ereignisprozesses.</summary>
    public string? Diagnostic { get; set; }
}
