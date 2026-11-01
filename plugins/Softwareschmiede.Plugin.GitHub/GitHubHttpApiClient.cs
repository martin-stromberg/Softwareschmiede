using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Prozessloser GitHub-API-Transport über <see cref="HttpClient"/>; der Token fließt
/// ausschließlich aus dem Credential-Store in den <c>Authorization</c>-Header — niemals in URLs,
/// Logs oder Fehlertexte.</summary>
public sealed class GitHubHttpApiClient : IGitHubEventApiClient
{
    private static readonly Lazy<HttpClient> Shared = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(100) });

    private readonly Func<string?> _tokenProvider;
    private readonly HttpClient _httpClient;

    /// <summary>Erstellt den Produktivtransport mit dem statisch geteilten <see cref="HttpClient"/>.</summary>
    /// <param name="tokenProvider">Liefert pro Anfrage das aktuelle Credential (oder <see langword="null"/>).</param>
    public GitHubHttpApiClient(Func<string?> tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _httpClient = Shared.Value;
    }

    /// <summary>Erstellt den Transport über einem eigenen <see cref="HttpMessageHandler"/> (Tests).</summary>
    public GitHubHttpApiClient(Func<string?> tokenProvider, HttpMessageHandler handler)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) };
    }

    /// <inheritdoc />
    public async Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default)
    {
        try
        {
            var token = _tokenProvider();
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), BuildRequestUri(request));
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            message.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            message.Headers.UserAgent.ParseAdd("Softwareschmiede");
            if (!string.IsNullOrEmpty(token))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (request.JsonBody is not null)
                message.Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(message, ct);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                headers[header.Key] = string.Join(",", header.Value);
            return new((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct), headers);
        }
        catch (ScmProviderException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new ScmProviderException(ScmProviderFailureKind.Temporary,
                "GitHub-Transport nicht erreichbar.", outcomeUncertain: request.Method != "GET" && request.Endpoint != "graphql");
        }
    }

    private static Uri BuildRequestUri(GitHubEventApiRequest request)
    {
        var host = ValidateHost(request.Host);
        var endpoint = request.Endpoint.TrimStart('/');
        var isGithubCom = host.Equals("github.com", StringComparison.OrdinalIgnoreCase);
        var uriString = endpoint.Equals("graphql", StringComparison.OrdinalIgnoreCase)
            ? isGithubCom ? "https://api.github.com/graphql" : $"https://{host}/api/graphql"
            : isGithubCom ? $"https://api.github.com/{endpoint}" : $"https://{host}/api/v3/{endpoint}";
        return new Uri(uriString, UriKind.Absolute);
    }

    /// <summary>Validiert einen DNS-Hostnamen ohne UserInfo-/Port-/Pfad-/Query-/Fragment-Bestandteile
    /// (Token-Grenze: der Bearer-Token darf nie an einen fremden Origin gehen) und liefert den
    /// normalisierten IDN-Host.</summary>
    internal static string ValidateHost(string host)
    {
        if (!Uri.TryCreate("https://" + host, UriKind.Absolute, out var uri)
            || uri.HostNameType != UriHostNameType.Dns
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new ScmProviderException(ScmProviderFailureKind.Permanent, "Ungültiger GitHub-Host.");
        return uri.IdnHost;
    }
}
