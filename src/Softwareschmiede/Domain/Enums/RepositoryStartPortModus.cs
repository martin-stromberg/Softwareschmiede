namespace Softwareschmiede.Domain.Enums;

/// <summary>Portmodus für Repository-Startskripte.</summary>
public enum RepositoryStartPortModus
{
    /// <summary>Freien Port automatisch ermitteln.</summary>
    Auto,

    /// <summary>Fest definierten Port verwenden.</summary>
    Fest,

    /// <summary>Port dem Skript übergeben, damit es ihn selbst verarbeitet.</summary>
    ScriptGesteuert
}
