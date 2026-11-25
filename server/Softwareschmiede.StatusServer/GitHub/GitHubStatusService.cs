using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Softwareschmiede.StatusServer.Options;

namespace Softwareschmiede.StatusServer.GitHub;

/// <summary>Read-Only-Sicht eines GitHub-Pull-Requests für die Status-Seite.</summary>
/// <param name="Number">Die Pull-Request-Nummer.</param>
/// <param name="Title">Der Titel.</param>
/// <param name="Url">Die HTML-URL beim Provider.</param>
public sealed record GitHubPullRequestView(int Number, string Title, string Url);

/// <summary>Read-Only-Sicht eines GitHub-Releases für die Status-Seite.</summary>
/// <param name="Name">Der Release-Name (Fallback: Tag).</param>
/// <param name="TagName">Der Tag-Name.</param>
/// <param name="Url">Die HTML-URL beim Provider.</param>
/// <param name="PublishedUtc">Der Veröffentlichungszeitpunkt (UTC).</param>
public sealed record GitHubReleaseView(string Name, string TagName, string Url, DateTimeOffset? PublishedUtc);

/// <summary>Aggregierter, read-only GitHub-Status eines konfigurierten Repositories.</summary>
/// <param name="DisplayName">Der Anzeigename im Format <c>owner/repo</c>.</param>
/// <param name="Url">Die HTML-URL des Repositories.</param>
/// <param name="OpenPullRequests">Offene Pull Requests (Top-N).</param>
/// <param name="Releases">Letzte Releases (Top-N).</param>
/// <param name="OpenIssuesCount">Anzahl offener Issues (ohne PRs); <see langword="null"/> bei Fehler.</param>
/// <param name="FetchedUtc">Zeitpunkt des letzten erfolgreichen Abrufs (UTC); <see langword="null"/>, falls nie erfolgreich.</param>
/// <param name="Error">Letzter Fehler, falls ein Endpunkt fehlschlug.</param>
/// <param name="ErrorUtc">Zeitpunkt des letzten Fehlers (UTC).</param>
public sealed record GitHubRepositoryStatus(
    string DisplayName,
    string Url,
    IReadOnlyList<GitHubPullRequestView> OpenPullRequests,
    IReadOnlyList<GitHubReleaseView> Releases,
    int? OpenIssuesCount,
    DateTimeOffset? FetchedUtc,
    string? Error,
    DateTimeOffset? ErrorUtc);

/// <summary>
/// Aggregiert serverseitig öffentliche GitHub-Repository-Daten mit ETag- und TTL-basiertem Cache.
/// Alle Endpunkte sind read-only; ein optionales Token erhöht lediglich das Rate-Limit.
/// </summary>
public sealed class GitHubStatusService
{
    private readonly HttpClient _httpClient;
    private readonly StatusServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GitHubStatusService> _logger;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly TimeSpan _ttl;

    /// <summary>Erstellt den Dienst.</summary>
    /// <param name="httpClient">Der konfigurierte <see cref="HttpClient"/> (named client <c>github</c>).</param>
    /// <param name="options">Die Server-Konfiguration.</param>
    /// <param name="timeProvider">Die Zeitquelle für den TTL-Cache.</param>
    /// <param name="logger">Der Logger.</param>
    public GitHubStatusService(
        HttpClient httpClient,
        IOptions<StatusServerOptions> options,
        TimeProvider timeProvider,
        ILogger<GitHubStatusService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(Math.Max(5, _options.GitHub.CacheTtlSeconds));
    }

