namespace Softwareschmiede.Domain.Enums;

/// <summary>Fachliche Herkunft und Funktion einer Pull-Request-Referenz.</summary>
public enum PullRequestReferenzRolle
{
    /// <summary>Der Pull Request wurde als Ergebnis der Aufgabe erstellt.</summary>
    CreatedByTask = 0,

    /// <summary>Die Aufgabe wurde zur Bearbeitung dieses Pull Requests importiert.</summary>
    ReviewSource = 1
}
