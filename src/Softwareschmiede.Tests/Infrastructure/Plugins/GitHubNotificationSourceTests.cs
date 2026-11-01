using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Transport-Mock-Tests des Notification-Pfads (<see cref="IScmNotificationSource"/>).</summary>
public sealed class GitHubNotificationSourceTests
{
    private static GitHubPlugin Plugin(FakeGitHubEventApi api, Mock<ICredentialStore>? store = null, FakeGitHubEventApi? agent = null)
        => new(Mock.Of<ICliRunner>(), (store ?? new Mock<ICredentialStore>()).Object, NullLogger<GitHubPlugin>.Instance, api, agent);

    private static Mock<ICredentialStore> StoreWithAgentToken()
    {
        var store = new Mock<ICredentialStore>();
        store.Setup(c => c.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns("bot-token");
        return store;
    }

    /// <summary>Threads werden zu ScmNotification gemappt; die Nummer stammt aus dem letzten subject.url-Segment.</summary>
    [Fact]
    public async Task EnumerateNotifications_MapsIssueAndPullRequestThreads()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications", """
            [
              {
                "id": "T_1",
                "repository": { "node_id": "R_1", "name": "repo", "owner": { "login": "owner" } },
                "subject": { "type": "Issue", "url": "https://api.github.com/repos/owner/repo/issues/7" },
                "reason": "mention",
                "updated_at": "2026-10-01T10:00:00Z"
              },
              {
                "id": "T_2",
                "repository": { "node_id": "R_1", "name": "repo", "owner": { "login": "owner" } },
                "subject": { "type": "PullRequest", "url": "https://api.github.com/repos/owner/repo/pulls/8" },
                "reason": "review_requested",
                "updated_at": "2026-10-02T10:00:00Z"
              }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var result = await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null);

        result.Should().HaveCount(2);
        result[0].Should().Be(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "mention", DateTimeOffset.Parse("2026-10-01T10:00:00Z")));
        result[1].Kind.Should().Be(ScmReferenceKind.PullRequest);
        result[1].Number.Should().Be(8);
        result[1].Reason.Should().Be("review_requested");
        agent.Requests.Should().ContainSingle()
            .Which.Endpoint.Should().Contain("notifications?all=true&participating=true&per_page=100");
    }

    /// <summary>Andere Subject-Typen (Release, CheckSuite, Discussion) werden übersprungen.</summary>
    [Fact]
    public async Task EnumerateNotifications_SkipsNonReferenceSubjects()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications", """
            [
              { "id": "T_1", "repository": { "node_id": "R", "name": "r", "owner": { "login": "o" } },
                "subject": { "type": "Release", "url": "https://api.github.com/repos/o/r/releases/1" },
                "reason": "mention", "updated_at": "2026-10-01T10:00:00Z" },
              { "id": "T_2", "repository": { "node_id": "R", "name": "r", "owner": { "login": "o" } },
                "subject": { "type": "CheckSuite", "url": "https://api.github.com/repos/o/r/check-suites/2" },
                "reason": "mention", "updated_at": "2026-10-01T10:00:00Z" },
              { "id": "T_3", "repository": { "node_id": "R", "name": "r", "owner": { "login": "o" } },
                "subject": { "type": "Issue", "url": "https://api.github.com/repos/o/r/issues/3" },
                "reason": "mention", "updated_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var result = await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null);

        result.Should().ContainSingle().Which.Number.Should().Be(3);
    }

    /// <summary>Ein vorhandenes since-Fenster wird als ISO-Parameter weitergereicht.</summary>
    [Fact]
    public async Task EnumerateNotifications_PassesSinceParameter()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications", "[]");
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);
        var since = DateTimeOffset.Parse("2026-10-01T12:30:00Z");

        await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", since);

        agent.Requests.Should().ContainSingle()
            .Which.Endpoint.Should().Contain("since=" + Uri.EscapeDataString(since.ToUniversalTime().ToString("O")));
    }

    /// <summary>Die Inbox wird über den Link-Header paginiert.</summary>
    [Fact]
    public async Task EnumerateNotifications_ReadsAllPages()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications?", """
            [
              { "id": "T_1", "repository": { "node_id": "R", "name": "r", "owner": { "login": "o" } },
                "subject": { "type": "Issue", "url": "https://api.github.com/repos/o/r/issues/1" },
                "reason": "mention", "updated_at": "2026-10-01T10:00:00Z" }
            ]
            """,
            new() { ["Link"] = "<https://api.github.com/notifications?all=true&participating=true&per_page=100&page=2>; rel=\"next\"" });
        agent.AddJson("page=2", """
            [
              { "id": "T_2", "repository": { "node_id": "R", "name": "r", "owner": { "login": "o" } },
                "subject": { "type": "Issue", "url": "https://api.github.com/repos/o/r/issues/2" },
                "reason": "mention", "updated_at": "2026-10-01T10:00:00Z" }
            ]
            """);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var result = await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null);

        result.Select(x => x.ThreadId).Should().Equal("T_1", "T_2");
        agent.Requests.Should().HaveCount(2);
    }

    /// <summary>Transportfehler bleiben typisierte Providerfehler.</summary>
    [Fact]
    public async Task EnumerateNotifications_TypedFailures()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddStatus("notifications", 503);
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);

        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Temporary);
    }

    /// <summary>Die Inbox läuft ausschließlich über den Agent-Client — der Benutzer-Client wird nicht berührt.</summary>
    [Fact]
    public async Task EnumerateNotifications_UsesAgentClientOnly()
    {
        var user = new FakeGitHubEventApi();
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications", "[]");
        var plugin = Plugin(user, StoreWithAgentToken(), agent);

        await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null);

        user.Requests.Should().BeEmpty();
        agent.Requests.Should().ContainSingle();
    }

    /// <summary>Ohne AgentToken wird ein Authentication-Fehler mit Einstellungshinweis gemeldet, noch vor dem ersten Aufruf.</summary>
    [Fact]
    public async Task EnumerateNotifications_WithoutAgentToken_ThrowsAuthentication()
    {
        var agent = new FakeGitHubEventApi();
        var plugin = Plugin(new FakeGitHubEventApi(), new Mock<ICredentialStore>(), agent);

        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", null));

        error.FailureKind.Should().Be(ScmProviderFailureKind.Authentication);
        error.Message.Should().Contain("Einstellungen → Plugins → GitHub");
        error.Message.Should().Contain("Authentifizierung");
        agent.Requests.Should().BeEmpty();
    }

    /// <summary>Ein ungültiger Host wird als permanenter Providerfehler abgelehnt.</summary>
    [Fact]
    public async Task EnumerateNotifications_InvalidHost_ThrowsPermanent()
    {
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), new FakeGitHubEventApi());
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("not a host !!", null));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Permanent);
    }

    /// <summary>Ein Host mit UserInfo-/Port-/Pfad-/Query-/Fragment-Bestandteilen wird abgelehnt,
    /// bevor der Agent-Token an einen fremden Origin gehen könnte.</summary>
    [Theory]
    [InlineData("user@evil.com")]
    [InlineData("evil.com:444")]
    [InlineData("evil.com/pfad")]
    [InlineData("evil.com?x=1")]
    [InlineData("evil.com#frag")]
    public async Task EnumerateNotifications_HostWithForeignComponents_ThrowsPermanent(string host)
    {
        var agent = new FakeGitHubEventApi();
        var plugin = Plugin(new FakeGitHubEventApi(), StoreWithAgentToken(), agent);
        var error = await Assert.ThrowsAsync<ScmProviderException>(
            () => ((IScmNotificationSource)plugin).EnumerateNotificationsAsync(host, null));
        error.FailureKind.Should().Be(ScmProviderFailureKind.Permanent);
        agent.Requests.Should().BeEmpty();
    }
}
