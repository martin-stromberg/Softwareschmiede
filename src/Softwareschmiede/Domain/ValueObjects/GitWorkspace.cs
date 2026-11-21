namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Git-Arbeitsbereich, bestehend aus Branch-Name, lokalem Klon-Pfad und dem urspruenglichen Start-Branch.</summary>
/// <param name="BranchName">Name des Git-Branches.</param>
/// <param name="ClonePath">Lokaler Pfad des Klons.</param>
/// <param name="BaseBranchName">Optionaler urspruenglicher Start-Branch, von dem der Feature-Branch abgezweigt wurde.</param>
public sealed record GitWorkspace(
    string BranchName,
    string ClonePath,
    string? BaseBranchName = null);
