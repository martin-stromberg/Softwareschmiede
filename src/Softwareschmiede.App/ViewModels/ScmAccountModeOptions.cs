using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Anzeige-Texte der SCM-Konto-Modi. Einzige Quelle für die Labels — UI und Tests
/// beziehen die Texte von hier, damit sie nicht an mehreren Stellen gepflegt werden.
/// </summary>
public static class ScmAccountModeText
{
    /// <summary>Überwachung und Antworten laufen unter dem Benutzer-Token.</summary>
    public const string UserAccount = "Benutzerkonto";

    /// <summary>Überwachung und Antworten laufen unter dem Token eines separaten Kontos (Notifications-Inbox).</summary>
    public const string SeparateAccount = "Separates Konto";
}

/// <summary>
/// Auswahloption für den SCM-Konto-Modus in den Einstellungen: der Steuerwert
/// (<see cref="Mode"/>) zusammen mit dem sichtbaren Anzeige-Label.
/// </summary>
/// <param name="Mode">Der persistierte Konto-Modus.</param>
/// <param name="Label">Der in der Auswahlbox angezeigte Text.</param>
public sealed record ScmAccountModeOption(ScmAccountMode Mode, string Label)
{
    /// <summary>Alle wählbaren Konto-Modi in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<ScmAccountModeOption> All { get; } =
    [
        new(ScmAccountMode.UserAccount, ScmAccountModeText.UserAccount),
        new(ScmAccountMode.SeparateAccount, ScmAccountModeText.SeparateAccount)
    ];

    /// <summary>Liefert die Auswahloption zu einem gespeicherten Modus.</summary>
    public static ScmAccountModeOption ForMode(ScmAccountMode mode)
        => All.First(o => o.Mode == mode);

    /// <inheritdoc/>
    public override string ToString() => Label;
}
