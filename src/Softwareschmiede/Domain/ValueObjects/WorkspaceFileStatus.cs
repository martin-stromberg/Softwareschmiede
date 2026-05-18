namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert den Git-Porcelain-Status einer Datei.</summary>
/// <param name="IndexStatus">Status im Git-Index.</param>
/// <param name="WorktreeStatus">Status im Arbeitsverzeichnis.</param>
public sealed record WorkspaceFileStatus(char IndexStatus, char WorktreeStatus)
{
    public static WorkspaceFileStatus Parse(string porcelainCode)
    {
        if (string.IsNullOrWhiteSpace(porcelainCode) || porcelainCode.Length < 2)
        {
            throw new ArgumentException("Porcelain-Status muss aus mindestens zwei Zeichen bestehen.", nameof(porcelainCode));
        }

        return new WorkspaceFileStatus(porcelainCode[0], porcelainCode[1]);
    }

    public bool IsIgnored => IndexStatus == '!' && WorktreeStatus == '!';

    public bool IsUntracked => IndexStatus == '?' && WorktreeStatus == '?';

    public bool IsDeleted => IndexStatus == 'D' || WorktreeStatus == 'D';

    public bool IsConflict => IndexStatus == 'U' || WorktreeStatus == 'U';

    public bool IsRenameOrCopy => IndexStatus is 'R' or 'C' || WorktreeStatus is 'R' or 'C';

    public bool IsTypeChanged => IndexStatus == 'T' || WorktreeStatus == 'T';

    public bool IsStaged => IndexStatus is not (' ' or '?' or '!');

    public bool IsDirty => WorktreeStatus is not (' ' or '?' or '!');

    public string BadgeText => IsUntracked
        ? "??"
        : IsIgnored
            ? "!!"
            : IsConflict
                ? "UU"
                : string.Concat(
                    IndexStatus == ' ' ? string.Empty : IndexStatus.ToString(),
                    WorktreeStatus == ' ' ? string.Empty : WorktreeStatus.ToString());

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
