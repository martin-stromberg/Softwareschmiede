namespace Softwareschmiede.Domain.Entities;

/// <summary>Referenz auf einen Security- oder Quality-Alert aus einem SCM-Provider.</summary>
public sealed class AlertReferenz
{
    /// <summary>Eindeutige ID der Alert-Referenz.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehörigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>SCM-Provider-Prefix.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Repository-Identifier oder Repository-URL.</summary>
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>Alert-Typ.</summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>Stabile providerweite Quellenkennung.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>URL des Alerts im Provider.</summary>
    public string? AlertUrl { get; set; }

    /// <summary>Titel des Alerts.</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Schweregrad des Alerts.</summary>
    public string? Severity { get; set; }

    /// <summary>Status des Alerts im Provider.</summary>
    public string? State { get; set; }

    /// <summary>Rule-ID des Alerts.</summary>
    public string? RuleId { get; set; }

    /// <summary>Name des Analysewerkzeugs.</summary>
    public string? ToolName { get; set; }

    /// <summary>Navigationseigenschaft zur zugehörigen Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;
}
