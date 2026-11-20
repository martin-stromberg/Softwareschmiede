namespace Softwareschmiede.Domain.Enums;

/// <summary>Bestimmt, wie eine offene Aufgabe verworfen wird.</summary>
public enum DiscardAction
{
    /// <summary>Aufgabe wird archiviert.</summary>
    Archive,

    /// <summary>Aufgabe wird dauerhaft gelöscht.</summary>
    Delete
}
