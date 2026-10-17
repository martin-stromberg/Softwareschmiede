using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Reale Providerlogik mit ausschließlich ausgetauschtem API-Transport.</summary>
public sealed class GitHubEventSourceTests
{
    private static readonly DateTimeOffset Created = DateTimeOffset.Parse("2026-10-01T10:00:00Z");
    private static readonly ScmEventRepository Repository = new("Softwareschmiede.GitHub", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", true);
    private static readonly ScmEventReference Issue = new(Repository, ScmReferenceKind.Issue, 7);
    private static readonly ScmEventReference PullRequest = new(Repository, ScmReferenceKind.PullRequest, 8);

    /// <summary>Identitäten benutzen dieselben Node-IDs wie die GraphQL-Autoren.</summary>
    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("git@github.com:owner/repo.git")]
    [InlineData("ssh://git@github.com/owner/repo.git")]
    [InlineData("owner/repo")]
    public async Task RepositoryContext_UsesStableIdsAndPermissions(string remote)
    {
        var api = new FakeApi();
        Identity(api);
        var result = await Plugin(api).GetEventRepositoryAsync(new(Guid.NewGuid(), Repository.ProviderPrefix, remote));
        Assert.Equal(Repository, result);
        Assert.All(api.Requests, request => Assert.Equal("GET", request.Method));
    }

