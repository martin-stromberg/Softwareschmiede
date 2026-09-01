namespace Softwareschmiede.Domain.Enums;

/// <summary>Unterstuetzter Provider fuer gespeicherte Pull Requests.</summary>
public enum PullRequestProvider
{
    /// <summary>GitHub Pull Request.</summary>
    GitHub = 0,

    /// <summary>Bitbucket Cloud Pull Request.</summary>
    BitbucketCloud = 1,

    /// <summary>Bitbucket Server oder Data Center Pull Request.</summary>
    BitbucketServerDataCenter = 2
}
