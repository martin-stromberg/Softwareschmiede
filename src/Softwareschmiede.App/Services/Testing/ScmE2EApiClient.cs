using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>Raw GitHub REST/GraphQL fixture transport; production parsing remains in GitHubPlugin.</summary>
internal sealed class ScmE2EApiClient(ScmE2ETestConfiguration configuration) : IGitHubEventApiClient
{
    private readonly object _sync = new();
    private const string Repository = "repos/scm-owner/scm-e2e";

    public Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            File.AppendAllText(configuration.CallsPath, JsonSerializer.Serialize(request) + "\n");
            return Task.FromResult(Respond(request));
        }
    }

    private GitHubEventApiResponse Respond(GitHubEventApiRequest request)
    {
        if (request.Host != "github.com") throw new InvalidOperationException("Unbekannter SCM-Fixturehost.");
        var scenario = configuration.ReadScenario();
        if (!scenario.Authenticated) return new(401, "{}");
        var path = request.Endpoint.Split('?')[0];
        if (path == "user" && request.Method == "GET") return Json(new { node_id = "OWNER", login = "scm-owner" });
        if (path == Repository && request.Method == "GET")
            return Json(new { node_id = "REPO", name = "scm-e2e", owner = new { node_id = "OWNER", login = "scm-owner" }, permissions = new { push = scenario.CanWrite } });
        var responses = ReadResponses();
        if (path == "graphql" && request.Method == "POST")
        {
            using var document = JsonDocument.Parse(request.JsonBody!);
            var query = document.RootElement.GetProperty("query").GetString()!;
            var variables = document.RootElement.GetProperty("variables");
            if (variables.TryGetProperty("id", out var id))
            {
                var text = scenario.Texts.SingleOrDefault(x => x.NodeId == id.GetString());
                if (text is not null) return Json(new { data = new { node = Node(text) } });
                var response = responses.SingleOrDefault(x => "RESPONSE_" + x.Id == id.GetString());
                if (response is not null)
                    return Json(new { data = new { node = Node(new ScmE2EText { Id = response.Id, Body = response.Body }, "RESPONSE_" + response.Id) } });
                return Json(new { data = new { node = (object?)null } });
            }
            if (!variables.TryGetProperty("number", out var numberProperty))
            {
                // Enumeration: offene Referenzen aus den Szenario-Texten; geschlossene liefern nichts.
                var field = query.Contains("pullRequests(", StringComparison.Ordinal) ? "pullRequests" : "issues";
                var prKind = field == "pullRequests";
                object[] nodes = query.Contains("[CLOSED", StringComparison.Ordinal) ? []
                    : scenario.Texts.Where(x => x.PullRequest == prKind).Select(x => x.Number).Distinct()
                        .Select(n => (object)new { number = n, updatedAt = DateTimeOffset.UtcNow }).ToArray();
                return Json(new { data = new { repository = new Dictionary<string, object> { [field] = new { nodes, pageInfo = new { hasNextPage = false, endCursor = (string?)null } } } } });
            }
            var number = numberProperty.GetInt32();
            var pr = query.Contains("pullRequest(", StringComparison.Ordinal);
            if (query.Contains("reviews(", StringComparison.Ordinal))
                return Json(new { data = new { repository = new { pullRequest = new { reviews = new { nodes = Array.Empty<object>(), pageInfo = new { hasNextPage = false, endCursor = (string?)null } } } } } });
            var description = scenario.Texts.SingleOrDefault(x => x.Number == number && x.PullRequest == pr && x.Kind == "Description")
                ?? new ScmE2EText { Id = number + (pr ? 10000 : 0), Number = number, Kind = "Description", PullRequest = pr, Body = "Beschreibung ohne Trigger" };
            return Json(new { data = new { repository = new Dictionary<string, object> { [pr ? "pullRequest" : "issue"] = Node(description) } } });
        }
        if (!path.StartsWith(Repository + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("Unbekannter SCM-Fixture-Endpunkt: " + path);
        if (request.Method == "POST")
        {
            using var document = JsonDocument.Parse(request.JsonBody!);
            var body = document.RootElement.TryGetProperty("body", out var bodyProperty) ? bodyProperty.GetString() ?? "" : "";
            var content = document.RootElement.TryGetProperty("content", out var contentProperty) ? contentProperty.GetString() : null;
            if (!scenario.CanWrite || (scenario.RejectAiResponse && body.Contains("KI-Simulator:", StringComparison.Ordinal))) return new(403, "{}");
            if (!path.EndsWith("/comments", StringComparison.Ordinal) && !path.EndsWith("/reactions", StringComparison.Ordinal))
                throw new InvalidOperationException("Unbekannte SCM-Fixturemutation: " + path);
            var response = new ScmE2ERemoteResponse(path, 100000 + responses.Count, body, content);
            File.AppendAllText(configuration.ResponsesPath, JsonSerializer.Serialize(response) + "\n");
            return Json(Remote(response));
        }
        if (request.Method == "GET" && path.EndsWith("/reactions", StringComparison.Ordinal))
            return Json(responses.Where(x => x.Endpoint == path).Select(Remote));
        var detail = Regex.Match(path, "^" + Repository + @"/(issues|pulls)/(\d+)$");
        if (request.Method == "GET" && detail.Success)
        {
            var detailNumber = int.Parse(detail.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var detailPr = detail.Groups[1].Value == "pulls";
            var title = scenario.Texts.FirstOrDefault(x => x.Number == detailNumber && x.PullRequest == detailPr)?.Titel
                ?? $"SCM Referenz {detailNumber}";
            var body = scenario.Texts.FirstOrDefault(x => x.Number == detailNumber && x.PullRequest == detailPr
                && x.Kind == "Description")?.Body ?? string.Empty;
            if (!detailPr)
                return Json(new { number = detailNumber, title, body, labels = Array.Empty<object>(), milestone = (object?)null,
                    html_url = $"https://github.com/scm-owner/scm-e2e/issues/{detailNumber}" });
            return Json(new
            {
                number = detailNumber, title, body, state = "open", node_id = "PR_" + detailNumber,
                html_url = $"https://github.com/scm-owner/scm-e2e/pull/{detailNumber}",
                head = new { @ref = $"scm-branch-{detailNumber}", sha = "sha" + detailNumber,
                    repo = new { full_name = "scm-owner/scm-e2e", clone_url = "https://github.com/scm-owner/scm-e2e.git" } },
                @base = new { @ref = "main" }
            });
        }
        var match = Regex.Match(path, "^" + Repository + @"/(issues|pulls)/(\d+)/comments$");
        if (request.Method == "GET" && match.Success)
        {
            var number = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var kind = match.Groups[1].Value == "pulls" ? "ReviewComment" : "IssueComment";
            var items = scenario.Texts.Where(x => x.Number == number && x.Kind == kind)
                .Select(x => (object)new { id = x.Id, node_id = x.NodeId, body = x.Body, user = new { node_id = x.Author == "scm-owner" ? "OWNER" : "OTHER", login = x.Author } }).ToList();
            items.AddRange(responses.Where(x => x.Endpoint == path).Select(Remote));
            return Json(items);
        }
        throw new InvalidOperationException("Unbekannter SCM-Fixtureaufruf: " + request.Method + " " + path);
    }

    private List<ScmE2ERemoteResponse> ReadResponses()
        => !File.Exists(configuration.ResponsesPath) ? [] : File.ReadAllLines(configuration.ResponsesPath)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => JsonSerializer.Deserialize<ScmE2ERemoteResponse>(x)!).ToList();

    private static object Node(ScmE2EText text, string? nodeId = null)
        => new
        {
            id = nodeId ?? text.NodeId,
            body = text.Body,
            createdAt = text.CreatedAt,
            updatedAt = text.EditedAt ?? text.CreatedAt,
            lastEditedAt = text.EditedAt,
            url = $"https://github.com/scm-owner/scm-e2e/issues/{text.Number}",
            author = new { id = text.Author == "scm-owner" ? "OWNER" : "OTHER", login = text.Author },
            editor = text.EditedAt is null ? null : new { id = text.Author == "scm-owner" ? "OWNER" : "OTHER", login = text.Author }
        };

    private static object Remote(ScmE2ERemoteResponse response)
        => new
        {
            id = response.Id,
            node_id = "RESPONSE_" + response.Id,
            body = response.Body,
            content = response.Content,
            html_url = "https://github.com/scm-owner/scm-e2e/issues/1#issuecomment-" + response.Id,
            user = new { node_id = "OWNER", login = "scm-owner" }
        };

    private static GitHubEventApiResponse Json(object value) => new(200, JsonSerializer.Serialize(value));
}
