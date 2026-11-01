using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Plugins;

/// <summary>Regressionstests zum serverseitigen <c>since</c>-Filter von GitHub: Am Tag der US-Zeitumstellung
/// (z. B. 2026-11-01) vergleicht GitHub Zeitpunkte aus der wiederholten Stunde (09:00–10:00Z) um eine Stunde
/// zu früh. Eine Erwähnung um 09:47Z fiel so bei einem 2-Minuten-Fenster ab 09:46Z aus Inbox und
/// Kommentarabruf heraus. Das Plugin muss das Fenster deshalb mit Sicherheitsabstand an GitHub übergeben.</summary>
public sealed class GitHubSinceWindowTests
{
    private static readonly DateTimeOffset Since = DateTimeOffset.Parse("2026-11-01T09:46:00Z");
    private static readonly ScmEventRepository Repository = new("Softwareschmiede.GitHub", "github.com", "R_1",
        "owner", "repo", "U_1", "U_1", "owner", true);

    /// <summary>Die Inbox fragt GitHub mindestens zwei Stunden vor dem lokalen Fenster ab, damit eine von
    /// GitHub um eine Stunde verschoben gefilterte Notification nicht verloren geht.</summary>
    [Fact]
    public async Task EnumerateNotifications_SinceWindow_IncludesSafetyMarginForGitHubDstShift()
    {
        var agent = new FakeGitHubEventApi();
        agent.AddJson("notifications", "[]");
        var store = new Mock<ICredentialStore>();
        store.Setup(c => c.GetCredential("Softwareschmiede.GitHub.AgentToken")).Returns("bot-token");
        var plugin = new GitHubPlugin(Mock.Of<ICliRunner>(), store.Object, NullLogger<GitHubPlugin>.Instance, new FakeGitHubEventApi(), agent);

        await ((IScmNotificationSource)plugin).EnumerateNotificationsAsync("github.com", Since);

        RequestedSince(agent.Requests.Should().ContainSingle().Subject.Endpoint)
            .Should().BeOnOrBefore(Since.AddHours(-2));
    }

    /// <summary>Auch der Kommentarabruf einer Referenz übergibt das Fenster mit Sicherheitsabstand.</summary>
    [Fact]
    public async Task ReadIssueComments_SinceWindow_IncludesSafetyMarginForGitHubDstShift()
    {
        var api = new FakeGitHubEventApi();
        api.Add("comments?", Array.Empty<object>());
        var plugin = new GitHubPlugin(Mock.Of<ICliRunner>(), Mock.Of<ICredentialStore>(), NullLogger<GitHubPlugin>.Instance, api);

        await plugin.ReadEventsAsync(new ScmEventReadRequest(new ScmEventReference(Repository, ScmReferenceKind.Issue, 27),
            ScmContentKind.IssueComment, Since, Since.AddMinutes(2)));

        RequestedSince(api.Requests.Should().ContainSingle().Subject.Endpoint)
            .Should().BeOnOrBefore(Since.AddHours(-2));
    }

    private static DateTimeOffset RequestedSince(string endpoint)
    {
        var value = endpoint.Split('?', 2)[1].Split('&').Single(x => x.StartsWith("since=", StringComparison.Ordinal))["since=".Length..];
        return DateTimeOffset.Parse(Uri.UnescapeDataString(value), System.Globalization.CultureInfo.InvariantCulture);
    }
}
