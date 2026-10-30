using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed partial class GitHubPlugin
{
    private readonly IGitHubEventApiClient _eventApiClient;
    private const string TextFields = "id body createdAt updatedAt lastEditedAt url author { login ... on Node { id } } editor { login ... on Node { id } }";

    /// <inheritdoc />
    public async Task<ScmEventRepository> GetEventRepositoryAsync(ScmRepositoryContext repository, CancellationToken ct = default)
    {
        if (!repository.PluginPrefix.Equals(PluginPrefix, StringComparison.OrdinalIgnoreCase))
            throw Permanent("Der Repository-Provider stimmt nicht überein.");
        var (host, owner, name) = ParseEventRepository(repository.RepositoryId);
        var viewer = await EventApiAsync(host, "GET", "user", null, false, ct);
        var metadata = await EventApiAsync(host, "GET", $"repos/{owner}/{name}", null, false, ct);
        var remoteOwner = Object(metadata, "owner");
        var canonicalOwner = Required(remoteOwner, "login");
        var canonicalName = Required(metadata, "name");
        ValidateSegment(canonicalOwner);
        ValidateSegment(canonicalName);
        var permissions = Object(metadata, "permissions");
        return new(PluginPrefix, host, Required(metadata, "node_id"), canonicalOwner, canonicalName,
            String(remoteOwner, "node_id"), Required(viewer, "node_id"), Required(viewer, "login"),
            Boolean(permissions, "push") || Boolean(permissions, "admin") || Boolean(permissions, "maintain"));
    }

    /// <inheritdoc />
    public async Task<ScmEventBatch> ReadEventsAsync(ScmEventReadRequest request, CancellationToken ct = default)
    {
        ValidateReference(request.Reference);
        var repository = request.Reference.Repository;
        var observations = new List<ScmEventObservation>();
        var field = request.Reference.Kind == ScmReferenceKind.Issue ? "issue" : "pullRequest";
        var variables = new Dictionary<string, object?>
        {
            ["owner"] = repository.Owner,
            ["name"] = repository.Name,
            ["number"] = request.Reference.Number
        };
        if (request.ContentKind == ScmContentKind.Description)
        {
            var data = await GraphAsync(repository.Host,
                "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){" + field + "(number:$number){" + TextFields + "}}}", variables, false, ct);
            AddObservation(request, Object(Object(data, "repository"), field), null, observations);
        }
        else if (request.ContentKind == ScmContentKind.PullRequestReview)
        {
            if (request.Reference.Kind != ScmReferenceKind.PullRequest) throw Permanent("Reviews benötigen eine Pull-Request-Referenz.");
            string? cursor = null;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                variables["cursor"] = cursor;
                var data = await GraphAsync(repository.Host,
                    "query($owner:String!,$name:String!,$number:Int!,$cursor:String){repository(owner:$owner,name:$name){pullRequest(number:$number){reviews(first:100,after:$cursor){nodes{" +
                    TextFields + " fullDatabaseId state submittedAt} pageInfo{hasNextPage endCursor}}}}}", variables, false, ct);
                var connection = Object(Object(Object(data, "repository"), "pullRequest"), "reviews");
                foreach (var node in RequiredArray(connection, "nodes"))
                    if (!string.Equals(String(node, "state"), "PENDING", StringComparison.OrdinalIgnoreCase)
                        && Date(node, "submittedAt") is { } submitted && submitted <= request.WatermarkUtc)
                        AddObservation(request, node, String(node, "fullDatabaseId"), observations);
                cursor = NextCursor(connection, visited);
            } while (cursor is not null);
        }
        else
        {
            if (request.ContentKind is not (ScmContentKind.IssueComment or ScmContentKind.ReviewComment))
                throw Permanent("Unbekannte Textquelle.");
            if (request.ContentKind == ScmContentKind.ReviewComment && request.Reference.Kind != ScmReferenceKind.PullRequest)
                throw Permanent("Review-Kommentare benötigen eine Pull-Request-Referenz.");
            var endpoint = $"{RepositoryEndpoint(repository)}/{(request.ContentKind == ScmContentKind.ReviewComment ? "pulls" : "issues")}/{request.Reference.Number}/comments?per_page=100";
            if (request.SinceUtc is { } since) endpoint += "&since=" + Uri.EscapeDataString(since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            var comments = await RestPagesAsync(repository.Host, endpoint, ct);
            foreach (var comment in comments)
            {
                // Always obtain body and editor in the same GraphQL snapshot, including same-second edits.
                var nodeId = Required(comment, "node_id");
                var type = request.ContentKind == ScmContentKind.ReviewComment ? "PullRequestReviewComment" : "IssueComment";
                var data = await GraphAsync(repository.Host, "query($id:ID!){node(id:$id){... on " + type + "{" + TextFields + "}}}",
                    new { id = nodeId }, false, ct);
                var node = Object(data, "node");
                if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue; // Deleted between calls.
                if (!string.Equals(Required(node, "id"), nodeId, StringComparison.Ordinal)) throw Permanent("GitHub lieferte eine fremde Textreferenz.");
                AddObservation(request, node, Required(comment, "id"), observations);
            }
        }
        return new(request, observations.OrderBy(x => x.Revision).ThenBy(x => x.EventKey, StringComparer.Ordinal).ToArray());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScmEventReference>> EnumerateReferencesAsync(ScmEventRepository repository,
        DateTimeOffset? sinceUtc, CancellationToken ct = default)
    {
        if (!repository.ProviderPrefix.Equals(PluginPrefix, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(repository.RepositoryId)
            || !ParseEventRepository($"https://{repository.Host}/{repository.Owner}/{repository.Name}").Host
                .Equals(repository.Host, StringComparison.OrdinalIgnoreCase))
            throw Permanent("Ungültiger GitHub-Ereignisursprung.");
        var references = new List<ScmEventReference>();
        await CollectReferencesAsync(repository, ScmReferenceKind.Issue, "issues", "[OPEN]", null, references, ct);
        await CollectReferencesAsync(repository, ScmReferenceKind.PullRequest, "pullRequests", "[OPEN]", null, references, ct);
        if (sinceUtc is { } since)
        {
            await CollectReferencesAsync(repository, ScmReferenceKind.Issue, "issues", "[CLOSED]", since, references, ct);
            await CollectReferencesAsync(repository, ScmReferenceKind.PullRequest, "pullRequests", "[CLOSED,MERGED]", since, references, ct);
        }
        return references;
    }

    /// <inheritdoc />
    public async Task<Issue?> ReadIssueAsync(ScmEventReference reference, CancellationToken ct = default)
    {
        ValidateReference(reference);
        if (reference.Kind != ScmReferenceKind.Issue) throw Permanent("Issue-Anlagedaten benötigen eine Issue-Referenz.");
        var repository = reference.Repository;
        var element = await EventApiAsync(repository.Host, "GET", $"{RepositoryEndpoint(repository)}/issues/{reference.Number}", null, false, ct);
        if (element.ValueKind != JsonValueKind.Object) throw Permanent("Unvollständige GitHub-Antwort.");
        // Der Issues-Endpunkt liefert auch Pull Requests; eine fremde Referenzart ist nicht anlegbar.
        if (Object(element, "pull_request").ValueKind == JsonValueKind.Object) return null;
        var labels = Object(element, "labels").ValueKind == JsonValueKind.Array
            ? element.GetProperty("labels").EnumerateArray().Select(x => String(x, "name"))
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray()
            : Array.Empty<string>();
        return new Issue(int.Parse(Required(element, "number"), CultureInfo.InvariantCulture),
            String(element, "title") ?? string.Empty, String(element, "body"), labels,
            String(Object(element, "milestone"), "title"), String(element, "html_url") ?? String(element, "url"))
        {
            IstGeschlossen = string.Equals(String(element, "state"), "closed", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <inheritdoc />
    public async Task<PullRequest?> ReadPullRequestAsync(ScmEventReference reference, CancellationToken ct = default)
    {
        ValidateReference(reference);
        if (reference.Kind != ScmReferenceKind.PullRequest) throw Permanent("Pull-Request-Anlagedaten benötigen eine Pull-Request-Referenz.");
        var repository = reference.Repository;
        var repositoryId = PullRequestRepositoryId.Normalize(PullRequestProvider.GitHub, $"{repository.Owner}/{repository.Name}");
        var element = await EventApiAsync(repository.Host, "GET", $"{RepositoryEndpoint(repository)}/pulls/{reference.Number}", null, false, ct);
        if (element.ValueKind != JsonValueKind.Object) throw Permanent("Unvollständige GitHub-Antwort.");
        var pullRequest = ParsePullRequest(element, repositoryId);
        return pullRequest is not null && pullRequest.Nummer == reference.Number ? pullRequest : null;
    }

    /// <summary>Liest eine paginierte Referenzliste; jüngste Aktivität zuerst, Abbruch am <paramref name="cutoff"/>.</summary>
    private async Task CollectReferencesAsync(ScmEventRepository repository, ScmReferenceKind kind, string field,
        string states, DateTimeOffset? cutoff, List<ScmEventReference> target, CancellationToken ct)
    {
        var variables = new Dictionary<string, object?> { ["owner"] = repository.Owner, ["name"] = repository.Name };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            variables["cursor"] = cursor;
            var data = await GraphAsync(repository.Host,
                "query($owner:String!,$name:String!,$cursor:String){repository(owner:$owner,name:$name){" + field
                + "(first:100,states:" + states + ",orderBy:{field:UPDATED_AT,direction:DESC},after:$cursor){nodes{number updatedAt} pageInfo{hasNextPage endCursor}}}}",
                variables, false, ct);
            var connection = Object(Object(data, "repository"), field);
            var reachedCutoff = false;
            foreach (var node in RequiredArray(connection, "nodes"))
            {
                if (cutoff is not null && Date(node, "updatedAt") is { } updated && updated < cutoff)
                {
                    reachedCutoff = true;
                    break;
                }
                if (!int.TryParse(String(node, "number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number <= 0)
                    throw Permanent("Unvollständige GitHub-Antwort.");
                target.Add(new ScmEventReference(repository, kind, number));
            }
            cursor = reachedCutoff ? null : NextCursor(connection, visited);
        } while (cursor is not null);
    }

    private static void AddObservation(ScmEventReadRequest request, JsonElement node, string? remoteId, List<ScmEventObservation> target)
    {
        if (node.ValueKind != JsonValueKind.Object) throw Permanent("Die verknüpfte GitHub-Referenz ist nicht verfügbar.");
        var created = RequiredDate(node, "createdAt");
        var updated = RequiredDate(node, "updatedAt");
        var edited = Date(node, "lastEditedAt");
        // Do not commit a post-watermark text under an earlier cursor.
        if (created > request.WatermarkUtc || updated > request.WatermarkUtc || edited > request.WatermarkUtc) return;
        var author = Object(node, "author");
        var editor = Object(node, "editor");
        var body = String(node, "body") ?? string.Empty;
        var repo = request.Reference.Repository;
        var authorId = String(author, "id");
        var authorLogin = String(author, "login");
        if (IsViewer(repo, authorId, authorLogin) && body.Contains("<!-- softwareschmiede:", StringComparison.OrdinalIgnoreCase)) return;
        var referencePath = request.Reference.Kind == ScmReferenceKind.Issue ? "issues" : "pull";
        var fragment = request.ContentKind switch
        {
            ScmContentKind.IssueComment => $"#issuecomment-{remoteId}",
            ScmContentKind.ReviewComment => $"#discussion_r{remoteId}",
            ScmContentKind.PullRequestReview => $"#pullrequestreview-{remoteId}",
            _ => string.Empty
        };
        target.Add(new(request.Reference, request.ContentKind, Required(node, "id"), remoteId, created, updated, edited,
            authorId, authorLogin, String(editor, "id"), String(editor, "login"), body,
            $"https://{repo.Host}/{repo.Owner}/{repo.Name}/{referencePath}/{request.Reference.Number}{fragment}", DateTimeOffset.UtcNow));
    }

    private async Task<List<JsonElement>> RestPagesAsync(string host, string endpoint, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var initialPath = endpoint.Split('?')[0];
        string? next = endpoint;
        while (next is not null)
        {
            if (!visited.Add(next)) throw Permanent("GitHub lieferte eine zyklische Seitennavigation.");
            var response = await SendEventApiAsync(new(host, "GET", next), false, ct);
            var array = ParseJson(response.Body);
            if (array.ValueKind != JsonValueKind.Array) throw Permanent("Ungültige GitHub-Seitenantwort.");
            items.AddRange(array.EnumerateArray());
            var link = Header(response, "Link");
            var match = Regex.Match(link ?? string.Empty, "<([^>]+)>;\\s*rel=\"next\"", RegexOptions.CultureInvariant);
            next = match.Success ? ValidateNextPage(host, initialPath, match.Groups[1].Value) : null;
        }
        return items;
    }

    private async Task<JsonElement> EventApiAsync(string host, string method, string endpoint, object? body, bool mutation, CancellationToken ct)
    {
        var result = await SendEventApiAsync(new(host, method, endpoint, body is null ? null : JsonSerializer.Serialize(body)), mutation, ct);
        try { return ParseJson(result.Body); }
        catch (ScmProviderException) when (mutation)
        { throw new ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Antwort nach Schreibversuch nicht auswertbar.", outcomeUncertain: true); }
    }

    private async Task<JsonElement> GraphAsync(string host, string query, object variables, bool mutation, CancellationToken ct)
    {
        var result = await EventApiAsync(host, "POST", "graphql", new { query, variables }, mutation, ct);
        if (result.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var types = errors.EnumerateArray().Select(e => String(e, "type") ?? String(Object(e, "extensions"), "code")).ToArray();
            var kind = types.Contains("RATE_LIMITED") ? ScmProviderFailureKind.RateLimit
                : types.Contains("FORBIDDEN") ? ScmProviderFailureKind.Permission
                : types.Contains("UNAUTHORIZED") ? ScmProviderFailureKind.Authentication : ScmProviderFailureKind.Temporary;
            throw new ScmProviderException(kind, EventFailureMessage(kind, "GitHub-GraphQL-Abfrage"), outcomeUncertain: mutation);
        }
        var data = Object(result, "data");
        if (data.ValueKind != JsonValueKind.Object) throw new ScmProviderException(ScmProviderFailureKind.Temporary,
            "Unvollständige GitHub-GraphQL-Antwort.", outcomeUncertain: mutation);
        return data;
    }

    private async Task<GitHubEventApiResponse> SendEventApiAsync(GitHubEventApiRequest request, bool mutation, CancellationToken ct)
    {
        GitHubEventApiResponse response;
        try { response = await _eventApiClient.SendAsync(request, ct); }
        catch (ScmProviderException ex) when (mutation && ex.FailureKind == ScmProviderFailureKind.Temporary && !ex.OutcomeUncertain)
        { throw new ScmProviderException(ex.FailureKind, ex.Message, ex.RetryAfterUtc, true); }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TimeoutException || (mutation && ex is OperationCanceledException))
        { throw new ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Verbindung unterbrochen.", outcomeUncertain: mutation); }
        if (response.StatusCode is >= 200 and < 300) return response;
        var rateLimited = response.StatusCode == 429 || (response.StatusCode == 403
            && (Header(response, "X-RateLimit-Remaining") == "0" || Header(response, "Retry-After") is not null));
        var kind = rateLimited ? ScmProviderFailureKind.RateLimit : response.StatusCode switch
        {
            401 => ScmProviderFailureKind.Authentication,
            403 => ScmProviderFailureKind.Permission,
            >= 500 => ScmProviderFailureKind.Temporary,
            _ => ScmProviderFailureKind.Permanent
        };
        DateTimeOffset? retry = null;
        if (double.TryParse(Header(response, "Retry-After"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            retry = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(seconds, 0, 86400));
        else if (DateTimeOffset.TryParse(Header(response, "Retry-After"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryDate)) retry = retryDate;
        else if (long.TryParse(Header(response, "X-RateLimit-Reset"), out var reset) && reset is >= 0 and <= 253402300799)
            retry = DateTimeOffset.FromUnixTimeSeconds(reset);
        throw new ScmProviderException(kind, EventFailureMessage(kind, $"HTTP {response.StatusCode}, {request.Host}/{request.Endpoint.Split('?')[0]}"), retry,
            mutation && response.StatusCode >= 500);
    }

    private static string EventFailureMessage(ScmProviderFailureKind kind, string detail) => kind switch
    {
        ScmProviderFailureKind.Authentication => $"GitHub-Anmeldung abgelaufen oder ungültig. Öffnen Sie Einstellungen → Plugins → GitHub. Erneuern Sie unter Authentifizierung den Personal Access Token und speichern Sie. Klicken Sie anschließend hier auf Quellen erneut prüfen. Bis dahin werden neue Anfragen dieser Quelle nicht eingelesen ({detail}).",
        ScmProviderFailureKind.Permission => $"GitHub-Zugriffsberechtigung fehlt. Prüfen Sie unter Einstellungen → Plugins → GitHub → Authentifizierung den Personal Access Token: Er benötigt Zugriff auf das betroffene Repository sowie Lese- und Schreibrechte für Issues und Pull Requests. Speichern Sie Änderungen und klicken Sie anschließend auf Quellen erneut prüfen bzw. beim Ereignis auf Erneut prüfen ({detail}).",
        _ => $"GitHub-Anfrage fehlgeschlagen ({detail})."
    };

    private static string? Header(GitHubEventApiResponse response, string name) =>
        response.Headers?.FirstOrDefault(x => x.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string ValidateNextPage(string host, string initialPath, string next)
    {
        if (!Uri.TryCreate(next, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.Fragment)) throw Permanent("Ungültiges GitHub-Seitenziel.");
        var apiHost = host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? "api.github.com" : host;
        var prefix = host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? "/" : "/api/v3/";
        if (!uri.Host.Equals(apiHost, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.Equals(prefix + initialPath, StringComparison.Ordinal)) throw Permanent("GitHub-Seitenziel verlässt die abgefragte Quelle.");
        return initialPath + uri.Query;
    }

    private static (string Host, string Owner, string Name) ParseEventRepository(string value)
    {
        var host = "github.com";
        var path = value;
        var scp = Regex.Match(value, @"\Agit@(?<host>[A-Za-z0-9.-]+):(?<path>[^?#\\]+)\z", RegexOptions.CultureInvariant);
        if (scp.Success)
        {
            if (!Uri.TryCreate("https://" + scp.Groups["host"].Value, UriKind.Absolute, out var sshHost)
                || sshHost.HostNameType != UriHostNameType.Dns) throw Permanent("Ungültiger GitHub-SSH-Host.");
            host = sshHost.IdnHost;
            path = scp.Groups["path"].Value;
        }
        else if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var supportedTransport = (uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo))
                || (uri.Scheme == "ssh" && (uri.IsDefaultPort || uri.Port == 22) && uri.UserInfo == "git");
            if (!supportedTransport || uri.HostNameType != UriHostNameType.Dns
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) throw Permanent("Ungültige GitHub-Repository-URL.");
            host = uri.IdnHost;
            path = uri.AbsolutePath.Trim('/');
        }
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        var segments = path.Split('/');
        if (segments.Length != 2) throw Permanent("GitHub-Repository muss Owner/Name enthalten.");
        ValidateSegment(segments[0]);
        ValidateSegment(segments[1]);
        return (host, segments[0], segments[1]);
    }

    private void ValidateReference(ScmEventReference reference)
    {
        if (!reference.Repository.ProviderPrefix.Equals(PluginPrefix, StringComparison.OrdinalIgnoreCase)
            || reference.Number <= 0 || !Enum.IsDefined(reference.Kind)) throw Permanent("Ungültiger GitHub-Ereignisursprung.");
        var parsed = ParseEventRepository($"https://{reference.Repository.Host}/{reference.Repository.Owner}/{reference.Repository.Name}");
        if (!parsed.Host.Equals(reference.Repository.Host, StringComparison.OrdinalIgnoreCase)) throw Permanent("Ungültiger GitHub-Host.");
        if (string.IsNullOrWhiteSpace(reference.Repository.RepositoryId) || string.IsNullOrWhiteSpace(reference.Repository.ViewerId))
            throw Permanent("Repository- oder Benutzeridentität fehlt.");
    }

    private static void ValidateSegment(string value)
    {
        if (!Regex.IsMatch(value, @"\A[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant) || value is "." or "..")
            throw Permanent("Ungültiger GitHub-Repository-Bezeichner.");
    }

    private static string RepositoryEndpoint(ScmEventRepository repository) => $"repos/{repository.Owner}/{repository.Name}";
    private static bool IsViewer(ScmEventRepository repository, string? id, string? login) =>
        !string.IsNullOrEmpty(id) ? id.Equals(repository.ViewerId, StringComparison.Ordinal)
        : !string.IsNullOrEmpty(login) && login.Equals(repository.ViewerLogin, StringComparison.OrdinalIgnoreCase);
    private static ScmProviderException Permanent(string message) => new(ScmProviderFailureKind.Permanent, message);
    private static JsonElement ParseJson(string value)
    {
        try { using var document = JsonDocument.Parse(value); return document.RootElement.Clone(); }
        catch (JsonException) { throw Permanent("Ungültige GitHub-JSON-Antwort."); }
    }
    private static JsonElement Object(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static string? String(JsonElement element, string name)
    {
        var value = Object(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }
    private static string Required(JsonElement element, string name) => String(element, name) is { Length: > 0 } value ? value : throw Permanent("Unvollständige GitHub-Antwort.");
    private static bool Boolean(JsonElement element, string name) => Object(element, name).ValueKind == JsonValueKind.True;
    private static DateTimeOffset? Date(JsonElement element, string name) => DateTimeOffset.TryParse(String(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    private static DateTimeOffset RequiredDate(JsonElement element, string name) => Date(element, name) ?? throw Permanent("GitHub-Zeitstempel fehlt.");
    private static IEnumerable<JsonElement> RequiredArray(JsonElement element, string name)
    {
        var value = Object(element, name);
        if (value.ValueKind != JsonValueKind.Array) throw Permanent("Unvollständige GitHub-Seite.");
        return value.EnumerateArray();
    }
    private static string? NextCursor(JsonElement connection, HashSet<string> visited)
    {
        var pageInfo = Object(connection, "pageInfo");
        if (Object(pageInfo, "hasNextPage").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Permanent("GitHub-Seiteninformation fehlt.");
        if (!Boolean(pageInfo, "hasNextPage")) return null;
        var cursor = Required(pageInfo, "endCursor");
        if (!visited.Add(cursor)) throw Permanent("GitHub lieferte einen zyklischen Cursor.");
        return cursor;
    }
}
