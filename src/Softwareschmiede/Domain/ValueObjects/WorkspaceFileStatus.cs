namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert den Git-Porcelain-Status einer Datei.</summary>
/// <param name="IndexStatus">Status im Git-Index.</param>
/// <param name="WorktreeStatus">Status im Arbeitsverzeichnis.</param>
public sealed record WorkspaceFileStatus(char IndexStatus, char WorktreeStatus)
{
    /// <summary>Parst einen zweistelligen Git-Porcelain-Statuscode.</summary>
    public static WorkspaceFileStatus Parse(string porcelainCode)
    {
        if (string.IsNullOrWhiteSpace(porcelainCode) || porcelainCode.Length < 2)
        {
            throw new ArgumentException("Porcelain-Status muss aus mindestens zwei Zeichen bestehen.", nameof(porcelainCode));
        }

        return new WorkspaceFileStatus(porcelainCode[0], porcelainCode[1]);
    }

    /// <summary>Gibt an, ob die Datei von Git ignoriert wird.</summary>
    public bool IsIgnored => IndexStatus == '!' && WorktreeStatus == '!';

    /// <summary>Gibt an, ob die Datei nicht versioniert ist.</summary>
    public bool IsUntracked => IndexStatus == '?' && WorktreeStatus == '?';

    /// <summary>Gibt an, ob die Datei gelöscht wurde.</summary>
    public bool IsDeleted => IndexStatus == 'D' || WorktreeStatus == 'D';

    /// <summary>Gibt an, ob die Datei einen Merge-Konflikt hat.</summary>
    public bool IsConflict => IndexStatus == 'U' || WorktreeStatus == 'U';

    /// <summary>Gibt an, ob die Datei umbenannt oder kopiert wurde.</summary>
    public bool IsRenameOrCopy => IndexStatus is 'R' or 'C' || WorktreeStatus is 'R' or 'C';

    /// <summary>Gibt an, ob der Dateityp geändert wurde.</summary>
    public bool IsTypeChanged => IndexStatus == 'T' || WorktreeStatus == 'T';

    /// <summary>Gibt an, ob die Datei im Index gestaged ist.</summary>
    public bool IsStaged => IndexStatus is not (' ' or '?' or '!');

    /// <summary>Gibt an, ob die Datei im Arbeitsverzeichnis Änderungen hat.</summary>
    public bool IsDirty => WorktreeStatus is not (' ' or '?' or '!');

    /// <summary>Kurztext für das Status-Badge.</summary>
    public string BadgeText => IsUntracked
        ? "??"
        : IsIgnored
            ? "!!"
            : IsConflict
                ? "UU"
                : string.Concat(
                    IndexStatus == ' ' ? string.Empty : IndexStatus.ToString(),
                    WorktreeStatus == ' ' ? string.Empty : WorktreeStatus.ToString());

    /// <summary>CSS-Klasse für die farbliche Darstellung des Status.</summary>
    public string CssClass => IsIgnored
        ? "git-status-ignored"
        : IsUntracked
            ? "git-status-untracked"
            : IsConflict
                ? "git-status-conflict"
                : IsDeleted
                    ? "git-status-deleted"
                    : IsRenameOrCopy
                        ? "git-status-renamed"
                        : IsTypeChanged
                            ? "git-status-typechange"
                            : IndexStatus == 'A'
                                ? "git-status-added"
                                : IndexStatus == 'M'
                                    ? "git-status-staged"
                                    : WorktreeStatus == 'M'
                                        ? "git-status-dirty"
                                        : IsStaged
                                            ? "git-status-staged"
                                            : "git-status-dirty";

    /// <summary>Lesbare Beschreibung des Dateistatus.</summary>
    public string Description => IsIgnored
        ? "Ignoriert"
        : IsUntracked
            ? "Nicht versioniert"
            : IsConflict
                ? "Merge-Konflikt"
                : IsDeleted
                    ? "Gelöscht"
                    : IsRenameOrCopy
                        ? "Umbenannt / Kopiert"
                        : IsTypeChanged
                            ? "Typ geändert"
                            : IsStaged && IsDirty
                                ? "Staged + geändert"
                                : IsStaged
                                    ? "Staged"
                                    : "Geändert";
}
