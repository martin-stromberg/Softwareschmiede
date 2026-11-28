using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

public sealed partial class GitHubPlugin
{
    private readonly IGitHubEventApiClient _eventApiClient;
    private readonly IGitHubEventApiClient _agentEventApiClient;
    /// <summary>Plugin-interner Auflösungscache: kanonischer Schlüssel
    /// <c>{clientId}|{host}|{owner}|{name}</c> (lowercased, Client-Instanz als Konto-Dimension) auf
    /// das unveränderliche <see cref="ScmEventRepository"/> samt Verfallszeitpunkt. Nur erfolgreiche
    /// Auflösungen werden eingetragen; Eviction erfolgt bei <c>Authentication</c>- (ganzes Konto) und
    /// <c>Permission</c>-Fehlern (Konto+Host) sowie implizit per TTL.</summary>
    private readonly ConcurrentDictionary<string, (ScmEventRepository Value, DateTimeOffset ExpiresUtc)> _resolutionCache = new(StringComparer.Ordinal);
    /// <summary>Fail-fast-Sperrtabelle nach Rate-Limit: <c>(Client-Instanz, Host, BudgetTyp)</c> auf den
    /// Sperr-bis-Zeitpunkt. Die Budget-Typ-Dimension (<see langword="true"/> = GraphQL-Kontingent)
    /// trennt die Kontingente: eine REST-Erschöpfung sperrt die GraphQL-Auflösung nicht mit.</summary>
    private readonly ConcurrentDictionary<(IGitHubEventApiClient Client, string Host, bool GraphQl), DateTimeOffset> _rateLimitGates = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _resolutionTtl;
    /// <summary>Feste lokale Fallback-Sperre für Rate-Limit-Fehler ohne serverseitigen
    /// Reset-Zeitpunkt: GraphQL-<c>RATE_LIMITED</c> aus <c>errors[]</c> sowie HTTP-429/403 mit
    /// <c>X-RateLimit-Remaining: 0</c>, aber ohne auswertbare <c>Retry-After</c>-/
    /// <c>X-RateLimit-Reset</c>-Header.</summary>
    private static readonly TimeSpan RateLimitLockFallbackDuration = TimeSpan.FromSeconds(60);
    private const string TextFields = "id body createdAt updatedAt lastEditedAt url author { login ... on Node { id } } editor { login ... on Node { id } } reactionGroups { content viewerHasReacted }";

    /// <summary>Sicherheitsabstand für den serverseitigen <c>since</c>-Filter. GitHub vergleicht Zeitpunkte
    /// aus der wiederholten Stunde der US-Zeitumstellung um eine Stunde verschoben (beobachtet am
    /// 2026-11-01: Notification mit <c>updated_at</c> 09:47:34Z wurde wie 08:47:34Z gefiltert). Ohne
    /// Abstand fällt ein solcher Eintrag dauerhaft aus dem Fenster. Wiederholt gelieferte Einträge sind
    /// unschädlich, weil Beobachtungen über Snapshots und Ereignisschlüssel dedupliziert werden.</summary>
    internal static readonly TimeSpan SinceSafetyMargin = TimeSpan.FromHours(2);

