using System.Text.RegularExpressions;
using System.Text.Json;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed partial class GitHubPlugin
{
    /// <inheritdoc />
    public async Task<ScmResponseResult?> ReconcileResponseAsync(ScmResponseRequest request, CancellationToken ct = default)
    {
        ValidateResponse(request);
        if (request.Kind == ScmResponseKind.Reaction && request.ContentKind == ScmContentKind.PullRequestReview)
            return await ReconcileReviewReactionAsync(request, ct);
        var repository = request.Reference.Repository;
        var endpoint = request.Kind switch
        {
            ScmResponseKind.Comment => $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}/comments?per_page=100",
            ScmResponseKind.PullRequest => $"{RepositoryEndpoint(repository)}/pulls?state=all&per_page=100",
            ScmResponseKind.Reaction => ReactionEndpoint(request) + "?per_page=100",
            _ => throw Permanent("Unbekannte GitHub-Antwortaktion.")
        };
        // Read all pages before reporting absence; a failed later page must not permit a second POST.
        var candidates = await RestPagesAsync(repository.Host, endpoint, ct);
        foreach (var candidate in candidates)
        {
            var user = Object(candidate, "user");
            if (!IsViewer(repository, String(user, "node_id"), String(user, "login"))) continue;
            var matches = request.Kind == ScmResponseKind.Reaction
                ? String(candidate, "content") == request.Reaction
                : (String(candidate, "body") ?? string.Empty).Contains(request.Marker, StringComparison.Ordinal);
            if (matches) return new(Required(candidate, "id"), SafeResponseUrl(request, candidate), true);
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<ScmResponseResult> ExecuteResponseAsync(ScmResponseRequest request, CancellationToken ct = default)
    {
        ValidateResponse(request);
        var repository = request.Reference.Repository;
        // A renamed/replaced repository or changed credential must not redirect a persisted answer.
        var current = await GetEventRepositoryAsync(new(Guid.NewGuid(), PluginPrefix,
            $"https://{repository.Host}/{repository.Owner}/{repository.Name}"), ct);
        if (current.RepositoryId != repository.RepositoryId || current.ViewerId != repository.ViewerId)
            throw new ScmProviderException(ScmProviderFailureKind.Permission, "Repository- oder Benutzeridentität hat sich geändert.");
        if (!current.CanWrite) throw new ScmProviderException(ScmProviderFailureKind.Permission, "GitHub-Schreibberechtigung fehlt.");
        var existing = await ReconcileResponseAsync(request, ct);
        if (existing is not null) return existing;
        JsonElement result;
        if (request.Kind == ScmResponseKind.Reaction && request.ContentKind == ScmContentKind.PullRequestReview)
        {
            var data = await GraphAsync(repository.Host,
                "mutation($subject:ID!,$content:ReactionContent!){addReaction(input:{subjectId:$subject,content:$content}){reaction{id}}}",
                new { subject = request.RemoteNodeId, content = GraphReaction(request.Reaction!) }, true, ct);
            result = Object(Object(data, "addReaction"), "reaction");
        }
        else
        {
            var (endpoint, body) = request.Kind switch
            {
                ScmResponseKind.Comment => ($"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}/comments",
                    (object)new { body = ResponseBody(request) }),
                ScmResponseKind.Reaction => (ReactionEndpoint(request), (object)new { content = request.Reaction }),
                ScmResponseKind.PullRequest => ($"{RepositoryEndpoint(repository)}/pulls",
                    (object)new { title = request.Title, body = ResponseBody(request), head = request.HeadBranch, @base = request.BaseBranch }),
                _ => throw Permanent("Unbekannte GitHub-Antwortaktion.")
            };
            result = await EventApiAsync(repository.Host, "POST", endpoint, body, true, ct);
        }
        var id = String(result, "id");
        if (string.IsNullOrWhiteSpace(id)) throw new ScmProviderException(ScmProviderFailureKind.Temporary,
            "GitHub-Schreibantwort enthält keine bestätigte Aktions-ID.", outcomeUncertain: true);
        return new(id, SafeResponseUrl(request, result));
    }

    private async Task<ScmResponseResult?> ReconcileReviewReactionAsync(ScmResponseRequest request, CancellationToken ct)
    {
        var repository = request.Reference.Repository;
        string? cursor = null;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        ScmResponseResult? found = null;
        do
        {
            var data = await GraphAsync(repository.Host,
                "query($id:ID!,$cursor:String){node(id:$id){... on PullRequestReview{pullRequest{number repository{id}} reactions(first:100,after:$cursor){nodes{id content user{id login}} pageInfo{hasNextPage endCursor}}}}}",
                new { id = request.RemoteNodeId, cursor }, false, ct);
            var node = Object(data, "node");
            var pullRequest = Object(node, "pullRequest");
            if (String(Object(pullRequest, "repository"), "id") != repository.RepositoryId
                || String(pullRequest, "number") != request.Reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw Permanent("Review-Reaktion gehört nicht zum gespeicherten Ursprung.");
            var connection = Object(node, "reactions");
            foreach (var reaction in RequiredArray(connection, "nodes"))
            {
                var user = Object(reaction, "user");
                if (String(reaction, "content") == GraphReaction(request.Reaction!)
                    && IsViewer(repository, String(user, "id"), String(user, "login")))
                    found = new(Required(reaction, "id"), null, true);
            }
            cursor = NextCursor(connection, visited);
        } while (cursor is not null);
        return found;
    }

    private void ValidateResponse(ScmResponseRequest request)
    {
        ValidateReference(request.Reference);
        if (!Regex.IsMatch(request.ActionId, @"\A[A-Za-z0-9:_-]{1,200}\z", RegexOptions.CultureInvariant))
            throw Permanent("Ungültige Antwortaktions-ID.");
        switch (request.Kind)
        {
            case ScmResponseKind.Comment:
                if (string.IsNullOrWhiteSpace(request.Body)) throw Permanent("Antworttext fehlt.");
                break;
            case ScmResponseKind.Reaction:
                _ = GraphReaction(request.Reaction ?? string.Empty);
                if (request.ContentKind == ScmContentKind.PullRequestReview)
                {
                    if (request.Reference.Kind != ScmReferenceKind.PullRequest || string.IsNullOrWhiteSpace(request.RemoteNodeId))
                        throw Permanent("Review-Ursprung fehlt.");
                }
                else _ = ReactionEndpoint(request);
                break;
            case ScmResponseKind.PullRequest:
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.HeadBranch) || string.IsNullOrWhiteSpace(request.BaseBranch))
                    throw Permanent("Titel und Branches des Pull Requests fehlen.");
                // Branches are JSON data; only a branch of this repository can be selected.
                if (request.HeadBranch.Contains(':') || request.BaseBranch.Contains(':')
                    || request.HeadBranch.Any(char.IsControl) || request.BaseBranch.Any(char.IsControl))
                    throw Permanent("Pull-Request-Branches müssen zum gespeicherten Repository gehören.");
                break;
            default: throw Permanent("Unbekannte GitHub-Antwortaktion.");
        }
    }

    private static string ResponseBody(ScmResponseRequest request) => $"{request.Body ?? string.Empty}\n\n{request.Marker}";

    private static string ReactionEndpoint(ScmResponseRequest request)
    {
        var prefix = RepositoryEndpoint(request.Reference.Repository);
        if (request.ContentKind == ScmContentKind.Description)
            return $"{prefix}/issues/{request.Reference.Number}/reactions";
        if (request.ContentKind is not (ScmContentKind.IssueComment or ScmContentKind.ReviewComment)
            || !long.TryParse(request.RemoteCommentId, out var id) || id <= 0)
            throw Permanent("Kommentar-Ursprung für die Reaktion fehlt.");
        if (request.ContentKind == ScmContentKind.ReviewComment && request.Reference.Kind != ScmReferenceKind.PullRequest)
            throw Permanent("Review-Kommentar benötigt eine Pull-Request-Referenz.");
        return $"{prefix}/{(request.ContentKind == ScmContentKind.ReviewComment ? "pulls" : "issues")}/comments/{id}/reactions";
    }

    private static string GraphReaction(string content) => content switch
    {
        "+1" => "THUMBS_UP",
        "-1" => "THUMBS_DOWN",
        "laugh" => "LAUGH",
        "confused" => "CONFUSED",
        "heart" => "HEART",
        "hooray" => "HOORAY",
        "rocket" => "ROCKET",
        "eyes" => "EYES",
        _ => throw Permanent("Unbekannte GitHub-Reaktion.")
    };

    private static string? SafeResponseUrl(ScmResponseRequest request, JsonElement result)
    {
        var url = String(result, "html_url");
        if (url is null) return null;
        var repo = request.Reference.Repository;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort
            && uri.Host.Equals(repo.Host, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath.StartsWith($"/{repo.Owner}/{repo.Name}/", StringComparison.OrdinalIgnoreCase) ? url : null;
    }
}
