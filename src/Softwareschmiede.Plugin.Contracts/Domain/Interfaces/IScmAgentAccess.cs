using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit eines SCM-Plugins, dem KI-Agenten dieselbe SCM-Anbindung
/// bereitzustellen, die der Host selbst verwendet. Fachliche Issue-/PR-Aktionen
/// (Kommentar, Review, Label, Merge, Commit/Push, Schließen u. a.) führt der Agent im
/// Arbeitsverzeichnis selbst aus; der Antwortkanal dient Annahme, Status und Ergebnis.</summary>
public interface IScmAgentAccess
{
    /// <summary>Liefert die Umgebungsvariablen, die der Host beim CLI-Start der
    /// <see cref="TerminalSessionStartSpec"/> hinzufügt, damit die SCM-Werkzeuge des
    /// Agenten (z. B. die <c>gh</c>-CLI und git) mit denselben Berechtigungen arbeiten
    /// wie der Host. Rückgabewerte können Geheimnisse (Tokens) enthalten und dürfen
    /// niemals in Prompts, Protokollen, Ausgabesenken oder Persistenz abgebildet
    /// werden — sie gehören ausschließlich in die Prozessumgebung des Kindprozesses.
    /// Eine leere Liste bedeutet: keine Anbindung verfügbar; der reguläre CLI-Start
    /// darf dadurch nicht blockiert werden.</summary>
    /// <param name="ct">Abbruch-Token.</param>
    Task<IReadOnlyDictionary<string, string?>> GetAgentEnvironmentAsync(CancellationToken ct = default);
}
