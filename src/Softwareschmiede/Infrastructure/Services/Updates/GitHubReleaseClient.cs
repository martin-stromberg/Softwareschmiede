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
    public async Task<UpdateInfo?> GetLatestReleaseAsync(UpdateCheckOptions options, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CheckTimeout);

        string? requestUri = $"https://api.github.com/repos/{_options.RepositoryOwner}/{_options.RepositoryName}/releases?per_page=100";
        var visitedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        UpdateInfo? best = null;
        SemanticUpdateVersion? bestVersion = null;

        try
        {
            while (requestUri is not null)
            {
                if (!visitedUris.Add(requestUri))
                {
                    _logger.LogWarning("GitHub-Release-Pagination verweist erneut auf {Uri}.", requestUri);
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Softwareschmiede", "1.0"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub-Release-Prüfung lieferte HTTP {StatusCode}.", (int)response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, cancellationToken: timeoutCts.Token);
                if (releases is null)
                {
                    _logger.LogWarning("GitHub-Release-Prüfung lieferte keine Release-Liste.");
                    return null;
                }

                foreach (var release in releases)
                {
                    var candidate = TryCreateCandidate(release, options, out var candidateVersion);
                    if (candidate is null)
                        continue;

                    if (bestVersion is null || candidateVersion.CompareTo(bestVersion) > 0)
                    {
                        best = candidate;
                        bestVersion = candidateVersion;
                    }
                }

                if (!TryGetNextPageUri(response, out var nextUri))
                {
                    _logger.LogWarning("GitHub-Release-Pagination enthält eine ungültige Folge-URL.");
                    return null;
                }

                if (nextUri is not null && !IsAllowedFollowUpUri(nextUri))
                {
                    _logger.LogWarning("GitHub-Release-Pagination verweist auf eine unzulässige Folge-URL {Uri}.", nextUri);
                    return null;
                }

                requestUri = nextUri?.AbsoluteUri;
            }

            return best;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or UriFormatException)
        {
            _logger.LogWarning(ex, "GitHub-Release-Prüfung ist fehlgeschlagen.");
            return null;
        }
    }

    private UpdateInfo? TryCreateCandidate(GitHubRelease release, UpdateCheckOptions options, out SemanticUpdateVersion version)
    {
        version = null!;
        if (release.Draft || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        if (!UpdateVersionComparer.TryParse(release.TagName, out var parsed))
        {
            _logger.LogWarning("GitHub-Release {TagName} ist keine gültige Update-Version.", release.TagName);
            return null;
        }

        var isPrerelease = release.Prerelease || parsed.IsPrerelease;
        if (isPrerelease && !options.IncludePrereleases)
            return null;

        var asset = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, _options.AssetName, StringComparison.OrdinalIgnoreCase)
            && IsAbsoluteHttpUrl(a.BrowserDownloadUrl));
        if (asset is null)
        {
            _logger.LogWarning("GitHub-Release {TagName} enthält kein nutzbares Asset {AssetName}.", release.TagName, _options.AssetName);
            return null;
        }

        version = parsed;
        return new UpdateInfo(
            UpdateVersionComparer.Normalize(release.TagName),
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl, UriKind.Absolute),
            release.PublishedAt,
            isPrerelease);
    }

    private bool IsAllowedFollowUpUri(Uri uri)
    {
        var expectedPath = $"/repos/{_options.RepositoryOwner}/{_options.RepositoryName}/releases";
        return uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.AbsolutePath.TrimEnd('/'), expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetNextPageUri(HttpResponseMessage response, out Uri? nextUri)
    {
        nextUri = null;
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return true;

        foreach (var linkValue in linkValues)
        {
            foreach (var entry in linkValue.Split(','))
            {
                var segments = entry.Split(';');
                if (segments.Length < 2)
                    continue;

                var isNext = segments.Skip(1).Any(segment =>
                {
                    var pair = segment.Split('=', 2);
                    return pair.Length == 2
                        && string.Equals(pair[0].Trim(), "rel", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(pair[1].Trim().Trim('"'), "next", StringComparison.OrdinalIgnoreCase);
                });
                if (!isNext)
                    continue;

                var rawUri = segments[0].Trim();
                if (rawUri.Length > 2
                    && rawUri.StartsWith('<')
                    && rawUri.EndsWith('>')
                    && Uri.TryCreate(rawUri[1..^1], UriKind.Absolute, out nextUri))
                {
                    return true;
                }

                return false;
            }
        }

        return true;
    }

    private static bool IsAbsoluteHttpUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

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
