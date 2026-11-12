using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Prüfmodi der SCM-Abschlussüberwachung. Einzige Quelle für die Labels —
/// UI und Tests beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class ScmTaskCompletionPruefmodusTexte
{
    /// <summary>Nur Aufgaben mit SCM-Ereigniszuordnung werden beobachtet.</summary>
    public const string Ereignisgesteuert = "Nur ereignisgesteuerte Aufgaben";

    /// <summary>Alle noch nicht beendeten Aufgaben mit SCM-Referenz werden beobachtet.</summary>
    public const string AlleAufgaben = "Alle Aufgaben";
}

/// <summary>
/// Auswahloption für den Prüfmodus der SCM-Abschlussüberwachung in den Einstellungen: der Steuerwert
/// (<see cref="Modus"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Modus">Der persistierte Prüfmodus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record ScmTaskCompletionPruefmodusOption(ScmTaskCompletionPruefmodus Modus, string Label)
{
    /// <summary>Alle wählbaren Prüfmodi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<ScmTaskCompletionPruefmodusOption> Alle { get; } =
    [
        new(ScmTaskCompletionPruefmodus.Ereignisgesteuert, ScmTaskCompletionPruefmodusTexte.Ereignisgesteuert),
        new(ScmTaskCompletionPruefmodus.AlleAufgaben, ScmTaskCompletionPruefmodusTexte.AlleAufgaben)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static ScmTaskCompletionPruefmodusOption FuerModus(ScmTaskCompletionPruefmodus modus)
        => Alle.First(o => o.Modus == modus);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
