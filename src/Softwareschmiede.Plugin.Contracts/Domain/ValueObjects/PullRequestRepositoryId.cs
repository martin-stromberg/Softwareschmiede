using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Normalisiert und validiert providerabhaengige API-Repository-IDs.</summary>
public static class PullRequestRepositoryId
{
    /// <summary>Normalisiert eine API-ID oder wirft bei einer ungueltigen Form eine Ausnahme.</summary>
    public static string Normalize(PullRequestProvider provider, string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        var value = repositoryId.Trim().Replace('\\', '/');
        if (value.Contains("://", StringComparison.Ordinal)
            || value.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
            || value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Repository-IDs muessen als providerinterne API-ID und nicht als URL angegeben werden.", nameof(repositoryId));
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Die Repository-ID muss genau aus zwei nicht leeren Segmenten bestehen.", nameof(repositoryId));

        var first = parts[0];
        var second = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];
        if (string.IsNullOrWhiteSpace(second))
            throw new ArgumentException("Der Repository-Name darf nicht leer sein.", nameof(repositoryId));

        return provider switch
        {
            PullRequestProvider.GitHub or PullRequestProvider.BitbucketCloud
                => $"{first.ToLowerInvariant()}/{second.ToLowerInvariant()}",
            PullRequestProvider.BitbucketServerDataCenter
                => $"{first.ToUpperInvariant()}/{second.ToLowerInvariant()}",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter Pull-Request-Provider.")
        };
    }

    /// <summary>Versucht, eine API-ID zu normalisieren.</summary>
    public static bool TryNormalize(PullRequestProvider provider, string? repositoryId, out string normalized)
    {
        try
        {
            normalized = Normalize(provider, repositoryId ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            normalized = string.Empty;
            return false;
        }
    }
}
