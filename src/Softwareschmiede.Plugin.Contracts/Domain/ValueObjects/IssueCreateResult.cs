namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis einer Issue-Anlage beim Provider.</summary>
/// <param name="Status">Status des Anlageversuchs.</param>
/// <param name="Issue">Angelegtes Issue bei erfolgreicher Anlage.</param>
/// <param name="ErrorMessage">Verständliche Fehlermeldung bei Nichtunterstützung oder Fehler.</param>
public sealed record IssueCreateResult(
    IssueCreateResultStatus Status,
    Issue? Issue,
    string? ErrorMessage)
{
    /// <summary>Erzeugt ein erfolgreiches Ergebnis.</summary>
    public static IssueCreateResult Success(Issue issue) => new(IssueCreateResultStatus.Success, issue, null);

    /// <summary>Erzeugt ein Nichtunterstützt-Ergebnis.</summary>
    public static IssueCreateResult NotSupported(string message) => new(IssueCreateResultStatus.NotSupported, null, message);

    /// <summary>Erzeugt ein Fehler-Ergebnis.</summary>
    public static IssueCreateResult Failed(string message) => new(IssueCreateResultStatus.Failed, null, message);

    /// <summary>Gibt an, ob die Anlage erfolgreich war.</summary>
    public bool IsSuccess => Status == IssueCreateResultStatus.Success && Issue is not null;
}

/// <summary>Status einer Issue-Anlage.</summary>
public enum IssueCreateResultStatus
{
    /// <summary>Issue wurde erfolgreich angelegt.</summary>
    Success,

    /// <summary>Der Provider unterstützt Issue-Anlage nicht.</summary>
    NotSupported,

    /// <summary>Der Provider unterstützt Issue-Anlage, der Aufruf ist aber fehlgeschlagen.</summary>
    Failed
}