    /// <summary>Formatiert den an GitHub übergebenen <c>since</c>-Parameter inklusive Sicherheitsabstand.</summary>
    /// <param name="since">Der Beginn des lokalen Abfragefensters.</param>
    /// <returns>Der URL-kodierte Abfrageparameter samt führendem <c>&amp;</c>.</returns>
    private static string SinceParameter(DateTimeOffset since)
        => "&since=" + Uri.EscapeDataString((since - SinceSafetyMargin).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    /// <inheritdoc />
    /// <remarks>Auflösungspfad mit Rate-Limit-Schonung: erfolgreiche Ergebnisse werden TTL-basiert
    /// im plugin-internen <see cref="_resolutionCache"/> gehalten (Schlüssel je Client-Instanz,
    /// Host, Owner und Name), sodass zyklische Auflösungen (Poll, Dispatch, Outbox) keinen
    /// Transport-Call mehr erzeugen. Der Primärtransport ist eine einzige GraphQL-Abfrage
    /// (Viewer + Repository + <c>viewerPermission</c>) auf dem freien GraphQL-Kontingent; die
    /// bisherige Zwei-Call-REST-Auflösung bleibt als Fallback ausschließlich bei
    /// <see cref="ScmProviderFailureKind.Temporary"/>- und
    /// <see cref="ScmProviderFailureKind.RateLimit"/>-GraphQL-Fehlern bestehen. Fehlschläge werden
    /// niemals gecacht.</remarks>
    public async Task<ScmEventRepository> GetEventRepositoryAsync(ScmRepositoryContext repository, CancellationToken ct = default)
    {
        if (!repository.PluginPrefix.Equals(PluginPrefix, StringComparison.OrdinalIgnoreCase))
            throw Permanent("Der Repository-Provider stimmt nicht überein.");
        var client = ClientFor(repository.Account);
        var (host, owner, name) = ParseEventRepository(repository.RepositoryId);
        var cacheKey = ResolutionCacheKey(client, host, owner, name);
        if (_resolutionCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > _timeProvider.GetUtcNow())
            return cached.Value;
        ScmEventRepository resolved;
        try { resolved = await ResolveEventRepositoryGraphQlAsync(client, host, owner, name, repository.Account, ct); }
        catch (ScmProviderException ex) when (ex.FailureKind is ScmProviderFailureKind.Temporary or ScmProviderFailureKind.RateLimit)
        { resolved = await ResolveEventRepositoryRestAsync(client, host, owner, name, repository.Account, ct); }
        _resolutionCache[cacheKey] = (resolved, _timeProvider.GetUtcNow().Add(_resolutionTtl));
        return resolved;
    }

    /// <inheritdoc />
    /// <remarks>Die kanonische Form (<c>https://{host}/{owner}/{name}</c>) ist selbst ein parsebarer
    /// Alias und dient hostseitig als Gruppierungs- und Checkpoint-Schlüssel.</remarks>
    public string NormalizeRepositoryAlias(string alias)
    {
        var (host, owner, name) = ParseEventRepository(alias);
        return $"https://{host}/{owner}/{name}";
    }

    /// <summary>Primärer Auflösungstransport: eine GraphQL-Abfrage liefert Viewer-Identität,
    /// Repository-Metadaten und <c>viewerPermission</c> in einem Call.
    /// <c>repository: null</c> bzw. ein <c>NOT_FOUND</c>-Fehler (aus <see cref="GraphAsync"/> bereits
    /// <see cref="ScmProviderFailureKind.Permanent"/> klassifiziert) bedeutet „für das Konto nicht
    /// sichtbar" und ist permanent.</summary>
    private async Task<ScmEventRepository> ResolveEventRepositoryGraphQlAsync(IGitHubEventApiClient client,
        string host, string owner, string name, ScmAccountMode account, CancellationToken ct)
    {
        var data = await GraphAsync(client, host,
            "query($owner:String!,$name:String!){viewer{login id} repository(owner:$owner,name:$name){id databaseId name owner{login id} viewerPermission}}",
            new Dictionary<string, object?> { ["owner"] = owner, ["name"] = name }, false, ct);
        var remote = Object(data, "repository");
        if (remote.ValueKind != JsonValueKind.Object)
            throw Permanent("Das GitHub-Repository ist für das Konto nicht sichtbar.");
        var remoteOwner = Object(remote, "owner");
        var canonicalOwner = Required(remoteOwner, "login");
        var canonicalName = Required(remote, "name");
        ValidateSegment(canonicalOwner);
        ValidateSegment(canonicalName);
        var viewer = Object(data, "viewer");
        return new(PluginPrefix, host, Required(remote, "id"), canonicalOwner, canonicalName,
            Required(remoteOwner, "id"), Required(viewer, "id"), Required(viewer, "login"),
            String(remote, "viewerPermission") is "ADMIN" or "MAINTAIN" or "WRITE",
            account, String(remote, "databaseId"));
    }

    /// <summary>Fallback-Auflösungstransport (GHES-Kompatibilität und GraphQL-Störungen):
    /// die bisherige Zwei-Call-REST-Auflösung <c>GET user</c> + <c>GET repos/{owner}/{name}</c>
    /// mit unverändertem Mapping.</summary>
    private async Task<ScmEventRepository> ResolveEventRepositoryRestAsync(IGitHubEventApiClient client,
        string host, string owner, string name, ScmAccountMode account, CancellationToken ct)
    {
        var viewer = await EventApiAsync(client, host, "GET", "user", null, false, ct);
        var metadata = await EventApiAsync(client, host, "GET", $"repos/{owner}/{name}", null, false, ct);
        var remoteOwner = Object(metadata, "owner");
        var canonicalOwner = Required(remoteOwner, "login");
        var canonicalName = Required(metadata, "name");
        ValidateSegment(canonicalOwner);
        ValidateSegment(canonicalName);
        var permissions = Object(metadata, "permissions");
        return new(PluginPrefix, host, Required(metadata, "node_id"), canonicalOwner, canonicalName,
            String(remoteOwner, "node_id"), Required(viewer, "node_id"), Required(viewer, "login"),
            Boolean(permissions, "push") || Boolean(permissions, "admin") || Boolean(permissions, "maintain"),
            account, String(metadata, "id"));
    }

    /// <summary>Kanonischer Cache-Schlüssel der Auflösung: Identitäts-Hash der Client-Instanz als
    /// Konto-Dimension plus lowercased <c>{host}|{owner}|{name}</c>. Host- und Segmentzeichen
    /// enthalten durch <see cref="ParseEventRepository"/> bzw. <see cref="ValidateSegment"/> nie
    /// <c>|</c>, sodass das Format eindeutig bleibt.</summary>
    private static string ResolutionCacheKey(IGitHubEventApiClient client, string host, string owner, string name)
        => string.Join('|', RuntimeHelpers.GetHashCode(client), host.ToLowerInvariant(),
            owner.ToLowerInvariant(), name.ToLowerInvariant());

    /// <summary>Verwirft gecachte Auflösungen: <paramref name="host"/> <see langword="null"/> → alle
    /// Einträge des Konto-Clients (bei <c>Authentication</c>; Token abgelaufen/gewechselt →
    /// <c>ViewerId</c> potenziell falsch), sonst die Einträge des (Client, Host)-Paars (bei
    /// <c>Permission</c> → <c>CanWrite</c> wird frisch geladen).</summary>
    private void EvictResolutions(IGitHubEventApiClient client, string? host)
    {
        var clientPrefix = RuntimeHelpers.GetHashCode(client) + "|";
        var prefix = host is null ? clientPrefix : clientPrefix + host.ToLowerInvariant() + "|";
        foreach (var key in _resolutionCache.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _resolutionCache.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public async Task<ScmEventBatch> ReadEventsAsync(ScmEventReadRequest request, CancellationToken ct = default)
    {
        ValidateReference(request.Reference);
        var repository = request.Reference.Repository;
        var client = ClientFor(repository.Account);
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
            var data = await GraphAsync(client, repository.Host,
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
                var data = await GraphAsync(client, repository.Host,
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
            if (request.SinceUtc is { } since) endpoint += SinceParameter(since);
            var comments = await RestPagesAsync(client, repository.Host, endpoint, ct);
            foreach (var comment in comments)
            {
                // Always obtain body and editor in the same GraphQL snapshot, including same-second edits.
                var nodeId = Required(comment, "node_id");
                var type = request.ContentKind == ScmContentKind.ReviewComment ? "PullRequestReviewComment" : "IssueComment";
                var data = await GraphAsync(client, repository.Host, "query($id:ID!){node(id:$id){... on " + type + "{" + TextFields + "}}}",
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
        var client = ClientFor(repository.Account);
        await CollectReferencesAsync(client, repository, ScmReferenceKind.Issue, "issues", "[OPEN]", null, references, ct);
        await CollectReferencesAsync(client, repository, ScmReferenceKind.PullRequest, "pullRequests", "[OPEN]", null, references, ct);
        if (sinceUtc is { } since)
        {
            await CollectReferencesAsync(client, repository, ScmReferenceKind.Issue, "issues", "[CLOSED]", since, references, ct);
            await CollectReferencesAsync(client, repository, ScmReferenceKind.PullRequest, "pullRequests", "[CLOSED,MERGED]", since, references, ct);
        }
        return references;
    }

    /// <inheritdoc />
    public async Task<Issue?> ReadIssueAsync(ScmEventReference reference, CancellationToken ct = default)
    {
        ValidateReference(reference);
        if (reference.Kind != ScmReferenceKind.Issue) throw Permanent("Issue-Anlagedaten benötigen eine Issue-Referenz.");
        var repository = reference.Repository;
        var element = await EventApiAsync(ClientFor(repository.Account), repository.Host, "GET", $"{RepositoryEndpoint(repository)}/issues/{reference.Number}", null, false, ct);
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
            IsClosed = string.Equals(String(element, "state"), "closed", StringComparison.OrdinalIgnoreCase)
        };
    }

    /// <inheritdoc />
    public async Task<PullRequest?> ReadPullRequestAsync(ScmEventReference reference, CancellationToken ct = default)
    {
        ValidateReference(reference);
        if (reference.Kind != ScmReferenceKind.PullRequest) throw Permanent("Pull-Request-Anlagedaten benötigen eine Pull-Request-Referenz.");
        var repository = reference.Repository;
        var repositoryId = PullRequestRepositoryId.Normalize(PullRequestProvider.GitHub, $"{repository.Owner}/{repository.Name}");
        var element = await EventApiAsync(ClientFor(repository.Account), repository.Host, "GET", $"{RepositoryEndpoint(repository)}/pulls/{reference.Number}", null, false, ct);
        if (element.ValueKind != JsonValueKind.Object) throw Permanent("Unvollständige GitHub-Antwort.");
        var pullRequest = ParsePullRequest(element, repositoryId);
        return pullRequest is not null && pullRequest.Number == reference.Number ? pullRequest : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScmNotification>> EnumerateNotificationsAsync(string host,
        DateTimeOffset? sinceUtc, CancellationToken ct = default)
    {
        host = GitHubHttpApiClient.ValidateHost(host);
        var endpoint = "notifications?all=true&participating=true&per_page=100";
        if (sinceUtc is { } since)
            endpoint += SinceParameter(since);
        var threads = await RestPagesAsync(ClientFor(ScmAccountMode.SeparateAccount), host, endpoint, ct);
        var notifications = new List<ScmNotification>();
        foreach (var thread in threads)
        {
            var subject = Object(thread, "subject");
            var kind = String(subject, "type") switch
            {
                "Issue" => ScmReferenceKind.Issue,
                "PullRequest" => ScmReferenceKind.PullRequest,
                _ => (ScmReferenceKind?)null
            };
            if (kind is null) continue; // Releases, CheckSuites, Discussions u. a. sind keine Referenzen.
            var subjectUrl = String(subject, "url");
            var number = subjectUrl is not null && int.TryParse(subjectUrl.TrimEnd('/').Split('/')[^1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            if (number <= 0) continue;
            var repository = Object(thread, "repository");
            notifications.Add(new(Required(thread, "id"), Required(repository, "node_id"), host,
                Required(Object(repository, "owner"), "login"), Required(repository, "name"), kind.Value, number,
                String(thread, "reason") ?? string.Empty, RequiredDate(thread, "updated_at")));
        }
        return notifications;
    }

    /// <inheritdoc />
    /// <remarks>Der Issue-Events-Endpunkt deckt Issues und Pull Requests gleichermaßen ab und
    /// liefert den Zuweisenden (<c>actor</c>) je Ereignis — die Notifications-API enthält ihn
    /// nicht. Maßgeblich ist das jüngste Zuweisungsereignis mit <c>assignee</c> = Viewer; ein
    /// jüngeres <c>unassigned</c> auf den Viewer hebt die Zuweisung auf (<see langword="null"/>).</remarks>
    public async Task<ScmAssignment?> GetLatestViewerAssignmentAsync(ScmEventReference reference, CancellationToken ct = default)
    {
        ValidateReference(reference);
        var repository = reference.Repository;
        var events = await RestPagesAsync(ClientFor(repository.Account), repository.Host,
            $"{RepositoryEndpoint(repository)}/issues/{reference.Number}/events?per_page=100", ct);
        JsonElement? effective = null;
        var effectiveAt = DateTimeOffset.MinValue;
        long effectiveId = -1;
        foreach (var element in events)
        {
            if (String(element, "event") is not ("assigned" or "unassigned")) continue;
            var assignee = Object(element, "assignee");
            if (!IsViewer(repository, String(assignee, "node_id"), String(assignee, "login"))) continue;
            var at = RequiredDate(element, "created_at");
            var id = long.TryParse(String(element, "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
            if (effective is null || at > effectiveAt || (at == effectiveAt && id > effectiveId))
            {
                effective = element;
                effectiveAt = at;
                effectiveId = id;
            }
        }
        if (effective is not { } latest || String(latest, "event") != "assigned") return null;
        var actor = Object(latest, "actor").ValueKind == JsonValueKind.Object
            ? Object(latest, "actor") : Object(latest, "assigner");
        var effectiveAssignee = Object(latest, "assignee");
        return new ScmAssignment(Required(latest, "node_id"), effectiveAt,
            String(actor, "node_id"), String(actor, "login"),
            String(effectiveAssignee, "node_id"), String(effectiveAssignee, "login"));
    }

    /// <summary>Liest eine paginierte Referenzliste; jüngste Aktivität zuerst, Abbruch am <paramref name="cutoff"/>.</summary>
    private async Task CollectReferencesAsync(IGitHubEventApiClient client, ScmEventRepository repository, ScmReferenceKind kind, string field,
        string states, DateTimeOffset? cutoff, List<ScmEventReference> target, CancellationToken ct)
    {
        var variables = new Dictionary<string, object?> { ["owner"] = repository.Owner, ["name"] = repository.Name };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            variables["cursor"] = cursor;
            var data = await GraphAsync(client, repository.Host,
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
        // Marker-Vorfilter nur für Kommentar-Arten: Eigene marker-tragende Kommentare sind stets
        // eigene Outputs und in keinem Pfad legitimierbar. Beschreibungen dagegen können über den
        // Assign-Auslöser legitimiert werden — den sieht das Plugin im ScmEventReadRequest nicht,
        // deshalb entscheidet der hostseitige Validator (Texttrigger-Pfad lehnt sie weiterhin ab).
        if (request.ContentKind.IsCommentKind()
            && IsViewer(repo, authorId, authorLogin) && body.Contains("<!-- softwareschmiede:", StringComparison.OrdinalIgnoreCase)) return;
        var reactionGroups = Object(node, "reactionGroups");
        if (reactionGroups.ValueKind != JsonValueKind.Array) throw Permanent("Unvollständige GitHub-Antwort.");
        var viewerReactions = reactionGroups.EnumerateArray()
            .Where(group => Boolean(group, "viewerHasReacted"))
            .Select(group => Required(group, "content")).ToArray();
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
            $"https://{repo.Host}/{repo.Owner}/{repo.Name}/{referencePath}/{request.Reference.Number}{fragment}", DateTimeOffset.UtcNow,
            viewerReactions.Length > 0 ? viewerReactions : null));
    }

    private Task<List<JsonElement>> RestPagesAsync(IGitHubEventApiClient client, string host, string endpoint, CancellationToken ct)
        => RestPagesAsync(client, host, endpoint,
            page => page.ValueKind == JsonValueKind.Array ? page.EnumerateArray() : null, ct);

    /// <summary>Liest paginierte Elemente einer Objekt-Antwort mit verschachtelter Liste
    /// (<c>{total_count, &lt;arrayProperty&gt;:[]}</c>) mit derselben Link-/Host-Semantik wie <see cref="RestPagesAsync(IGitHubEventApiClient, string, string, CancellationToken)"/>.</summary>
    private Task<List<JsonElement>> RestObjectPagesAsync(IGitHubEventApiClient client, string host, string endpoint,
        string arrayProperty, CancellationToken ct)
        => RestPagesAsync(client, host, endpoint,
            page => Object(page, arrayProperty).ValueKind == JsonValueKind.Array ? page.GetProperty(arrayProperty).EnumerateArray() : null, ct);

    /// <summary>Liest einen paginierten REST-Endpunkt vollständig: Zyklusprüfung, Link-Header-
    /// Pagination mit <see cref="ValidateNextPage"/> und einheitliche Fehlersemantik;
    /// <paramref name="extractElements"/> liefert die Elemente einer Seite oder <see langword="null"/>.</summary>
    private async Task<List<JsonElement>> RestPagesAsync(IGitHubEventApiClient client, string host, string endpoint,
        Func<JsonElement, IEnumerable<JsonElement>?> extractElements, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var initialPath = endpoint.Split('?')[0];
        string? next = endpoint;
        while (next is not null)
        {
            if (!visited.Add(next)) throw Permanent("GitHub lieferte eine zyklische Seitennavigation.");
            var response = await SendEventApiAsync(client, new(host, "GET", next), false, ct);
            var page = ParseJson(response.Body);
            if (extractElements(page) is not { } elements) throw Permanent("Ungültige GitHub-Seitenantwort.");
            items.AddRange(elements);
            var link = Header(response, "Link");
            var match = Regex.Match(link ?? string.Empty, "<([^>]+)>;\\s*rel=\"next\"", RegexOptions.CultureInvariant);
            next = match.Success ? ValidateNextPage(host, initialPath, match.Groups[1].Value) : null;
        }
        return items;
    }

    private async Task<JsonElement> EventApiAsync(IGitHubEventApiClient client, string host, string method, string endpoint, object? body, bool mutation, CancellationToken ct)
    {
        var result = await SendEventApiAsync(client, new(host, method, endpoint, body is null ? null : JsonSerializer.Serialize(body)), mutation, ct);
        try { return ParseJson(result.Body); }
        catch (ScmProviderException) when (mutation)
        { throw new ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Antwort nach Schreibversuch nicht auswertbar.", outcomeUncertain: true); }
    }

    private async Task<JsonElement> GraphAsync(IGitHubEventApiClient client, string host, string query, object variables, bool mutation, CancellationToken ct)
    {
        var result = await EventApiAsync(client, host, "POST", "graphql", new { query, variables }, mutation, ct);
        if (result.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var types = errors.EnumerateArray().Select(e => String(e, "type") ?? String(Object(e, "extensions"), "code")).ToArray();
            var kind = types.Contains("RATE_LIMITED") ? ScmProviderFailureKind.RateLimit
                : types.Contains("NOT_FOUND") ? ScmProviderFailureKind.Permanent
                : types.Contains("FORBIDDEN") ? ScmProviderFailureKind.Permission
                : types.Contains("UNAUTHORIZED") ? ScmProviderFailureKind.Authentication : ScmProviderFailureKind.Temporary;
            // Dieselbe Eviction-Semantik wie auf HTTP-Ebene (siehe SendEventApiAsync):
            // Konto-Token ungültig → alle Einträge des Clients; Berechtigung → (Client, Host).
            if (kind == ScmProviderFailureKind.Authentication) EvictResolutions(client, null);
            else if (kind == ScmProviderFailureKind.Permission) EvictResolutions(client, host);
            if (kind == ScmProviderFailureKind.RateLimit)
            {
                // GraphQL-Fehler tragen keinen Server-Reset: kurze lokale Sperre (ein Poll-Intervall)
                // verhindert Feuer frei, ohne das Kontingent länger als nötig zu blockieren.
                var lockedUntil = _timeProvider.GetUtcNow().Add(RateLimitLockFallbackDuration);
                ArmRateLimitGate((client, host.ToLowerInvariant(), true), lockedUntil);
                throw new ScmProviderException(kind, EventFailureMessage(kind, "GitHub-GraphQL-Abfrage"), lockedUntil, mutation);
            }
            throw new ScmProviderException(kind, EventFailureMessage(kind, "GitHub-GraphQL-Abfrage"), outcomeUncertain: mutation);
        }
        var data = Object(result, "data");
        if (data.ValueKind != JsonValueKind.Object) throw new ScmProviderException(ScmProviderFailureKind.Temporary,
            "Unvollständige GitHub-GraphQL-Antwort.", outcomeUncertain: mutation);
        return data;
    }

    /// <summary>Wählt den API-Transport zum Konto. Token-Grenze (bewusste Asymmetrie): Das
    /// Konto-Routing gilt ausschließlich für die Ereignispfade (<see cref="IScmEventSource"/>,
    /// <see cref="IScmEventResponder"/>, <see cref="IScmNotificationSource"/>,
    /// <see cref="IScmAgentAccess"/>). Nicht-Ereignis-APIs (Repository-/Issue-/PR-Listen,
    /// PR-Status, Workflow-Runs, Alerts, Templates, <c>CheckHealthAsync</c>) sowie die verbleibenden
    /// gh-/git-Schreib- und Arbeitskopien-Pfade (<c>issue create</c>, <c>pr create</c>, <c>pr merge</c>,
    /// Clone/Push/Pull, <c>git ls-remote</c>) laufen bewusst weiter unter dem Benutzer-Token —
    /// siehe <c>_eventApiClient</c>/<c>GetGhEnvironment</c>/<c>GetGitEnvironment</c>.</summary>
    private IGitHubEventApiClient ClientFor(ScmAccountMode account)
    {
        if (account == ScmAccountMode.SeparateAccount
            && string.IsNullOrEmpty(_credentialStore.GetCredential(GitHubAgentTokenCredentialKey)))
            throw new ScmProviderException(ScmProviderFailureKind.Authentication,
                "Für den Kontomodus „Separates Konto“ fehlt der Personal Access Token des separaten Kontos. Öffnen Sie Einstellungen → Plugins → GitHub → Authentifizierung und tragen Sie den Token ein. Bis dahin werden keine Benachrichtigungen eingelesen.");
        return account == ScmAccountMode.SeparateAccount ? _agentEventApiClient : _eventApiClient;
    }

    /// <summary>Wählt den Credential-Key zum Konto; dieselbe Token-Grenze wie bei
    /// <see cref="ClientFor"/> — Nicht-Ereignis-Pfade nutzen weiterhin <c>GitHubTokenCredentialKey</c>.</summary>
    private static string CredentialKeyFor(ScmAccountMode account)
        => account == ScmAccountMode.SeparateAccount ? GitHubAgentTokenCredentialKey : GitHubTokenCredentialKey;

    /// <summary>Zentraler Ereignis-Transport: prüft vor jedem Call die Rate-Limit-Sperrtabelle
    /// <see cref="_rateLimitGates"/> (Fail-fast ohne HTTP-Request, solange der Sperrzeitpunkt nicht
    /// erreicht ist) und wendet auf jeden typisierten Fehler — ob direkt vom Transport geworfen
    /// oder von <see cref="EvaluateResponse"/> aus einer Antwort klassifiziert — dieselbe
    /// Behandlung an (<see cref="OnTransportFailure"/>): <see cref="ScmProviderFailureKind.RateLimit"/>
    /// trägt eine Sperre ein — mit <c>RetryAfterUtc</c> oder, wenn der Server keinen
    /// Reset-Zeitpunkt liefert, mit der lokalen <see cref="RateLimitLockFallbackDuration"/> —;
    /// <c>Authentication</c>- (Konto) bzw. <c>Permission</c>-Fehler (Client+Host) verwerfen
    /// gecachte Auflösungen. Der Host im Gate-Schlüssel ist normalisiert, wie in Cache-Schlüssel
    /// und Eviction.</summary>
    private async Task<GitHubEventApiResponse> SendEventApiAsync(IGitHubEventApiClient client, GitHubEventApiRequest request, bool mutation, CancellationToken ct)
    {
        var gateKey = (client, request.Host.ToLowerInvariant(), request.Endpoint.Split('?')[0].Equals("graphql", StringComparison.Ordinal));
        if (_rateLimitGates.TryGetValue(gateKey, out var lockedUntil) && lockedUntil > _timeProvider.GetUtcNow())
            throw new ScmProviderException(ScmProviderFailureKind.RateLimit,
                "GitHub-Kontingent erschöpft; nächster Versuch nach dem Rate-Limit-Reset.", lockedUntil);
        GitHubEventApiResponse response;
        try { response = await client.SendAsync(request, ct); }
        catch (ScmProviderException ex) when (mutation && ex.FailureKind == ScmProviderFailureKind.Temporary && !ex.OutcomeUncertain)
        { throw new ScmProviderException(ex.FailureKind, ex.Message, ex.RetryAfterUtc, true); }
        catch (ScmProviderException ex)
        {
            // Direkt vom Transport typisiert geworfene Fehler durchlaufen dieselbe
            // Sperr-/Eviction-Behandlung wie aus Antworten klassifizierte Fehler.
            OnTransportFailure(ex, client, gateKey, request.Host);
            throw;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TimeoutException || (mutation && ex is OperationCanceledException))
        { throw new ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Verbindung unterbrochen.", outcomeUncertain: mutation); }
        try { return EvaluateResponse(response, $"HTTP {response.StatusCode}, {request.Host}/{request.Endpoint.Split('?')[0]}", mutation); }
        catch (ScmProviderException ex)
        {
            OnTransportFailure(ex, client, gateKey, request.Host);
            throw;
        }
    }

    /// <summary>Sperr-/Eviction-Behandlung typisierter Fehler aus beiden Fehlerpfaden:
    /// <c>RateLimit</c> armiert das Gate — Rate-Limit ohne Server-Reset armiert die lokale
    /// Fallback-Sperre, ohne die jeder Folgeaufruf desselben Gate-Schlüssels weiter auf den
    /// Transport ginge —; <c>Authentication</c> verwirft alle Auflösungen des Kontos,
    /// <c>Permission</c> die des (Client, Host)-Paars.</summary>
    private void OnTransportFailure(ScmProviderException ex, IGitHubEventApiClient client,
        (IGitHubEventApiClient Client, string Host, bool GraphQl) gateKey, string host)
    {
        if (ex.FailureKind == ScmProviderFailureKind.RateLimit)
            ArmRateLimitGate(gateKey, ex.RetryAfterUtc ?? _timeProvider.GetUtcNow().Add(RateLimitLockFallbackDuration));
        else if (ex.FailureKind == ScmProviderFailureKind.Authentication)
            EvictResolutions(client, null);
        else if (ex.FailureKind == ScmProviderFailureKind.Permission)
            EvictResolutions(client, host);
    }

    /// <summary>Trägt einen Sperr-bis-Zeitpunkt in <see cref="_rateLimitGates"/> ein; bei
    /// konkurrierenden Einträgen desselben Schlüssels gewinnt der späteste Zeitpunkt, damit
    /// eine kürzere Sperre eine bereits längere nicht überschreibt.</summary>
    private void ArmRateLimitGate((IGitHubEventApiClient Client, string Host, bool GraphQl) gateKey, DateTimeOffset lockedUntil)
        => _rateLimitGates.AddOrUpdate(gateKey, lockedUntil, (_, existing) => existing > lockedUntil ? existing : lockedUntil);

    /// <summary>Einheitliche Status→<see cref="ScmProviderFailureKind"/>-Matrix und Backoff-Auswertung;
    /// <paramref name="uncertain"/> gilt ausschließlich für sicher wiederholbare Aufrufe (Mutationen),
    /// niemals für den Attachment-Upload-Teilschritt.</summary>
    private GitHubEventApiResponse EvaluateResponse(GitHubEventApiResponse response, string detail, bool uncertain)
    {
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
            retry = _timeProvider.GetUtcNow().AddSeconds(Math.Clamp(seconds, 0, 86400));
        else if (DateTimeOffset.TryParse(Header(response, "Retry-After"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryDate)) retry = retryDate;
        else if (long.TryParse(Header(response, "X-RateLimit-Reset"), out var reset) && reset is >= 0 and <= 253402300799)
            retry = DateTimeOffset.FromUnixTimeSeconds(reset);
        throw new ScmProviderException(kind, EventFailureMessage(kind, detail), retry,
            uncertain && response.StatusCode >= 500);
    }

    private static string EventFailureMessage(ScmProviderFailureKind kind, string detail) => kind switch
    {
        ScmProviderFailureKind.Authentication => $"GitHub-Anmeldung abgelaufen oder ungültig. Öffnen Sie Einstellungen → Plugins → GitHub. Erneuern Sie unter Authentifizierung den Personal Access Token und speichern Sie. Klicken Sie anschließend auf Quellen erneut prüfen bzw. beim Ereignis auf Erneut prüfen. Bis dahin werden neue Anfragen dieser Quelle nicht eingelesen ({detail}).",
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
        var scp = ScpSshUrlPattern.Match(value);
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
