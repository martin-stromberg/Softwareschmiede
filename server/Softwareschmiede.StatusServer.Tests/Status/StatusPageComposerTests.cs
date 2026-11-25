using FluentAssertions;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.GitHub;
using Softwareschmiede.StatusServer.Status;
using Softwareschmiede.StatusServer.Tests.TestSupport;

namespace Softwareschmiede.StatusServer.Tests.Status;

/// <summary>Tests für <see cref="StatusPageComposer"/> (HTML-Rendern aller Sektionen).</summary>
public sealed class StatusPageComposerTests
{
    private static StatusPageComposer CreateSut(TestTimeProvider time) => new(time);

    private static InstanceSnapshotView OnlineInstance()
        => new(
            "inst-1234567890",
            "Martin-PC",
            new StatusSnapshot
            {
                InstanceId = "inst-1234567890",
                InstanceName = "Martin-PC",
                CreatedUtc = new DateTimeOffset(2026, 1, 1, 11, 59, 0, TimeSpan.Zero),
                ProtocolVersion = RelayProtocolVersion.Current,
                AppVersion = "2.4.0",
                RunningAutomationsCount = 1,
                WorkItems =
                [
                    new StatusSnapshotWorkItem
                    {
                        Title = "Laufende Aufgabe",
                        Status = "Started",
                        RunStatus = "Running",
                        IssueNumber = 479,
                        IssueUrl = "https://github.com/o/r/issues/479",
                        RepositoryName = "repo",
                        HasActiveRun = true,
                        PullRequests = [new StatusSnapshotPullRequest(12, "PR-Titel", "https://github.com/o/r/pull/12", "Open", "o/r")],
                    },
                    new StatusSnapshotWorkItem
                    {
                        Title = "Wartende Aufgabe",
                        Status = "Started",
                        RunStatus = "WaitingForInput",
                        IssueNumber = 478,
                        IssueUrl = "https://github.com/o/r/issues/478",
                    },
                ],
                ScmEvents =
                [
                    new StatusSnapshotScmEvent
                    {
                        Status = "Verarbeitet",
                        Reason = "Run gestartet",
                        ReceivedUtc = new DateTimeOffset(2026, 1, 1, 11, 58, 0, TimeSpan.Zero),
                        ReferenceDisplay = "IssueComment #478",
                        Url = "https://github.com/o/r/issues/478#comment-1",
                    },
                ],
            },
            Connected: true,
            RegisteredUtc: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            LastSeenUtc: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            IsStale: false);

