using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Update-Modi. Einzige Quelle für die Labels — UI und Tests
/// beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class UpdateModusTexte
{
    /// <summary>Update-Prüfung deaktiviert.</summary>
    public const string Off = "Aus";

    /// <summary>Nur auf Updates prüfen, ohne automatische Installation.</summary>
    public const string CheckOnly = "Nur prüfen";

    /// <summary>Beim Programmstart prüfen und bei Befund installieren.</summary>
    public const string CheckAndRunAtStartup = "Bei Programmstart prüfen und ausführen";
}

/// <summary>
/// Auswahloption für den Update-Modus in den Einstellungen: der Steuerwert
/// (<see cref="Mode"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Mode">Der persistierte Update-Modus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record UpdateModusOption(UpdateMode Mode, string Label)
{
    /// <summary>Alle wählbaren Update-Modi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<UpdateModusOption> All { get; } =
    [
        new(UpdateMode.Off, UpdateModusTexte.Off),
        new(UpdateMode.CheckOnly, UpdateModusTexte.CheckOnly),
        new(UpdateMode.CheckAndRunAtStartup, UpdateModusTexte.CheckAndRunAtStartup)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static UpdateModusOption FuerModus(UpdateMode mode)
        => All.First(o => o.Mode == mode);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
