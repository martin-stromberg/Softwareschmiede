using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Backmerge-Modi. Einzige Quelle für die Labels — UI und Tests
/// beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class ScmBackmergeModusTexte
{
    /// <summary>Automatische Backmerge-Bestätigung deaktiviert.</summary>
    public const string Deaktiviert = "Deaktiviert";

    /// <summary>Backmerge-Pull-Requests in allen Projekten automatisch mergen.</summary>
    public const string Global = "Global aktiviert";

    /// <summary>Backmerge-Pull-Requests nur in Projekten mit aktivierter Option automatisch mergen.</summary>
    public const string Projektbezogen = "Projektbezogen aktivierbar";
}

/// <summary>
/// Auswahloption für den Backmerge-Modus in den Einstellungen: der Steuerwert
/// (<see cref="Modus"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Modus">Der persistierte Backmerge-Modus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record ScmBackmergeModusOption(ScmBackmergeModus Modus, string Label)
{
    /// <summary>Alle wählbaren Backmerge-Modi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<ScmBackmergeModusOption> Alle { get; } =
    [
        new(ScmBackmergeModus.Deaktiviert, ScmBackmergeModusTexte.Deaktiviert),
        new(ScmBackmergeModus.Global, ScmBackmergeModusTexte.Global),
        new(ScmBackmergeModus.Projektbezogen, ScmBackmergeModusTexte.Projektbezogen)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static ScmBackmergeModusOption FuerModus(ScmBackmergeModus modus)
        => Alle.First(o => o.Modus == modus);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
