using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.Infrastructure.Services.Updates;

/// <summary>Ruft GitHub-Releases per REST API ab.</summary>
public sealed class GitHubReleaseClient : IUpdateReleaseClient
{
    private readonly HttpClient _httpClient;
    private readonly UpdateOptions _options;
    private readonly ILogger<GitHubReleaseClient> _logger;

    /// <inheritdoc cref="GitHubReleaseClient"/>
    public GitHubReleaseClient(HttpClient httpClient, IOptions<UpdateOptions> options, ILogger<GitHubReleaseClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo?> GetLatestStableReleaseAsync(CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CheckTimeout);

        var requestUri = $"https://api.github.com/repos/{_options.RepositoryOwner}/{_options.RepositoryName}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Softwareschmiede", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub-Release-Prüfung lieferte HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: timeoutCts.Token);
            if (release is null || release.Prerelease || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            var asset = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, _options.AssetName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));
            if (asset is null)
            {
                _logger.LogWarning("GitHub-Release {TagName} enthält kein Asset {AssetName}.", release.TagName, _options.AssetName);
                return null;
            }

            if (!UpdateVersionComparer.TryParse(release.TagName, out _))
            {
                _logger.LogWarning("GitHub-Release {TagName} ist keine gültige Update-Version.", release.TagName);
                return null;
            }

            return new UpdateInfo(
                UpdateVersionComparer.Normalize(release.TagName),
                release.TagName,
                asset.Name,
                new Uri(asset.BrowserDownloadUrl),
                release.PublishedAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            _logger.LogWarning(ex, "GitHub-Release-Prüfung ist fehlgeschlagen.");
            return null;
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
