namespace Softwareschmiede.Domain.Enums;

/// <summary>Status eines Pull Requests beim Provider.</summary>
public enum PullRequestStatus
{
    /// <summary>Status ist noch nicht bekannt.</summary>
    Unknown,

    /// <summary>Pull Request ist offen.</summary>
    Open,

    /// <summary>Pull Request ist geschlossen, aber nicht gemergt.</summary>
    Closed,

    /// <summary>Pull Request wurde gemergt.</summary>
    Merged
}
