using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Beschreibt die verfügbaren Git-Aktionen für die UI.</summary>
public sealed record GitActionCapabilities(
    RepositoryKind RepositoryKind,
    bool IsWorkingDirectoryCopy,
    bool CanPush,
    bool CanPull,
    bool CanCreatePullRequest,
    bool CanMergeToSource);
