using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Update-Modi. Einzige Quelle für die Labels — UI und Tests
/// beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class UpdateModusTexte
{
    /// <summary>Update-Prüfung deaktiviert.</summary>
    public const string Aus = "Aus";

    /// <summary>Nur auf Updates prüfen, ohne automatische Installation.</summary>
    public const string NurPruefen = "Nur prüfen";

    /// <summary>Beim Programmstart prüfen und bei Befund installieren.</summary>
    public const string BeiProgrammstartPruefenUndAusfuehren = "Bei Programmstart prüfen und ausführen";
}

/// <summary>
/// Auswahloption für den Update-Modus in den Einstellungen: der Steuerwert
/// (<see cref="Modus"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Modus">Der persistierte Update-Modus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record UpdateModusOption(UpdateMode Modus, string Label)
{
    /// <summary>Alle wählbaren Update-Modi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<UpdateModusOption> Alle { get; } =
    [
        new(UpdateMode.Aus, UpdateModusTexte.Aus),
        new(UpdateMode.NurPruefen, UpdateModusTexte.NurPruefen),
        new(UpdateMode.BeiProgrammstartPruefenUndAusfuehren, UpdateModusTexte.BeiProgrammstartPruefenUndAusfuehren)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static UpdateModusOption FuerModus(UpdateMode modus)
        => Alle.First(o => o.Modus == modus);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
