using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der SCM-Konto-Modi. Einzige Quelle für die Labels — UI und Tests
/// beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class ScmKontoModusTexte
{
    /// <summary>Überwachung und Antworten laufen unter dem Benutzer-Token.</summary>
    public const string Benutzerkonto = "Benutzerkonto";

    /// <summary>Überwachung und Antworten laufen unter dem Token eines separaten Kontos (Notifications-Inbox).</summary>
    public const string SeparatesKonto = "Separates Konto";
}

/// <summary>
/// Auswahloption für den SCM-Konto-Modus in den Einstellungen: der Steuerwert
/// (<see cref="Modus"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Modus">Der persistierte Konto-Modus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record ScmKontoModusOption(ScmKontoModus Modus, string Label)
{
    /// <summary>Alle wählbaren Konto-Modi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<ScmKontoModusOption> Alle { get; } =
    [
        new(ScmKontoModus.Benutzerkonto, ScmKontoModusTexte.Benutzerkonto),
        new(ScmKontoModus.SeparatesKonto, ScmKontoModusTexte.SeparatesKonto)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static ScmKontoModusOption FuerModus(ScmKontoModus modus)
        => Alle.First(o => o.Modus == modus);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
