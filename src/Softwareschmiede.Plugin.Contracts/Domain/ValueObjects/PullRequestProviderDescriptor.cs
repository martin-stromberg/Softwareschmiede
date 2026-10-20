using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Zentrale Zuordnung von Provider, Plugin und sichtbarer Bezeichnung.</summary>
public static class PullRequestProviderDescriptor
{
    /// <summary>Liefert den Plugin-Prefix des Providers.</summary>
    public static string GetPluginPrefix(PullRequestProvider provider) => provider switch
    {
        PullRequestProvider.GitHub => "Softwareschmiede.GitHub",
        PullRequestProvider.BitbucketCloud or PullRequestProvider.BitbucketServerDataCenter => "Softwareschmiede.Bitbucket",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter Pull-Request-Provider.")
    };

    /// <summary>Löst einen Plugin-Prefix auf den zugehörigen Pull-Request-Provider auf;
    /// <see langword="null"/> bei unbekannten Prefixen.</summary>
    public static PullRequestProvider? TryGetProviderForPluginPrefix(string? pluginPrefix)
    {
        if (string.Equals(pluginPrefix, "Softwareschmiede.GitHub", StringComparison.OrdinalIgnoreCase))
            return PullRequestProvider.GitHub;
        // Bitbucket kennt nur ein Plugin; Cloud ist der übliche Hosting-Modus und wird bei
        // Unklarheit angenommen (ereignisgesteuerte Quellen liefert derzeit ohnehin nur GitHub).
        if (string.Equals(pluginPrefix, "Softwareschmiede.Bitbucket", StringComparison.OrdinalIgnoreCase))
            return PullRequestProvider.BitbucketCloud;
        return null;
    }

    /// <summary>Liefert die stabile sichtbare Providerbezeichnung.</summary>
    public static string GetDisplayName(PullRequestProvider provider) => provider switch
    {
        PullRequestProvider.GitHub => "GitHub",
        PullRequestProvider.BitbucketCloud => "Bitbucket Cloud",
        PullRequestProvider.BitbucketServerDataCenter => "Bitbucket Server/Data Center",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter Pull-Request-Provider.")
    };

    /// <summary>Normalisiert einen konfigurierten Bitbucket-Hosting-Modus.</summary>
    public static PullRequestProvider ParseBitbucketHostingMode(string? hostingMode)
    {
        if (string.Equals(hostingMode, "Cloud", StringComparison.OrdinalIgnoreCase))
            return PullRequestProvider.BitbucketCloud;
        if (string.Equals(hostingMode, "SelfHosted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hostingMode, "Server", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hostingMode, "DataCenter", StringComparison.OrdinalIgnoreCase))
            return PullRequestProvider.BitbucketServerDataCenter;

        throw new InvalidOperationException($"Unbekannter Bitbucket-Hosting-Modus '{hostingMode ?? "<leer>"}'.");
    }
}
