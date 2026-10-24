using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Auflösung der konfigurierbaren SCM-Prompt-Anhänge: Bildet eine Beobachtung auf ihren
/// Ereignisart-Schlüssel ab und kombiniert den allgemeinen mit dem spezifischen Anhang.
/// Der Anhang wird ausschließlich beim eigentlichen Prompt-Versand an die CLI ergänzt;
/// der gespeicherte Ereignistext, die Trigger-Validierung und die Deduplizierung bleiben frei davon.
/// </summary>
public static class ScmPromptAppendix
{
    /// <summary>Alle fünf Einstellungsschlüssel der Prompt-Anhänge (für gebündelte Leseabfragen).</summary>
    public static readonly IReadOnlyCollection<string> SettingKeys =
    [
        AppEinstellungService.ScmPromptAppendGeneralKey,
        AppEinstellungService.ScmPromptAppendIssueKey,
        AppEinstellungService.ScmPromptAppendIssueCommentKey,
        AppEinstellungService.ScmPromptAppendPullRequestKey,
        AppEinstellungService.ScmPromptAppendPullRequestCommentKey,
    ];

    /// <summary>
    /// Bildet eine Beobachtung auf den Schlüssel ihres spezifischen Prompt-Anhangs ab:
    /// Die Beobachtungsart <see cref="ScmContentKind.Description"/> (der Referenztext selbst) wählt den
    /// Issue- bzw. Pull-Request-Anhang; jede andere Beobachtungsart — Kommentar, Review-Kommentar und
    /// veröffentlichte Pull-Request-Review — wählt den Kommentar-Anhang der Referenzart. Reviews und
    /// Review-Kommentare fallen bewusst unter <c>prcomment</c>: Sie gehören fachlich zum laufenden
    /// Review-/Kommentarstrom eines Pull Requests und kein „neuer PR"-Ereignistext.
    /// </summary>
    /// <param name="observation">Die beobachtete Textversion.</param>
    /// <returns>Der Einstellungsschlüssel des spezifischen Anhangs.</returns>
    public static string SpecificKeyFor(ScmEventObservation observation)
        => observation.Reference.Kind == ScmReferenceKind.PullRequest
            ? observation.ContentKind == ScmContentKind.Description
                ? AppEinstellungService.ScmPromptAppendPullRequestKey
                : AppEinstellungService.ScmPromptAppendPullRequestCommentKey
            : observation.ContentKind == ScmContentKind.Description
                ? AppEinstellungService.ScmPromptAppendIssueKey
                : AppEinstellungService.ScmPromptAppendIssueCommentKey;

    /// <summary>
    /// Kombiniert den allgemeinen und den spezifischen Anhang: Zuerst <paramref name="general"/>,
    /// dann <paramref name="specific"/>, mit einer Leerzeile getrennt. Leere oder nur aus Leerraum
    /// bestehende Werte werden ignoriert; gespeicherte Werte werden an den Rändern getrimmt.
    /// </summary>
    /// <param name="general">Der allgemeine Anhang (kann <see langword="null"/> sein).</param>
    /// <param name="specific">Der ereignisartspezifische Anhang (kann <see langword="null"/> sein).</param>
    /// <returns>Der kombinierte Anhang oder <see langword="null"/>, wenn keiner gesetzt ist.</returns>
    public static string? Combine(string? general, string? specific)
    {
        var parts = new[] { general, specific }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();
        return parts.Length == 0 ? null : string.Join("\n\n", parts);
    }

    /// <summary>
    /// Ermittelt den für eine Beobachtung wirksamen Prompt-Anhang aus den gelesenen Einstellungswerten:
    /// allgemeiner Anhang zuerst, dann der spezifische der Ereignisart.
    /// </summary>
    /// <param name="observation">Die beobachtete Textversion.</param>
    /// <param name="values">Schlüssel-Wert-Zuordnung, z. B. aus <see cref="AppEinstellungService.GetScmPromptAppendicesAsync"/>.</param>
    /// <returns>Der anzuhängende Text oder <see langword="null"/>, wenn nichts konfiguriert ist.</returns>
    public static string? Resolve(ScmEventObservation observation, IReadOnlyDictionary<string, string?> values)
        => Combine(
            values.GetValueOrDefault(AppEinstellungService.ScmPromptAppendGeneralKey),
            values.GetValueOrDefault(SpecificKeyFor(observation)));
}
