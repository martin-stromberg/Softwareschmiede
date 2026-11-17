namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Credentialfreier, austauschbarer GitHub-Ereignistransport.</summary>
public interface IGitHubEventApiClient
{
    /// <summary>Führt exakt eine API-Seite oder Mutation aus; Bodies bleiben JSON-Daten.</summary>
    Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default);

    /// <summary>Lädt eine Nachweis-Datei in den Attachment-Speicher; Fehler werfen ausschließlich
    /// typisierte Providerfehler ohne OutcomeUncertain — der wiederholbare Upload-Schritt kennt
    /// nie einen ungewissen Ausgang.</summary>
    Task<GitHubEventApiResponse> UploadAsync(GitHubAssetUploadRequest request, CancellationToken ct = default);
}

/// <summary>Einzelner API-Aufruf am vom Provider validierten Host.</summary>
public sealed record GitHubEventApiRequest(string Host, string Method, string Endpoint, string? JsonBody = null);

/// <summary>Binärer Datei-Upload; <see cref="Host"/> ist der Repository-Host (Routing/Ableitung),
/// <see cref="RepositoryId"/> die numerische Repository-ID des Providers.</summary>
public sealed record GitHubAssetUploadRequest(string Host, string RepositoryId, string Name, string ContentType, byte[] Data);

/// <summary>Unveränderte JSON-Antwort einschließlich der für Pagination und Backoff nötigen Header.</summary>
public sealed record GitHubEventApiResponse(int StatusCode, string Body, IReadOnlyDictionary<string, string>? Headers = null);
