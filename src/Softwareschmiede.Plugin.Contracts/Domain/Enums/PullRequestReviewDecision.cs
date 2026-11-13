namespace Softwareschmiede.Domain.Enums;

/// <summary>Review-Entscheidungszustand eines Pull Requests beim Provider.</summary>
public enum PullRequestReviewDecision
{
    /// <summary>Review-Entscheidung ist nicht bekannt oder wird vom Plugin nicht geliefert.</summary>
    Unknown,

    /// <summary>Es ist kein Review erforderlich oder es liegt keine Vorgabe vor.</summary>
    None,

    /// <summary>Ein Review ist erforderlich und liegt noch nicht vor.</summary>
    ReviewRequired,

    /// <summary>Ein Reviewer hat Änderungen angefordert.</summary>
    ChangesRequested,

    /// <summary>Der Pull Request ist genehmigt.</summary>
    Approved
}
