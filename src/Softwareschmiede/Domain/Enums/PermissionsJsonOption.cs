namespace Softwareschmiede.Domain.Enums;

/// <summary>Quelle der permissions.json für eine Autonome Aufgabe.</summary>
public enum PermissionsJsonOption
{
    /// <summary>permissions.json wird automatisch generiert.</summary>
    Generate,

    /// <summary>Eine bestehende permissions.json wird ausgewählt.</summary>
    Select,

    /// <summary>Eine vordefinierte permissions.json wird verwendet.</summary>
    Existing
}
