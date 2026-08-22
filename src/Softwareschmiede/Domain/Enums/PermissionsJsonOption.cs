namespace Softwareschmiede.Domain.Enums;

/// <summary>Quelle der permissions.json für eine Autonome Aufgabe.</summary>
public enum PermissionsJsonOption
{
    /// <summary>permissions.json wird automatisch generiert.</summary>
    Generieren,

    /// <summary>Eine bestehende permissions.json wird ausgewählt.</summary>
    Auswaehlen,

    /// <summary>Eine vordefinierte permissions.json wird verwendet.</summary>
    Vordefiniert
}