    private static IReadOnlyList<GitHubRepositoryStatus> GitHubRepos()
        =>
        [
            new GitHubRepositoryStatus(
                "owner/repo",
                "https://github.com/owner/repo",
                [new GitHubPullRequestView(482, "Backmerge", "https://github.com/owner/repo/pull/482")],
                [new GitHubReleaseView("Hotfix", "v2.3.1", "https://github.com/owner/repo/releases/v2.3.1",
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))],
                5,
                new DateTimeOffset(2026, 1, 1, 11, 59, 30, TimeSpan.Zero),
                null,
                null),
        ];

    /// <summary>Die Seite enthält alle Kernsektionen, Meta-Refresh und keine Aktionsbuttons (read-only).</summary>
    [Fact]
    public void Render_OnlineInstance_ContainsAllSectionsAndNoButtons()
    {
        var time = new TestTimeProvider();
        var html = CreateSut(time).Render([OnlineInstance()], GitHubRepos(), 15);

        html.Should().Contain("meta http-equiv=\"refresh\" content=\"15\"");
        html.Should().Contain("name=\"viewport\"");
        html.Should().Contain("Laufende Aufgaben");
        html.Should().Contain("Wartet auf Anwender");
        html.Should().Contain("Letzte SCM-Ereignisse");
        html.Should().Contain("Offene Pull Requests");
        html.Should().Contain("Letzte Releases");
        html.Should().Contain("badge online");
        html.Should().Contain("Martin-PC");
        html.Should().NotContain("<button");
    }

    /// <summary>Referenzierte Objekte werden als klickbare GitHub-Links gerendert.</summary>
    [Fact]
    public void Render_References_RenderedAsGitHubLinks()
    {
        var time = new TestTimeProvider();
        var html = CreateSut(time).Render([OnlineInstance()], GitHubRepos(), 15);

        html.Should().Contain("href=\"https://github.com/o/r/issues/479\"");
        html.Should().Contain("href=\"https://github.com/o/r/pull/12\"");
        html.Should().Contain("href=\"https://github.com/owner/repo/pull/482\"");
        html.Should().Contain("href=\"https://github.com/owner/repo/releases/v2.3.1\"");
        html.Should().Contain("href=\"https://github.com/o/r/issues/478#comment-1\"");
    }

    /// <summary>Eine getrennte Instanz wird mit „offline seit" markiert, GitHub-Sektionen bleiben normal.</summary>
    [Fact]
    public void Render_DisconnectedInstance_ShowsOfflineNote()
    {
        var time = new TestTimeProvider();
        var offline = OnlineInstance() with { Connected = false, IsStale = true };
        var html = CreateSut(time).Render([offline], GitHubRepos(), 15);

        html.Should().Contain("offline seit");
        html.Should().Contain("badge offline");
        html.Should().Contain("Offene Pull Requests");
    }

    /// <summary>Ohne Instanzdaten wird ein ehrlicher „keine Daten"-Zustand gerendert.</summary>
    [Fact]
    public void Render_NoInstances_ShowsEmptyState()
    {
        var time = new TestTimeProvider();
        var html = CreateSut(time).Render([], [], 15);

        html.Should().Contain("Keine App-Instanz hat bisher Daten geliefert");
        html.Should().Contain("badge offline");
    }

    /// <summary>Ein GitHub-Abruffehler wird mit Zeitstempel angezeigt.</summary>
    [Fact]
    public void Render_GitHubError_ShowsErrorWithTimestamp()
    {
        var time = new TestTimeProvider();
        var repos = new List<GitHubRepositoryStatus>
        {
            new("owner/repo", "https://github.com/owner/repo", [], [], null, null,
                "403 rate limit", new DateTimeOffset(2026, 1, 1, 11, 59, 0, TimeSpan.Zero)),
        };
        var html = CreateSut(time).Render([OnlineInstance()], repos, 15);

        html.Should().Contain("GitHub-Abruf f");
        html.Should().Contain("403 rate limit");
    }

    /// <summary>HTML-relevante Zeichen in Benutzerinhalten werden escaped.</summary>
    [Fact]
    public void Render_UserContent_IsHtmlEncoded()
    {
        var time = new TestTimeProvider();
        var instance = OnlineInstance();
        var snapshot = instance.Snapshot! with
        {
            WorkItems =
            [
                new StatusSnapshotWorkItem
                {
                    Title = "<script>alert(1)</script>",
                    Status = "Started",
                },
            ],
        };
        var html = CreateSut(time).Render([instance with { Snapshot = snapshot }], [], 15);

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
    }

    /// <summary>Null-Listen im Snapshot (WorkItems, ScmEvents, PullRequests) werden als leere
    /// Sektionen gerendert, statt das Rendern mit einer NRE abzubrechen (M4).</summary>
    [Fact]
    public void Render_NullCollections_RenderEmptySections()
    {
        var time = new TestTimeProvider();
        var instance = OnlineInstance();
        var snapshot = instance.Snapshot! with
        {
            // Absichtlich null trotz non-nullable Deklaration: Kabel-/Deserialisierungsfälle (M4).
            WorkItems = null!,
            ScmEvents = null!,
        };
        var html = CreateSut(time).Render([instance with { Snapshot = snapshot }], [], 15);

        html.Should().Contain("Laufende Aufgaben");
        html.Should().Contain("Wartet auf Anwender");
        html.Should().Contain("Letzte SCM-Ereignisse");
    }

    /// <summary>Nicht-http(s)-URLs werden nicht als href ausgegeben, sondern nur als Text —
    /// Whitelist gegen javascript:-/data:-Links (M5).</summary>
    [Fact]
    public void Render_NonHttpScheme_UrlRenderedAsTextOnly()
    {
        var time = new TestTimeProvider();
        var instance = OnlineInstance();
        var snapshot = instance.Snapshot! with
        {
            WorkItems =
            [
                new StatusSnapshotWorkItem
                {
                    Title = "Aufgabe",
                    Status = "Started",
                    RunStatus = "Running",
                    IssueNumber = 1,
                    IssueUrl = "javascript:alert(1)",
                    HasActiveRun = true,
                },
            ],
            ScmEvents =
            [
                new StatusSnapshotScmEvent
                {
                    Status = "Verarbeitet",
                    ReceivedUtc = new DateTimeOffset(2026, 1, 1, 11, 58, 0, TimeSpan.Zero),
                    ReferenceDisplay = "Ereignis",
                    Url = "file:///etc/passwd",
                },
            ],
        };
        var html = CreateSut(time).Render([instance with { Snapshot = snapshot }], [], 15);

        html.Should().NotContain("href=\"javascript:");
        html.Should().NotContain("href=\"file:");
        html.Should().Contain("#1");
        html.Should().Contain("Ereignis");
    }

    /// <summary>Alle drei App-Sektionen tragen die Instanz-Kennung, damit bei mehreren
    /// Instanzen klar ist, zu welcher App die Tabelle gehört (Usability-Fix).</summary>
    [Fact]
    public void Render_AppSections_CarryInstanceTag()
    {
        var time = new TestTimeProvider();
        var html = CreateSut(time).Render([OnlineInstance()], [], 15);

        html.Should().Contain("Laufende Aufgaben ");
        html.Should().Contain("Wartet auf Anwender ");
        html.Should().Contain("Letzte SCM-Ereignisse ");
        // Drei Sektionen, jeweils mit Instanz-Tag im h2.
        var count = html.Split("class=\"inst\">Instanz ").Length - 1;
        count.Should().BeGreaterThanOrEqualTo(3);
    }

    /// <summary>Das eingebettete CSS enthält keine nowrap-/Fixbreiten-Regeln, die auf
    /// Mobilgeräten den Umbruch verhindern (Usability-Fix).</summary>
    [Fact]
    public void Render_Css_AllowsWrappingForMobile()
    {
        var time = new TestTimeProvider();
        var html = CreateSut(time).Render([OnlineInstance()], [], 15);

        html.Should().NotContain("white-space: nowrap");
        html.Should().NotContain("white-space:nowrap");
        html.Should().Contain("name=\"viewport\"");
    }
}
