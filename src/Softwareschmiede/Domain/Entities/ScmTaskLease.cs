namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistenter SCM-Ereignisdatensatz: ScmTaskLease.</summary>
public sealed class ScmTaskLease
{
    /// <summary>AufgabeId des Ereignisprozesses.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Owner des Ereignisprozesses.</summary>
    public string Owner { get; set; } = "";

    /// <summary>ExpiresUtc des Ereignisprozesses.</summary>
    public long ExpiresUtc { get; set; }
}
