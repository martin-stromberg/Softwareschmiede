using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Transport-Mock-Tests des Zuweisungs-Pfads (<see cref="IScmAssignmentSource"/>):
/// Issue-Events-Endpunkt, Jüngst-Regel, Aufhebung, Viewer-Filter und Agent-Client-Grenze.</summary>
public sealed class GitHubAssignmentSourceTests
{
    private static readonly ScmEventRepository Repository = new("Softwareschmiede.GitHub", "github.com",
        "R_1", "owner", "repo", "U_1", "BOT", "scm-bot", true, ScmAccountMode.SeparateAccount);

    private static ScmEventReference Reference(int number = 7, ScmReferenceKind kind = ScmReferenceKind.Issue)
        => new(Repository, kind, number);

    private static GitHubPlugin Plugin(FakeGitHubEventApi api, Mock<ICredentialStore>? store = null, FakeGitHubEventApi? agent = null)
        => new(Mock.Of<ICliRunner>(), (store ?? new Mock<ICredentialStore>()).Object, NullLogger<GitHubPlugin>.Instance, api, agent,
            netrcPath: GitHubPluginTests.CreateMissingNetrcPath());

    private static Mock<ICredentialStore> StoreWithAgentToken()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(c => c.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns("bot-token");
        return store;
    }

    /// <summary>Der jüngste <c>assigned</c>-Eintrag mit assignee = Viewer liefert Zuweisungsidentität,
    /// Zuweisenden (<c>actor</c>) und Empfänger; andere Ereignisse und Fremdzuweisungen werden ignoriert.</summary>
    [Fact]
    public async Task GetViewerAssignment_MapsLatestViewerAssignedEvent()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events", """
            [
              { "id": 1, "node_id": "EV_FOREIGN", "event": "assigned",
                "actor": { "node_id": "U_9", "login": "fremd" },
                "assignee": { "node_id": "U_9", "login": "fremd" },
                "created_at": "2026-10-01T09:00:00Z" },
              { "id": 2, "node_id": "EV_LABEL", "event": "labeled",
                "actor": { "node_id": "U_1", "login": "owner" },
                "created_at": "2026-10-01T09:30:00Z" },
              { "id": 3, "node_id": "EV_3", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var assignment = await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken);

        assignment.Should().Be(new ScmAssignment("EV_3", DateTimeOffset.Parse("2026-10-01T10:00:00Z"),
            "U_1", "owner", "BOT", "scm-bot"));
        agent.Requests.Should().ContainSingle()
            .Which.Endpoint.Should().Contain("repos/owner/repo/issues/7/events?per_page=100");
    }

