namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis des optionalen Ladens von Issue-Templates.</summary>
/// <param name="Status">Status des Ladevorgangs.</param>
/// <param name="Templates">Geladene Templates.</param>
/// <param name="ErrorMessage">Fehlermeldung bei echtem Ladefehler.</param>
public sealed record IssueTemplateLoadResult(
    IssueTemplateLoadResultStatus Status,
    IReadOnlyList<IssueTemplate> Templates,
    string? ErrorMessage)
{
    /// <summary>Erzeugt ein erfolgreiches Template-Ergebnis.</summary>
    public static IssueTemplateLoadResult Success(IEnumerable<IssueTemplate> templates)
        => new(IssueTemplateLoadResultStatus.Success, templates.ToList(), null);

    /// <summary>Erzeugt ein Nichtunterstützt-Ergebnis.</summary>
    public static IssueTemplateLoadResult NotSupported(string? message = null)
        => new(IssueTemplateLoadResultStatus.NotSupported, [], message);

    /// <summary>Erzeugt ein Fehler-Ergebnis.</summary>
    public static IssueTemplateLoadResult Failed(string message)
        => new(IssueTemplateLoadResultStatus.Failed, [], message);
}

/// <summary>Status eines Template-Ladevorgangs.</summary>
public enum IssueTemplateLoadResultStatus
{
    /// <summary>Templates wurden erfolgreich geladen, die Liste kann leer sein.</summary>
    Success,

    /// <summary>Der Provider unterstützt Templates nicht.</summary>
    NotSupported,

    /// <summary>Der Provider unterstützt Templates, der Ladevorgang ist aber fehlgeschlagen.</summary>
    Failed
}
