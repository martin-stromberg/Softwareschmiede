namespace Softwareschmiede.Domain.Enums;

/// <summary>Typ eines Diff-Blocks (Gruppe zusammenhängender Änderungen).</summary>
public enum DiffBlockType
{
    /// <summary>Block mit hinzugefügten Zeilen.</summary>
    Added,

    /// <summary>Block mit gelöschten Zeilen.</summary>
    Removed,

    /// <summary>Block mit modifizierten Zeilen.</summary>
    Modified,

    /// <summary>Block mit Kontextzeilen (unverändert).</summary>
    Context
}
