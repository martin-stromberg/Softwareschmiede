using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Options;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Plugins;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.TestSupport;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests zur Auflösungs-Budgetierung (Issue #442): echtes
/// <see cref="GitHubPlugin"/> mit <see cref="FakeGitHubEventApi"/>-Transport und dem echten
/// <see cref="ScmEventMonitoringService"/> über mehrere Poll-Läufe — nachweisbar über die
/// Request-Zählung der Warteschlangen-Fake.</summary>
public sealed class ScmResolutionBudgetTests
{
    /// <summary>Über ≥ 2 Poll-Läufe bleibt die Remote-Auflösung auf einen einzigen GraphQL-Call
    /// je Repository begrenzt (Cache-Treffer ab dem zweiten Lauf), während die repository-weite
    /// Enumeration weiter Remote-Calls erzeugt.</summary>
    [Fact]
    public async Task Monitor_ResolutionBudget_BoundedAcrossPolls()
    {
        await using var fixture = new ScmResolutionFixture();
        await fixture.InitializeAsync();
        await fixture.AddRepositoryAsync("https://github.com/scm-owner/scm-e2e");
        fixture.PlanResolution();
        fixture.PlanEnumeration(2);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        var firstRunCalls = fixture.Api.Requests.Count;

        fixture.PlanEnumeration(4);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var resolutionCalls = fixture.Api.Requests.Count(x => x.JsonBody?.Contains("viewerPermission", StringComparison.Ordinal) == true);
        Assert.Equal(1, resolutionCalls);
        Assert.True(fixture.Api.Requests.Count > firstRunCalls, "Der zweite Lauf muss weiter Remote-Calls erzeugen (Enumeration).");
        Assert.Equal(7, fixture.Api.Requests.Count);
        Assert.DoesNotContain(fixture.Api.Requests, x => x.Endpoint.StartsWith("user", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Api.Requests, x => x.Endpoint.StartsWith("repos/scm-owner/scm-e2e", StringComparison.Ordinal));
        fixture.Api.AssertComplete();
    }

    /// <summary>Zwei Alias-Formen (<c>https://…</c> und <c>owner/name</c>) konvergieren hostseitig
    /// auf eine kanonische Gruppe: ein Auflösungs-Checkpoint-Key, eine Remote-Auflösung — und die
    /// Enumeration läuft genau einmal über das zusammengelegte Repository.</summary>
    [Fact]
    public async Task Monitor_AliasForms_MergeToOneCheckpoint()
    {
        await using var fixture = new ScmResolutionFixture();
        await fixture.InitializeAsync();
        await fixture.AddRepositoryAsync("https://github.com/scm-owner/scm-e2e");
        await fixture.AddRepositoryAsync("scm-owner/scm-e2e");
        fixture.PlanResolution();
        fixture.PlanEnumeration(2);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var resolutionCalls = fixture.Api.Requests.Count(x => x.JsonBody?.Contains("viewerPermission", StringComparison.Ordinal) == true);
        Assert.Equal(1, resolutionCalls);
        Assert.Equal(3, fixture.Api.Requests.Count);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var enumeration = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("enumeration/")).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("enumeration/softwareschmiede.github/github.com/REPO", enumeration.SourceKey);
        Assert.Empty(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("repository/")).ToListAsync(TestContext.Current.CancellationToken));
        fixture.Api.AssertComplete();
    }

    /// <summary>Ein unter dem alten Roh-Alias-Schema verwaister Auflösungs-Checkpoint wird beim
    /// ersten kanonischen Lauf gelöscht — unabhängig von dessen Backoff-Zeitpunkt.</summary>
    [Fact]
    public async Task Monitor_LegacyAliasCheckpoints_ArePruned()
    {
        await using var fixture = new ScmResolutionFixture();
        await fixture.InitializeAsync();
        await fixture.AddRepositoryAsync("https://github.com/scm-owner/scm-e2e");
        var legacyKey = "repository/softwareschmiede.github/" + Uri.EscapeDataString("scm-owner/scm-e2e");
        using (var setup = fixture.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            setupDb.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = legacyKey,
                Failures = 3,
                ErrorCode = "RateLimit",
                NextAttemptUtc = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
            });
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        fixture.PlanResolution();
        fixture.PlanEnumeration(2);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.False(await db.ScmObservationCheckpoints.AnyAsync(x => x.SourceKey == legacyKey, TestContext.Current.CancellationToken),
            "Der verwaiste Roh-Alias-Checkpoint muss gelöscht sein.");
        // Der kanonische Auflösungsversuch lief trotz des Alt-Backoffs erfolgreich durch.
        var resolutionCalls = fixture.Api.Requests.Count(x => x.JsonBody?.Contains("viewerPermission", StringComparison.Ordinal) == true);
        Assert.Equal(1, resolutionCalls);
        fixture.Api.AssertComplete();
    }

    /// <summary>Service-Aufbau nach <c>ScmDispatchFixture</c>-Vorbild mit dem echten
    /// <see cref="GitHubPlugin"/> als einziger SCM-Quelle und <see cref="FakeGitHubEventApi"/> als
    /// Transport beider Konten.</summary>
    private sealed class ScmResolutionFixture : IAsyncDisposable
    {
        internal readonly string Root;
        internal readonly FakeGitHubEventApi Api = new();
        internal readonly GitHubPlugin Plugin;
        internal ServiceProvider Services { get; }
        private readonly string _netrcPath;

        internal ScmResolutionFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "scm-resolution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _netrcPath = Path.Combine(Root, "_netrc");
            Plugin = new GitHubPlugin(Mock.Of<ICliRunner>(), Mock.Of<ICredentialStore>(),
                NullLogger<GitHubPlugin>.Instance, Api, null, netrcPath: _netrcPath,
                resolutionOptions: new ScmResolutionCacheOptions { RepositoryResolutionCacheTtl = TimeSpan.FromMinutes(15) });
            var plugins = new Mock<IPluginManager>();
            plugins.Setup(x => x.GetSourceCodeManagementPlugins()).Returns([Plugin]);
            plugins.Setup(x => x.GetDefaultSourceCodeManagementPlugin()).Returns(Plugin);
            plugins.Setup(x => x.GetDevelopmentAutomationPlugins()).Returns(Array.Empty<IAiPlugin>());
            var directories = new Mock<IWorkingDirectoryResolver>();
            directories.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkingDirectoryResolutionResult(Root, false, "test", Root));
            var factory = new Mock<ITerminalSessionFactory>();
            var services = new ServiceCollection();
            services.AddScmTestServices(Path.Combine(Root, "events.db"), plugins.Object, Plugin,
                directories.Object, factory.Object, TimeProvider.System);
            Services = services.BuildServiceProvider();
        }

        internal async Task InitializeAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().Database.EnsureCreatedAsync();
            Services.GetRequiredService<ScmEventStartupGate>().Ready();
        }

        /// <summary>Aktives Projekt-Repository im angegebenen Alias-Format (Plugin-Zuordnung über
        /// den PluginPrefix des echten GitHub-Plugins).</summary>
        internal async Task AddRepositoryAsync(string repositoryUrl)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var project = new Project { Id = Guid.NewGuid(), Name = "SCM" };
            db.AddRange(project, new GitRepository
            {
                Id = Guid.NewGuid(),
                Project = project,
                RepositoryUrl = repositoryUrl,
                RepositoryName = "scm-e2e",
                PluginType = Plugin.PluginPrefix,
                Active = true
            });
            await db.SaveChangesAsync();
        }

        /// <summary>Plant die GraphQL-Auflösungsantwort (ein Call je Cache-Miss).</summary>
        internal void PlanResolution() => Api.AddGraph(new
        {
            viewer = new { id = "OWNER", login = "scm-owner" },
            repository = new
            {
                id = "REPO",
                databaseId = 424242,
                name = "scm-e2e",
                owner = new { id = "OWNER", login = "scm-owner" },
                viewerPermission = "WRITE"
            }
        });

        /// <summary>Plant <paramref name="pages"/> leere Enumerations-Seiten (jede Abfrage deckt
        /// issues- und pullRequests-Feld ab — die Reihenfolge der Queries ist transportagnostisch).</summary>
        internal void PlanEnumeration(int pages)
        {
            for (var index = 0; index < pages; index++)
                Api.AddGraph(new
                {
                    repository = new
                    {
                        issues = EmptyPage,
                        pullRequests = EmptyPage
                    }
                });
        }

        private static readonly object EmptyPage = new
        {
            nodes = Array.Empty<object>(),
            pageInfo = new { hasNextPage = false, endCursor = (string?)null }
        };

        internal ScmEventMonitoringService Monitor => Services.GetRequiredService<ScmEventMonitoringService>();

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            try { Directory.Delete(Root, true); } catch { /* best effort */ }
        }
    }
}
