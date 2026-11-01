using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

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

    /// <summary>Repository-weite Erkennung listet offene Issues und Pull Requests über alle Seiten.</summary>
    [Fact]
    public async Task Enumeration_ReturnsOpenReferencesAcrossPages()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issues = Page(new[] { Ref(7), Ref(8) }, true, "c1") } });
        api.AddGraph(new { repository = new { issues = Page(new[] { Ref(9) }, false, null) } });
        api.AddGraph(new { repository = new { pullRequests = Page(new[] { Ref(3) }, false, null) } });
        var result = await Plugin(api).EnumerateReferencesAsync(Repository, null);
        Assert.Equal(new[]
        {
            new ScmEventReference(Repository, ScmReferenceKind.Issue, 7),
            new ScmEventReference(Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(Repository, ScmReferenceKind.Issue, 9),
            new ScmEventReference(Repository, ScmReferenceKind.PullRequest, 3)
        }, result);
        Assert.Equal(3, api.Requests.Count);
        Assert.All(api.Requests, request => Assert.Contains("states:[OPEN]", request.JsonBody));
        Assert.Contains("c1", api.Requests[1].JsonBody);
        api.AssertComplete();
    }

    /// <summary>Im Aktivitätsfenster ergänzen zuletzt bewegte geschlossene Referenzen die offene Menge.</summary>
    [Fact]
    public async Task Enumeration_SinceWindow_AddsOnlyRecentlyUpdatedClosedReferences()
    {
        var api = new FakeApi();
        api.AddGraph(new { repository = new { issues = Page(new[] { Ref(7) }, false, null) } });
        api.AddGraph(new { repository = new { pullRequests = Page(new[] { Ref(3) }, false, null) } });
        api.AddGraph(new { repository = new { issues = Page(new[] { Ref(5, Created), Ref(4, Created.AddHours(-1)) }, false, null) } });
        api.AddGraph(new { repository = new { pullRequests = Page(new[] { Ref(2, Created.AddMinutes(1)), Ref(1, Created.AddDays(-1)) }, false, null) } });
        var result = await Plugin(api).EnumerateReferencesAsync(Repository, Created);
        Assert.Equal(new[]
        {
            new ScmEventReference(Repository, ScmReferenceKind.Issue, 7),
            new ScmEventReference(Repository, ScmReferenceKind.PullRequest, 3),
            new ScmEventReference(Repository, ScmReferenceKind.Issue, 5),
            new ScmEventReference(Repository, ScmReferenceKind.PullRequest, 2)
        }, result);
        Assert.Contains("states:[CLOSED]", api.Requests[2].JsonBody);
        Assert.Contains("states:[CLOSED,MERGED]", api.Requests[3].JsonBody);
        api.AssertComplete();
    }

    /// <summary>Transportfehler der Enumeration bleiben typisierte Providerfehler für den Backoff-Pfad.</summary>
    [Fact]
    public async Task Enumeration_Failure_RemainsTypedProviderError()
    {
        var api = new FakeApi();
        api.AddStatus("graphql", 503);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).EnumerateReferencesAsync(Repository, null));
        Assert.Equal(ScmProviderFailureKind.Temporary, error.FailureKind);
    }

    /// <summary>Der HTTP-Transport sendet JSON als Request-Body und den Token nur im Authorization-Header.</summary>
    [Fact]
    public async Task HttpTransport_SendsJsonBodyAndBearerToken_InRequest()
    {
        const string json = "{\"body\":\"quotes \\\" \\n \\u001B \\u0000\"}";
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new DelegateHandler(async (request, _) =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.Created)
            { Content = new StringContent("{\"id\":42}", System.Text.Encoding.UTF8, "application/json") };
        });
        var client = new GitHubHttpApiClient(() => "token-secret", handler);
        var result = await client.SendAsync(new("github.com", "POST", "repos/owner/repo/issues/7/comments", json));
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("{\"id\":42}", result.Body);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("token-secret", captured.Headers.Authorization?.Parameter);
        Assert.Equal("application/vnd.github+json", captured.Headers.Accept.ToString());
        Assert.Equal("2022-11-28", captured.Headers.GetValues("X-GitHub-Api-Version").Single());
        Assert.Equal(json, capturedBody);
        Assert.DoesNotContain("token-secret", captured.RequestUri!.ToString());
    }

    /// <summary>Issue-Anlagedaten liefern Titel, Text, Labels, Milestone und kanonische URL.</summary>
    [Fact]
    public async Task ReadIssue_ReturnsCreationFields()
    {
        var api = new FakeApi();
        api.Add("issues/7", new
        {
            number = 7,
            title = "Fehler im Import",
            body = "Beschreibung",
            labels = new[] { new { name = "bug" }, new { name = "prio" } },
            milestone = new { title = "v1" },
            html_url = "https://github.com/owner/repo/issues/7"
        });
        var result = await Plugin(api).ReadIssueAsync(Issue);
        Assert.NotNull(result);
        Assert.Equal(7, result!.Nummer);
        Assert.Equal("Fehler im Import", result.Titel);
        Assert.Equal("Beschreibung", result.Body);
        Assert.Equal(new[] { "bug", "prio" }, result.Labels);
        Assert.Equal("v1", result.Milestone);
        Assert.Equal("https://github.com/owner/repo/issues/7", result.IssueUrl);
        api.AssertComplete();
    }

    /// <summary>Eine Pull-Request-Antwort am Issue-Endpunkt ist nicht anlegbar und liefert null.</summary>
    [Fact]
    public async Task ReadIssue_PullRequestResponse_ReturnsNull()
    {
        var api = new FakeApi();
        api.Add("issues/7", new { number = 7, title = "PR", pull_request = new { url = "x" } });
        Assert.Null(await Plugin(api).ReadIssueAsync(Issue));
        api.AssertComplete();
    }

    /// <summary>Pull-Request-Anlagedaten liefern Quell- und Ziel-Branch sowie Quell-Repository.</summary>
    [Fact]
    public async Task ReadPullRequest_ReturnsCheckoutFields()
    {
        var api = new FakeApi();
        api.Add("pulls/8", new
        {
            number = 8,
            title = "Feature",
            body = "PR-Text",
            html_url = "https://github.com/owner/repo/pull/8",
            state = "open",
            node_id = "PR_node",
            head = new { @ref = "feature/x", sha = "abc", repo = new { full_name = "owner/repo", clone_url = "https://github.com/owner/repo.git" } },
            @base = new { @ref = "main" }
        });
        var result = await Plugin(api).ReadPullRequestAsync(PullRequest);
        Assert.NotNull(result);
        Assert.Equal(8, result!.Nummer);
        Assert.Equal("feature/x", result.BranchName);
        Assert.Equal("main", result.TargetBranch);
        Assert.Equal("abc", result.HeadSha);
        Assert.Equal("owner/repo", result.RepositoryId);
        Assert.Equal("https://github.com/owner/repo.git", result.SourceRepositoryUrl);
        Assert.Equal("refs/heads/feature/x", result.SourceRef);
        api.AssertComplete();
    }

    /// <summary>Detail-Fehler bleiben typisierte Providerfehler.</summary>
    [Fact]
    public async Task ReadIssue_NotFound_RemainsTypedProviderError()
    {
        var api = new FakeApi();
        api.AddStatus("issues/7", 404);
        var error = await Assert.ThrowsAsync<ScmProviderException>(() => Plugin(api).ReadIssueAsync(Issue));
        Assert.Equal(ScmProviderFailureKind.Permanent, error.FailureKind);
    }

    /// <summary>Die Repository-Auflösung läuft im SeparatesKonto ausschließlich über den
    /// Agent-Client; der Benutzer-Client wird nicht berührt und das Ergebnis trägt das Konto.</summary>
    [Fact]
    public async Task GetEventRepositoryAsync_SeparatesKonto_UsesAgentToken()
    {
        var user = new FakeApi();
        var agent = new FakeApi();
        Identity(agent);
        var plugin = Plugin(user, StoreWithAgentToken(), agent);

        var result = await plugin.GetEventRepositoryAsync(
            new(Guid.NewGuid(), Repository.ProviderPrefix, "owner/repo", ScmKontoModus.SeparatesKonto));

        Assert.Equal(Repository with { Konto = ScmKontoModus.SeparatesKonto }, result);
        Assert.Empty(user.Requests);
        Assert.Equal(2, agent.Requests.Count);
    }

    /// <summary>Reads folgen dem Konto der Referenz: SeparatesKonto → Agent-Client,
    /// Benutzerkonto → Benutzer-Client.</summary>
    [Theory]
    [InlineData(ScmKontoModus.Benutzerkonto)]
    [InlineData(ScmKontoModus.SeparatesKonto)]
    public async Task ReadEventsAsync_RoutesByReferenceKonto(ScmKontoModus konto)
    {
        var user = new FakeApi();
        var agent = new FakeApi();
        var routed = konto == ScmKontoModus.SeparatesKonto ? agent : user;
        var idle = konto == ScmKontoModus.SeparatesKonto ? user : agent;
        routed.AddGraph(new { repository = new { issue = Node("I_7", "text") } });
        var plugin = Plugin(user, StoreWithAgentToken(), agent);
        var reference = Issue with { Repository = Repository with { Konto = konto } };

        var batch = await plugin.ReadEventsAsync(Read(reference, ScmContentKind.Description));

        Assert.Single(batch.Observations);
        Assert.Single(routed.Requests);
        Assert.Empty(idle.Requests);
    }

    /// <summary>Antworten folgen dem Konto der gespeicherten Referenz — inklusive
    /// Identitätsprüfung, Abgleichs-Reads und dem eigentlichen POST.</summary>
    [Theory]
    [InlineData(ScmKontoModus.Benutzerkonto)]
    [InlineData(ScmKontoModus.SeparatesKonto)]
    public async Task Response_RoutesByReferenceKonto(ScmKontoModus konto)
    {
        var user = new FakeApi();
        var agent = new FakeApi();
        var routed = konto == ScmKontoModus.SeparatesKonto ? agent : user;
        var idle = konto == ScmKontoModus.SeparatesKonto ? user : agent;
        Identity(routed);
        routed.Add("comments?", Array.Empty<object>());
        routed.Add("issues/7/comments", new { id = 99, html_url = "https://github.com/owner/repo/issues/7#issuecomment-99" });
        var plugin = Plugin(user, StoreWithAgentToken(), agent);
        var reference = Issue with { Repository = Repository with { Konto = konto } };

        var result = await plugin.ExecuteResponseAsync(new(reference, "action-1", ScmResponseKind.Comment, "Antwort"));

        Assert.Equal("99", result.RemoteId);
        Assert.Empty(idle.Requests);
        Assert.Contains(routed.Requests, x => x.Method == "POST");
        routed.AssertComplete();
    }

    private static GitHubPlugin Plugin(FakeApi api) => new(Mock.Of<ICliRunner>(), Mock.Of<ICredentialStore>(), NullLogger<GitHubPlugin>.Instance, api);
    private static GitHubPlugin Plugin(FakeApi api, ICredentialStore store, FakeApi agent)
        => new(Mock.Of<ICliRunner>(), store, NullLogger<GitHubPlugin>.Instance, api, agent);
    private static ICredentialStore StoreWithAgentToken()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(x => x.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns("bot-token");
        return store.Object;
    }
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
    private static Dictionary<string, object?> Ref(int number, DateTimeOffset? updated = null) => new()
    {
        ["number"] = number,
        ["updatedAt"] = updated ?? Created
    };
    private static object Page(object nodes, bool hasNextPage, string? endCursor) =>
        new { nodes, pageInfo = new { hasNextPage, endCursor } };

    private sealed class FakeApi : FakeGitHubEventApi;
}
