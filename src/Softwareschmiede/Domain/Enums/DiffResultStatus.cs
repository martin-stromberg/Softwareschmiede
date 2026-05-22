namespace Softwareschmiede.Domain.Enums;

/// <summary>Status eines Diff-Ergebnisses.</summary>
public enum DiffResultStatus
{
    /// <summary>Diff-Generierung steht aus (in Warteschlange).</summary>
    Pending,

    /// <summary>Diff wurde erfolgreich generiert.</summary>
    Generated,

    /// <summary>Diff wurde aus Cache geladen.</summary>
    Cached,

    /// <summary>Fehler bei der Diff-Generierung.</summary>
    Error
}