    /// <summary>Alle REST-Seiten und der aktuelle GraphQL-Text werden vor Rückgabe vollständig gelesen.</summary>
    [Fact]
    public async Task Comments_ReadAllPages_AndBodyEditorFromSameVersion()
    {
        var api = new FakeApi();
        api.Add("issues/7/comments?per_page=100", new[] { new { id = 11, node_id = "C_11", body = "old" } },
            new() { ["Link"] = "<https://api.github.com/repos/owner/repo/issues/7/comments?per_page=100&page=2>; rel=\"next\"" });
        api.Add("page=2", new[] { new { id = 12, node_id = "C_12" } });
        api.AddGraph(new { node = Node("C_11", "$Softwareschmiede edited", Created.AddMinutes(1), "U_2", "intruder") });
        api.AddGraph(new { node = Node("C_12", "$softwareschmiede second") });
        var result = await Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.IssueComment));
        Assert.Equal(2, result.Observations.Count);
        var edited = Assert.Single(result.Observations, x => x.RemoteNodeId == "C_11");
        Assert.Equal("$Softwareschmiede edited", edited.Body);
        Assert.Equal("U_2", edited.EditorId);
        Assert.Equal("U_1", edited.AuthorId);
        Assert.Equal(Created.AddMinutes(1), edited.Revision);
        Assert.Equal("https://github.com/owner/repo/issues/7#issuecomment-11", edited.Url);
        Assert.Contains("editor", api.Requests[2].JsonBody);
        api.AssertComplete();
    }

    /// <summary>Ein Fehler auf Folgeseite liefert niemals einen erfolgreich checkpointbaren Teilbatch.</summary>
    [Fact]
    public async Task FailedSecondPage_ThrowsWithoutGraphOrPartialBatch()
    {
        var api = new FakeApi();
        api.Add("comments?", new[] { new { id = 11, node_id = "C_11" } },
            new() { ["Link"] = "<https://api.github.com/repos/owner/repo/issues/7/comments?page=2>; rel=\"next\"" });
        api.AddStatus("page=2", 503);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.IssueComment)));
        Assert.Equal(ScmProviderFailureKind.Temporary, error.FailureKind);
        Assert.Equal(2, api.Requests.Count);
    }

    /// <summary>Fremde Paginationziele erhalten keinen weiteren Transportaufruf.</summary>
    [Theory]
    [InlineData("https://evil.example/repos/owner/repo/issues/7/comments?page=2")]
    [InlineData("https://api.github.com/repos/other/repo/issues/7/comments?page=2")]
    [InlineData("http://api.github.com/repos/owner/repo/issues/7/comments?page=2")]
    public async Task Pagination_RejectsForeignTargets(string next)
    {
        var api = new FakeApi();
        api.Add("comments?", Array.Empty<object>(), new() { ["Link"] = $"<{next}>; rel=\"next\"" });
        await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.IssueComment)));
        Assert.Single(api.Requests);
    }

    /// <summary>Neue Metadatenzeit verändert den Deduplizierungsschlüssel nicht; A→B→A bleibt erkennbar.</summary>
    [Fact]
    public async Task Description_VersionsDistinguishTextAndEdits_NotMetadata()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issue = Node("I_7", "A") } });
        api.AddGraph(new { repository = new { issue = Node("I_7", "A", updated: Created.AddMinutes(1)) } });
        api.AddGraph(new { repository = new { issue = Node("I_7", "B", Created.AddMinutes(2)) } });
        api.AddGraph(new { repository = new { issue = Node("I_7", "A", Created.AddMinutes(3)) } });
        var plugin = Plugin(api);
        var observations = new List<ScmEventObservation>();
        for (var index = 0; index < 4; index++) observations.Add(Assert.Single((await plugin.ReadEventsAsync(Read(Issue, ScmContentKind.Description))).Observations));
        Assert.Equal(observations[0].EventKey, observations[1].EventKey);
        Assert.NotEqual(observations[0].EventKey, observations[3].EventKey);
        Assert.NotEqual(observations[2].BodyHash, observations[3].BodyHash);
        Assert.Single(observations.Select(x => x.ContentKey).Distinct());
    }

    /// <summary>Editorlos bearbeiteter Inhalt wird nicht mit dem ursprünglichen Autor als Editor versehen.</summary>
    [Fact]
    public async Task MissingEditor_RemainsMissing_AndSameVersionCanResolveLater()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issue = Node("I_7", "$Softwareschmiede", Created.AddMinutes(1), null, null) } });
        api.AddGraph(new { repository = new { issue = Node("I_7", "$Softwareschmiede", Created.AddMinutes(1), "U_1", "owner") } });
        var plugin = Plugin(api);
        var first = Assert.Single((await plugin.ReadEventsAsync(Read(Issue, ScmContentKind.Description))).Observations);
        var second = Assert.Single((await plugin.ReadEventsAsync(Read(Issue, ScmContentKind.Description) with { SinceUtc = Created.AddHours(1) })).Observations);
        Assert.Null(first.EditorId);
        Assert.Equal("U_1", second.EditorId);
        Assert.Equal(first.EventKey, second.EventKey);
    }

    /// <summary>Texte nach Abfragebeginn gehören in den nächsten Batch.</summary>
    [Fact]
    public async Task Watermark_ExcludesNewerVersion()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issue = Node("I_7", "new", Created.AddHours(2)) } });
        var batch = await Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.Description));
        Assert.Empty(batch.Observations);
    }

    /// <summary>Veröffentlichte Reviewtexte werden paginiert; Pending bleibt ausgeschlossen.</summary>
    [Fact]
    public async Task Reviews_PaginateAndExcludePending()
    {
        var api = new FakeApi();
        api.AddGraph(Reviews(new[] { Review("R_20", "PENDING", null), Review("R_21", "APPROVED", Created) }, true, "cursor-2"));
        api.AddGraph(Reviews(new[] { Review("R_22", "COMMENTED", Created) }, false, null));
        var batch = await Plugin(api).ReadEventsAsync(Read(PullRequest, ScmContentKind.PullRequestReview));
        Assert.Equal(new[] { "R_21", "R_22" }, batch.Observations.Select(x => x.RemoteNodeId));
        Assert.Contains("cursor-2", api.Requests[1].JsonBody);
        Assert.All(batch.Observations, x => Assert.Equal(ScmContentKind.PullRequestReview, x.ContentKind));
    }

    /// <summary>PR-Unterhaltung und Zeilenkommentare haben eigene Endpunkte und Schlüssel.</summary>
    [Fact]
    public async Task PullRequestCommentKinds_UseSeparateNamespaces()
    {
        var api = new FakeApi();
        api.Add("issues/8/comments?", new[] { new { id = 1, node_id = "C" } });
        api.AddGraph(new { node = Node("C", "text") });
        api.Add("pulls/8/comments?", new[] { new { id = 1, node_id = "C" } });
        api.AddGraph(new { node = Node("C", "text") });
        var plugin = Plugin(api);
        var conversation = Assert.Single((await plugin.ReadEventsAsync(Read(PullRequest, ScmContentKind.IssueComment))).Observations);
        var line = Assert.Single((await plugin.ReadEventsAsync(Read(PullRequest, ScmContentKind.ReviewComment))).Observations);
        Assert.NotEqual(conversation.ContentKey, line.ContentKey);
        Assert.Contains("PullRequestReviewComment", api.Requests[3].JsonBody);
    }

    /// <summary>Eigene markierte Rückantworten lösen auch mit Trigger keinen Folgeauftrag aus.</summary>
    [Fact]
    public async Task OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted()
    {
        var api = new FakeApi();
        api.Add("comments?", new[] { new { id = 1, node_id = "C_1" }, new { id = 2, node_id = "C_2" } });
        api.AddGraph(new { node = Node("C_1", "$Softwareschmiede\n<!-- softwareschmiede:action:abc -->") });
        var foreign = Node("C_2", "$Softwareschmiede\n<!-- softwareschmiede:action:abc -->");
        foreign["author"] = new { id = "U_2", login = "foreign" };
        api.AddGraph(new { node = foreign });
        var batch = await Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.IssueComment));
        Assert.Equal("C_2", Assert.Single(batch.Observations).RemoteNodeId);
    }

    /// <summary>Provider, Host, Repository, Typ und Nummer sind Bestandteil des Identitätsschlüssels.</summary>
    [Fact]
    public async Task EventKey_SeparatesEveryOriginDimension()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issue = Node("I", "same") } });
        var item = Assert.Single((await Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.Description))).Observations);
        var variants = new[]
        {
            item,
            item with { Reference = Issue with { Number = 9 } },
            item with { Reference = PullRequest with { Number = 7 } },
            item with { Reference = Issue with { Repository = Repository with { Host = "other.example" } } },
            item with { Reference = Issue with { Repository = Repository with { RepositoryId = "R_2" } } },
            item with { Reference = Issue with { Repository = Repository with { ProviderPrefix = "Other" } } }
        };
        Assert.Equal(variants.Length, variants.Select(x => x.EventKey).Distinct().Count());
    }

    /// <summary>HTTP-Fehler sind typisiert und enthalten weder Remotetext noch Secrets.</summary>
    [Theory]
    [InlineData(401, ScmProviderFailureKind.Authentication)]
    [InlineData(403, ScmProviderFailureKind.Permission)]
    [InlineData(429, ScmProviderFailureKind.RateLimit)]
    [InlineData(503, ScmProviderFailureKind.Temporary)]
    [InlineData(404, ScmProviderFailureKind.Permanent)]
    public async Task Failures_AreTypedAndSanitized(int status, ScmProviderFailureKind expected)
    {
        var api = new FakeApi();
        api.AddStatus("user", status, new() { ["Retry-After"] = "90" });
        var before = DateTimeOffset.UtcNow;
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).GetEventRepositoryAsync(new(Guid.NewGuid(), Repository.ProviderPrefix, "owner/repo")));
        Assert.Equal(status == 403 ? ScmProviderFailureKind.RateLimit : expected, error.FailureKind);
        Assert.DoesNotContain("secret", error.Message);
        Assert.True(error.RetryAfterUtc >= before.AddSeconds(89));
        Assert.False(error.OutcomeUncertain);
    }

    /// <summary>Authentication recovery names the saved token and the actual settings path.</summary>
    [Theory]
    [InlineData(401, "Erneuern")]
    [InlineData(403, "Lese- und Schreibrechte")]
    public async Task AccessFailures_ExplainTokenSettingsAndRecovery(int status, string instruction)
    {
        var api = new FakeApi();
        api.AddStatus("user", status);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).GetEventRepositoryAsync(new(Guid.NewGuid(), Repository.ProviderPrefix, "owner/repo")));
        Assert.Contains("Einstellungen → Plugins → GitHub", error.Message);
        Assert.Contains("Authentifizierung", error.Message);
        Assert.Contains("Personal Access Token", error.Message);
        Assert.Contains(instruction, error.Message);
        Assert.Contains("speichern", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quellen erneut prüfen", error.Message);
    }

    /// <summary>Eine partielle GraphQL-Antwort darf nicht als vollständige Quelle checkpointbar sein.</summary>
    [Fact]
    public async Task GraphErrors_RejectPartialData()
    {
        var api = new FakeApi();
        api.Add("graphql", new { data = new { repository = new { issue = Node("I", "text") } }, errors = new[] { new { type = "FORBIDDEN", message = "secret" } } });
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ReadEventsAsync(Read(Issue, ScmContentKind.Description)));
        Assert.Equal(ScmProviderFailureKind.Permission, error.FailureKind);
        Assert.DoesNotContain("secret", error.Message);
    }

    /// <summary>Vorhandene Viewer-Reaktion auf Folgeseite verhindert einen zweiten POST.</summary>
    [Fact]
    public async Task ExistingReaction_IsReconciledAcrossAllPages()
    {
        var api = new FakeApi();
        Identity(api);
        api.Add("issues/comments/42/reactions?", new[] { new { id = 1, content = "eyes", user = new { node_id = "U_2", login = "other" } } },
            new() { ["Link"] = "<https://api.github.com/repos/owner/repo/issues/comments/42/reactions?page=2>; rel=\"next\"" });
        api.Add("page=2", new[] { new { id = 2, content = "eyes", user = new { node_id = "U_1", login = "OWNER" } } });
        var result = await Plugin(api).ExecuteResponseAsync(new(Issue, "reaction-1", ScmResponseKind.Reaction,
            Reaction: "eyes", ContentKind: ScmContentKind.IssueComment, RemoteCommentId: "42"));
        Assert.True(result.AlreadyExisted);
        Assert.Equal("2", result.RemoteId);
        Assert.All(api.Requests, x => Assert.Equal("GET", x.Method));
    }

    /// <summary>Ungewisser Kommentar-POST wird danach per Marker wiedergefunden, nicht wiederholt.</summary>
    [Fact]
    public async Task CommentTimeout_ReportsUncertain_AndReconcilesMarker()
    {
        var request = new ScmResponseRequest(Issue, "event-1-waiting", ScmResponseKind.Comment, "Wartet auf Auswahl.");
        var api = new FakeApi();
        Identity(api);
        api.Add("comments?", Array.Empty<object>());
        api.AddFailure("issues/7/comments", new TimeoutException("secret"));
        var plugin = Plugin(api);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => plugin.ExecuteResponseAsync(request));
        Assert.True(error.OutcomeUncertain);
        Assert.DoesNotContain("secret", error.Message);
        var posted = api.Requests.Last();
        Assert.Equal("POST", posted.Method);
        Assert.Contains(request.Marker, JsonDocument.Parse(posted.JsonBody!).RootElement.GetProperty("body").GetString());
        api.Add("comments?", new[] { new { id = 23, body = "Wartet\n" + request.Marker, user = new { node_id = "U_1", login = "owner" } } });
        Assert.Equal("23", (await plugin.ReconcileResponseAsync(request))!.RemoteId);
        Assert.Single(api.Requests, x => x.Method == "POST");
    }

    /// <summary>403 nach Antwortversuch ist sicher fehlgeschlagen und darf nach Rechtekorrektur erneut geprüft werden.</summary>
    [Fact]
    public async Task ReplyPermissionFailure_IsNotUncertain()
    {
        var api = new FakeApi();
        Identity(api);
        api.Add("comments?", Array.Empty<object>());
        api.AddStatus("issues/7/comments", 403);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ExecuteResponseAsync(new(Issue, "action-1", ScmResponseKind.Comment, "Antwort")));
        Assert.Equal(ScmProviderFailureKind.Permission, error.FailureKind);
        Assert.False(error.OutcomeUncertain);
    }

    /// <summary>Ein anderer authentifizierter Nutzer darf eine gespeicherte Aktion nicht übernehmen.</summary>
    [Fact]
    public async Task ChangedViewer_BlocksResponseBeforePost()
    {
        var api = new FakeApi();
        Identity(api, "U_2");
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ExecuteResponseAsync(new(Issue, "action-1", ScmResponseKind.Comment, "Antwort")));
        Assert.Equal(ScmProviderFailureKind.Permission, error.FailureKind);
        Assert.All(api.Requests, x => Assert.Equal("GET", x.Method));
    }

    /// <summary>PR-Antworten bleiben am gespeicherten Repository und werden per Marker abgeglichen.</summary>
    [Fact]
    public async Task PullRequestResponse_PreservesOriginAndUsesJsonBody()
    {
        var api = new FakeApi();
        Identity(api);
        api.Add("pulls?state=all", Array.Empty<object>());
        api.Add("repos/owner/repo/pulls", new { id = 88, html_url = "https://github.com/owner/repo/pull/22" });
        var request = new ScmResponseRequest(Issue, "pr-1", ScmResponseKind.PullRequest, "$Softwareschmiede\n\u001b\0\"text\"", Title: "Resultat", HeadBranch: "feature/a", BaseBranch: "main");
        var result = await Plugin(api).ExecuteResponseAsync(request);
        Assert.Equal("88", result.RemoteId);
        using var body = JsonDocument.Parse(api.Requests.Last().JsonBody!);
        Assert.Equal("feature/a", body.RootElement.GetProperty("head").GetString());
        Assert.Equal("main", body.RootElement.GetProperty("base").GetString());
        Assert.StartsWith(request.Body!, body.RootElement.GetProperty("body").GetString());
        Assert.Equal("repos/owner/repo/pulls", api.Requests.Last().Endpoint);
    }

    /// <summary>gh erhält JSON ausschließlich in einer kurzlebigen Inputdatei und Tokens nur im Environment.</summary>
    [Fact]
    public async Task GhTransport_UsesArgumentListAndJsonFile_NotBodyOrTokenArguments()
    {
        var runner = new Mock<ICliRunner>();
        const string json = "{\"body\":\"quotes \\\" \\n \\u001B \\u0000\"}";
        string? inputPath = null;
        runner.Setup(x => x.RunAsync("gh", It.IsAny<IEnumerable<string>>(), null,
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IEnumerable<string> args, string? _, IDictionary<string, string>? environment, CancellationToken _) =>
            {
                var array = args.ToArray();
                Assert.DoesNotContain("token-secret", string.Join(" ", array));
                Assert.DoesNotContain(json, array);
                Assert.Equal("token-secret", environment!["GH_TOKEN"]);
                inputPath = array[Array.IndexOf(array, "--input") + 1];
                Assert.Equal(json, File.ReadAllText(inputPath));
                return Task.FromResult(new CliResult(0, "HTTP/2.0 201 Created\r\nContent-Type: application/json\r\n\r\n{\"id\":42}", ""));
            });
        var client = new GhEventApiClient(runner.Object, () => new Dictionary<string, string> { ["GH_TOKEN"] = "token-secret" });
        var result = await client.SendAsync(new("github.com", "POST", "repos/owner/repo/issues/7/comments", json));
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("{\"id\":42}", result.Body);
        Assert.NotNull(inputPath);
        Assert.False(File.Exists(inputPath));
    }

    private static GitHubPlugin Plugin(FakeApi api) => new(Mock.Of<ICliRunner>(), Mock.Of<ICredentialStore>(), NullLogger<GitHubPlugin>.Instance, api);
    private static ScmEventReadRequest Read(ScmEventReference reference, ScmContentKind kind) => new(reference, kind, null, Created.AddHours(1));
    private static void Identity(FakeApi api, string viewer = "U_1")
    {
        api.Add("user", new { node_id = viewer, login = "owner" });
        api.Add("repos/owner/repo", new { node_id = "R_1", name = "repo", owner = new { node_id = "U_1", login = "owner" }, permissions = new { push = true } });
    }
    private static Dictionary<string, object?> Node(string id, string body, DateTimeOffset? edited = null,
        string? editorId = "U_1", string? editorLogin = "owner", DateTimeOffset? updated = null) => new()
        {
            ["id"] = id,
            ["body"] = body,
            ["createdAt"] = Created,
            ["updatedAt"] = updated ?? edited ?? Created,
            ["lastEditedAt"] = edited,
            ["author"] = new { id = "U_1", login = "owner" },
            ["editor"] = edited is not null && editorId is not null ? new { id = editorId, login = editorLogin } : null,
            ["url"] = "https://evil.example/ignored"
        };
    private static Dictionary<string, object?> Review(string id, string state, DateTimeOffset? submitted)
    {
        var result = Node(id, "$Softwareschmiede review");
        result["state"] = state;
        result["submittedAt"] = submitted;
        result["fullDatabaseId"] = id[2..];
        return result;
    }
    private static object Reviews(object nodes, bool hasNextPage, string? endCursor) =>
        new { repository = new { pullRequest = new { reviews = new { nodes, pageInfo = new { hasNextPage, endCursor } } } } };

    private sealed class FakeApi : IGitHubEventApiClient
    {
        private readonly Queue<(string Endpoint, GitHubEventApiResponse? Response, Exception? Error)> _steps = new();
        internal List<GitHubEventApiRequest> Requests { get; } = [];
        internal void Add(string endpoint, object body, Dictionary<string, string>? headers = null) =>
            _steps.Enqueue((endpoint, new(200, JsonSerializer.Serialize(body), headers), null));
        internal void AddGraph(object data) => Add("graphql", new { data });
        internal void AddStatus(string endpoint, int status, Dictionary<string, string>? headers = null) =>
            _steps.Enqueue((endpoint, new(status, "{\"message\":\"secret\"}", headers), null));
        internal void AddFailure(string endpoint, Exception error) => _steps.Enqueue((endpoint, null, error));
        internal void AssertComplete() => Assert.Empty(_steps);
        public Task<GitHubEventApiResponse> SendAsync(GitHubEventApiRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            Assert.NotEmpty(_steps);
            var step = _steps.Dequeue();
            Assert.Contains(step.Endpoint, request.Endpoint);
            return step.Error is null ? Task.FromResult(step.Response!) : Task.FromException<GitHubEventApiResponse>(step.Error);
        }
    }
}
