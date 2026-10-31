namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Credentialfreier, austauschbarer GitHub-Ereignistransport.</summary>
public interface IGitHubEventApiClient
{
    /// <summary>Führt exakt eine API-Seite oder Mutation aus; Bodies bleiben JSON-Daten.</summary>
    Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default);
}

/// <summary>Einzelner API-Aufruf am vom Provider validierten Host.</summary>
public sealed record GitHubEventApiRequest(string Host, string Method, string Endpoint, string? JsonBody = null);

/// <summary>Unveränderte JSON-Antwort einschließlich der für Pagination und Backoff nötigen Header.</summary>
public sealed record GitHubEventApiResponse(int StatusCode, string Body, IReadOnlyDictionary<string, string>? Headers = null);
