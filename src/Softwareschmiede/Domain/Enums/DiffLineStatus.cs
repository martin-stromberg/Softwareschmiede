namespace Softwareschmiede.Domain.Enums;

/// <summary>Status einer einzelnen Zeile in einem Diff.</summary>
public enum DiffLineStatus
{
    /// <summary>Zeile wurde hinzugefügt.</summary>
    Added,

    /// <summary>Zeile wurde gelöscht.</summary>
    Removed,

    /// <summary>Zeile wurde modifiziert.</summary>
    Modified,

    /// <summary>Zeile ist unverändert (Kontextzeile).</summary>
    Context
}