    /// <summary>Bei mehreren Viewer-Zuweisungen gewinnt die jüngste; gleicher Zeitstempel wird
    /// über die höhere Event-ID aufgelöst. Ohne <c>actor</c> gilt das <c>assigner</c>-Feld.</summary>
    [Fact]
    public async Task GetViewerAssignment_LatestEventAndAssignerFallback()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events", """
            [
              { "id": 5, "node_id": "EV_OLD", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" },
              { "id": 6, "node_id": "EV_NEW", "event": "assigned",
                "assigner": { "node_id": "U_2", "login": "mitarbeiter" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var assignment = await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken);

        assignment.Should().Be(new ScmAssignment("EV_NEW", DateTimeOffset.Parse("2026-10-01T10:00:00Z"),
            "U_2", "mitarbeiter", "BOT", "scm-bot"));
    }

    /// <summary>Ein jüngeres <c>unassigned</c> auf den Viewer hebt die Zuweisung wirksam auf;
    /// umgekehrt gewinnt eine spätere erneute Zuweisung.</summary>
    [Fact]
    public async Task GetViewerAssignment_UnassignedLatest_ReturnsNull_Reassigned_ReturnsEvent()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events", """
            [
              { "id": 1, "node_id": "EV_ASSIGN", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" },
              { "id": 2, "node_id": "EV_UNASSIGN", "event": "unassigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T11:00:00Z" }
            ]
            """);
        agent.AddJson("issues/8/events", """
            [
              { "id": 1, "node_id": "EV_UNASSIGN", "event": "unassigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" },
              { "id": 2, "node_id": "EV_REASSIGN", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T11:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);
        var source = (IScmAssignmentSource)plugin;

        (await source.GetLatestViewerAssignmentAsync(Reference(7), TestContext.Current.CancellationToken)).Should().BeNull();
        var reassigned = await source.GetLatestViewerAssignmentAsync(Reference(8), TestContext.Current.CancellationToken);
        reassigned.Should().NotBeNull();
        reassigned!.EventNodeId.Should().Be("EV_REASSIGN");
    }

    /// <summary>Ohne Viewer-bezogenes Zuweisungsereignis bleibt die Zuweisung unbestimmt.</summary>
    [Fact]
    public async Task GetViewerAssignment_NoViewerEvent_ReturnsNull()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events", """
            [
              { "id": 1, "node_id": "EV_1", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "U_9", "login": "kollege" },
                "created_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var assignment = await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken);

        assignment.Should().BeNull();
    }

    /// <summary>Pull-Request-Referenzen nutzen denselben Issue-Events-Endpunkt.</summary>
    [Fact]
    public async Task GetViewerAssignment_PullRequest_UsesIssueEventsEndpoint()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/9/events", """
            [
              { "id": 1, "node_id": "EV_PR", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var assignment = await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(
            Reference(9, ScmReferenceKind.PullRequest), TestContext.Current.CancellationToken);

        assignment.Should().NotBeNull();
        assignment!.EventNodeId.Should().Be("EV_PR");
        agent.Requests.Should().ContainSingle()
            .Which.Endpoint.Should().Contain("issues/9/events");
    }

    /// <summary>Der Events-Endpunkt wird über den Link-Header paginiert.</summary>
    [Fact]
    public async Task GetViewerAssignment_ReadsAllPages()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events?", """
            [
              { "id": 1, "node_id": "EV_1", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T10:00:00Z" }
            ]
            """,
            new() { ["Link"] = "<https://api.github.com/repos/owner/repo/issues/7/events?per_page=100&page=2>; rel=\"next\"" });
        agent.AddJson("page=2", """
            [
              { "id": 2, "node_id": "EV_2", "event": "assigned",
                "actor": { "node_id": "U_1", "login": "owner" },
                "assignee": { "node_id": "BOT", "login": "scm-bot" },
                "created_at": "2026-10-01T11:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var assignment = await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken);

        assignment!.EventNodeId.Should().Be("EV_2");
        agent.Requests.Should().HaveCount(2);
    }

    /// <summary>Die Zuweisungsabfrage läuft über den Agent-Client — der Benutzer-Client wird nicht berührt.</summary>
    [Fact]
    public async Task GetViewerAssignment_UsesAgentClientOnly()
    {
        var user = new FakeGitHubEventApi();
        var agent = new FakeGitHubEventApi();
        agent.AddJson("issues/7/events", "[]");
        var plugin = Plugin(user, StoreWithAgentToken(), agent);

        await ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken);

        user.Requests.Should().BeEmpty();
        agent.Requests.Should().ContainSingle();
    }

    /// <summary>Transportfehler des Events-Endpunkts bleiben typisierte Providerfehler.</summary>
    [Theory]
    [InlineData(404, ScmProviderFailureKind.Permanent)]
    [InlineData(403, ScmProviderFailureKind.Permission)]
    [InlineData(429, ScmProviderFailureKind.RateLimit)]
    [InlineData(503, ScmProviderFailureKind.Temporary)]
    public async Task GetViewerAssignment_TypedFailures(int status, ScmProviderFailureKind kind)
    {
        var agent = new FakeGitHubEventApi();
        agent.AddStatus("issues/7/events", status);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => ((IScmAssignmentSource)plugin).GetLatestViewerAssignmentAsync(Reference(), TestContext.Current.CancellationToken));
        error.FailureKind.Should().Be(kind);
    }
}
