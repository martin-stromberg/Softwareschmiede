namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Git-Arbeitsbereich, bestehend aus Branch-Name und lokalem Klon-Pfad.</summary>
/// <param name="BranchName">Name des Git-Branches.</param>
/// <param name="ClonePfad">Lokaler Pfad des Klons.</param>
public sealed record GitArbeitsbereich(
    string BranchName,
    string ClonePfad
);
