using System.Text;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Fester, deterministischer Analyse-Prompt der Issue-Aufbereitung. Er wird aus den
/// persistierten Erkennungsdaten (Issue-Titel aus <see cref="IssueReferenz"/> und Beschreibung aus
/// <see cref="ScmEventObservation.Body"/>) gebaut und bildet den <c>Body</c> der
/// <see cref="ScmExchangeRequest"/> — damit ist der gespeicherte <c>request.json</c>-Inhalt beim
/// Wiederanlauf bitgleich rekonstruierbar. Der Kommentar-Schalter fließt bewusst nicht ein:
/// Der Prompt verlangt immer beide Aktionen; ob der Kommentar remote angewendet wird, erzwingt
/// der Host beim Ergebnisabgleich (ehrliche, nachträglich änderbare Entscheidung).</summary>
public static class ScmIssueAufbereitungPrompt
{
    /// <summary>Baut den Analyse-Prompt für ein beobachtetes Issue.</summary>
    /// <param name="titel">Der Issue-Titel (persistiert, unverändert).</param>
    /// <param name="beschreibung">Die Issue-Beschreibung zum Erkennungszeitpunkt.</param>
    public static string Build(string? titel, string? beschreibung)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Du analysierst und verbesserst ein neu angelegtes Issue. Lies Titel und");
        sb.AppendLine("Beschreibung unten sorgfältig und prüfe sie gegen folgende Qualitätskriterien:");
        sb.AppendLine("- Verständlichkeit und sprachliche Qualität");
        sb.AppendLine("- Vollständigkeit und fehlende Informationen");
        sb.AppendLine("- offene Fragen");
        sb.AppendLine("- technische und fachliche Risiken");
        sb.AppendLine("- Zielkonflikte");
        sb.AppendLine("- alternative Lösungsansätze");
        sb.AppendLine("- mögliche Fehlinterpretationen");
        sb.AppendLine();
        sb.AppendLine("Aufgaben:");
        sb.AppendLine("1. Erstelle eine optimierte Anforderungsbeschreibung, die die gefundenen Schwächen");
        sb.AppendLine("   behebt, fehlende Angaben als offene Punkte kenntlich macht und den ursprünglichen");
        sb.AppendLine("   Inhalt nicht verfälscht. Sie ersetzt die bisherige Issue-Beschreibung vollständig.");
        sb.AppendLine("2. Dokumentiere deine Analyse (Schwachstellen, offene Fragen, alternative");
        sb.AppendLine("   Lösungsansätze, Risiken, Zielkonflikte, Verbesserungsvorschläge).");
        sb.AppendLine();
        sb.AppendLine("Liefere das Ergebnis als Antwort-Aktionen:");
        sb.AppendLine("- genau eine \"issueBodyUpdate\"-Aktion mit der optimierten Beschreibung in \"body\"");
        sb.AppendLine("  (der Issue-Titel wird nicht geändert — nimm keine Titeländerung vor), und");
        sb.AppendLine("- eine \"comment\"-Aktion mit der Analyse-Dokumentation in \"body\".");
        sb.AppendLine();
        sb.AppendLine("=== Issue-Titel ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(titel) ? "(kein Titel)" : titel.Trim());
        sb.AppendLine();
        sb.AppendLine("=== Issue-Beschreibung ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(beschreibung) ? "(leer)" : beschreibung.Trim());
        return sb.ToString();
    }
}