    /// <summary>Liefert den aggregierten Status aller konfigurierten Repositories.</summary>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die Repository-Stati in konfigurierter Reihenfolge.</returns>
    public async Task<IReadOnlyList<GitHubRepositoryStatus>> GetRepositoryStatusesAsync(CancellationToken cancellationToken)
    {
        var result = new List<GitHubRepositoryStatus>();
        foreach (var repoValue in _options.GitHub.Repositories)
        {
            if (!GitHubRepositoryOptions.TryParse(repoValue, out var repo) || repo is null)
            {
                _logger.LogWarning("Ungültiger GitHub-Repository-Eintrag '{Value}' – wird übersprungen.", repoValue);
                continue;
            }

            result.Add(await GetRepositoryStatusAsync(repo, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<GitHubRepositoryStatus> GetRepositoryStatusAsync(
        GitHubRepositoryOptions repo, CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{repo.Owner}/{repo.Name}";

        var pulls = await GetCachedJsonAsync<GitHubPullRequestResponse[]>(
            $"repos/{repo.Owner}/{repo.Name}/pulls?state=open&per_page=20", cancellationToken).ConfigureAwait(false);
        var releases = await GetCachedJsonAsync<GitHubReleaseResponse[]>(
            $"repos/{repo.Owner}/{repo.Name}/releases?per_page=5", cancellationToken).ConfigureAwait(false);
        var issues = await GetCachedJsonAsync<GitHubIssueResponse[]>(
            $"repos/{repo.Owner}/{repo.Name}/issues?state=open&per_page=100", cancellationToken).ConfigureAwait(false);

        var fetchedUtc = new[] { pulls.FetchedUtc, releases.FetchedUtc, issues.FetchedUtc }
            .Where(t => t is not null)
            .DefaultIfEmpty()
            .Max();
        var firstErrorUtc = pulls.Error is not null ? pulls.ErrorUtc
            : releases.Error is not null ? releases.ErrorUtc
            : issues.ErrorUtc;
        var openIssueCount = issues.Value is null
            ? (int?)null
            : issues.Value.Count(i => i.PullRequest is null);

        return new GitHubRepositoryStatus(
            repo.DisplayName,
            url,
            pulls.Value?.Select(p => new GitHubPullRequestView(p.Number, p.Title ?? string.Empty, p.HtmlUrl ?? url)).ToList()
                ?? [],
            releases.Value?.Select(r => new GitHubReleaseView(
                string.IsNullOrEmpty(r.Name) ? r.TagName ?? string.Empty : r.Name,
                r.TagName ?? string.Empty,
                r.HtmlUrl ?? url,
                r.PublishedAt)).ToList() ?? [],
            openIssueCount,
            fetchedUtc,
            pulls.Error ?? releases.Error ?? issues.Error,
            firstErrorUtc);
    }

    private async Task<EndpointResult<T>> GetCachedJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        CacheEntry? previous;
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache.TryGetValue(relativeUrl, out previous);
            if (previous is not null && now - previous.FetchedUtc < _ttl)
                return previous.ToResult<T>();
        }
        finally
        {
            _cacheLock.Release();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            if (previous?.ETag is { Length: > 0 } etag)
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && previous?.Payload is not null)
            {
                await StoreAsync(relativeUrl, previous.ETag, previous.Payload, null).ConfigureAwait(false);
                return EndpointResult<T>.Ok(
                    JsonSerializer.Deserialize<T>(previous.Payload), _timeProvider.GetUtcNow());
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await StoreAsync(relativeUrl, response.Headers.ETag?.Tag, payload, null).ConfigureAwait(false);
            return EndpointResult<T>.Ok(JsonSerializer.Deserialize<T>(payload), _timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            or TaskCanceledException or OperationCanceledException)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;

            _logger.LogWarning(ex, "GitHub-Abruf '{Url}' fehlgeschlagen.", relativeUrl);
            var errorUtc = _timeProvider.GetUtcNow();
            var friendlyError = DescribeError(ex);
            await StoreAsync(relativeUrl, previous?.ETag, previous?.Payload, friendlyError).ConfigureAwait(false);
            var staleValue = previous?.Payload is { } oldPayload
                ? TryDeserialize<T>(oldPayload)
                : default;
            return EndpointResult<T>.Failed(staleValue, previous?.LastSuccessUtc, friendlyError, errorUtc);
        }
    }

    /// <summary>Übersetzt technische Ausnahmen in verständliche deutsche Kategorie-Texte
    /// für die Anzeige; die Rohmeldung bleibt im Log.</summary>
    private static string DescribeError(Exception ex)
        => ex switch
        {
            HttpRequestException { StatusCode: { } statusCode } =>
                statusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        "HTTP 401 – das konfigurierte GitHub-Token ist ungültig oder abgelaufen.",
                    HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                        $"HTTP {(int)statusCode} – Zugriff verweigert bzw. Rate-Limit erschöpft.",
                    HttpStatusCode.NotFound =>
                        "HTTP 404 – Repository oder Endpunkt nicht gefunden.",
                    _ =>
                        $"HTTP {(int)statusCode} – GitHub-Abruf fehlgeschlagen.",
                },
            HttpRequestException =>
                "Netzwerkfehler beim GitHub-Abruf (Server nicht erreichbar).",
            TaskCanceledException or OperationCanceledException =>
                "Zeitüberschreitung beim GitHub-Abruf.",
            JsonException =>
                "Die GitHub-Antwort war nicht lesbar.",
            _ =>
                "GitHub-Abruf fehlgeschlagen.",
        };

    private static T? TryDeserialize<T>(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private async Task StoreAsync(string key, string? etag, string? payload, string? error)
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache.TryGetValue(key, out var previous);
            var now = _timeProvider.GetUtcNow();
            _cache[key] = new CacheEntry(
                etag ?? string.Empty,
                payload,
                now,
                error is null ? now : previous?.LastSuccessUtc,
                error,
                error is not null ? now : previous?.ErrorUtc);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>Cache-Eintrag je Endpunkt. <c>FetchedUtc</c> ist der letzte Abrufversuch
    /// (steuert den TTL-Mindestabstand); <c>LastSuccessUtc</c>/<c>ErrorUtc</c> trennen den
    /// letzten erfolgreichen Abruf vom letzten Fehler, damit die Anzeige nicht eine
    /// Fehlversuchszeit als Datenstand ausgibt.</summary>
    private sealed record CacheEntry(
        string ETag,
        string? Payload,
        DateTimeOffset FetchedUtc,
        DateTimeOffset? LastSuccessUtc,
        string? Error,
        DateTimeOffset? ErrorUtc)
    {
        public EndpointResult<T> ToResult<T>()
        {
            var value = Payload is { } payload ? TryDeserialize<T>(payload) : default;
            return Error is null
                ? EndpointResult<T>.Ok(value, LastSuccessUtc ?? FetchedUtc)
                : EndpointResult<T>.Failed(value, LastSuccessUtc, Error, ErrorUtc ?? FetchedUtc);
        }
    }

    private sealed record EndpointResult<T>(T? Value, DateTimeOffset? FetchedUtc, string? Error, DateTimeOffset? ErrorUtc)
    {
        public static EndpointResult<T> Ok(T? value, DateTimeOffset fetchedUtc)
            => new(value, fetchedUtc, null, null);

        public static EndpointResult<T> Failed(T? staleValue, DateTimeOffset? fetchedUtc, string error, DateTimeOffset errorUtc)
            => new(staleValue, fetchedUtc, error, errorUtc);
    }

    private sealed class GitHubPullRequestResponse
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }
    }

    private sealed class GitHubIssueResponse
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("pull_request")]
        public JsonElement? PullRequest { get; set; }
    }
}
