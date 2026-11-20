using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der Prüfmodi der SCM-Abschlussüberwachung. Einzige Quelle für die Labels —
/// UI und Tests beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class ScmTaskCompletionCheckModeText
{
    /// <summary>Nur Aufgaben mit SCM-Ereigniszuordnung werden beobachtet.</summary>
    public const string EventDriven = "Nur ereignisgesteuerte Aufgaben";

    /// <summary>Alle noch nicht beendeten Aufgaben mit SCM-Referenz werden beobachtet.</summary>
    public const string AllWorkItems = "Alle Aufgaben";
}

/// <summary>
/// Auswahloption für den Prüfmodus der SCM-Abschlussüberwachung in den Einstellungen: der Steuerwert
/// (<see cref="Mode"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Mode">Der persistierte Prüfmodus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record ScmTaskCompletionCheckModeOption(ScmTaskCompletionCheckMode Mode, string Label)
{
    /// <summary>Alle wählbaren Prüfmodi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<ScmTaskCompletionCheckModeOption> All { get; } =
    [
        new(ScmTaskCompletionCheckMode.EventDriven, ScmTaskCompletionCheckModeText.EventDriven),
        new(ScmTaskCompletionCheckMode.AllWorkItems, ScmTaskCompletionCheckModeText.AllWorkItems)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static ScmTaskCompletionCheckModeOption ForMode(ScmTaskCompletionCheckMode mode)
        => All.First(o => o.Mode == mode);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
