using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests zur Repository-Auflösung: Ein für das überwachende Konto nicht
/// erreichbares Repository (HTTP 404, z. B. privat ohne Zugriff) darf die Überwachung der übrigen
/// Repositories nicht dauerhaft anhalten.</summary>
public sealed class ScmEventMonitoringServiceTests_RepositoryResolution
{
    private const string AccessibleUrl = "https://github.com/owner/repo";
    private const string InaccessibleUrl = "https://github.com/owner/private-repo";

    /// <summary>Im separaten Konto blockiert ein 404 bei der Auflösung eines Repositorys nicht die
    /// Inbox: Die Erwähnung in einem erreichbaren Repository wird trotzdem eingelesen, und das
    /// betroffene Repository wird mit verständlicher Diagnose ausgelassen.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_InaccessibleRepository_DoesNotBlockMentionsOfOtherRepositories()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, AccessibleUrl, "repo");
        await AddRepositoryAsync(fixture, InaccessibleUrl, "private-repo");
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        ConfigureResolutionWithInaccessibleRepository(fixture);
        fixture.ConfigureMentionObservations();
        fixture.ConfigureIssue();
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 27, "mention", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(27, ScmEventStore.Observation(item).Reference.Number);
        var inbox = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey == "notifications/test-scm/github.com", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(inbox.WatermarkUtc);
        var failure = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("repository/"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Permanent", failure.ErrorCode);
        Assert.Contains("separate Konto", failure.Diagnostic);
        Assert.DoesNotContain("keine neuen Anfragen dieses Providers", failure.Diagnostic);
    }

    /// <summary>Ein 404 bei der Repository-Auflösung sperrt die Quelle nicht für immer: Wird dem
    /// Konto später Zugriff erteilt, greift die Überwachung nach dem Backoff wieder.</summary>
    [Fact]
    public async Task Monitor_RepositoryResolutionNotFound_IsRetriedAfterBackoffInsteadOfBlockedForever()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, AccessibleUrl, "repo");
        await AddRepositoryAsync(fixture, InaccessibleUrl, "private-repo");
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        ConfigureResolutionWithInaccessibleRepository(fixture);
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var failure = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("repository/"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(failure.NextAttemptUtc);
        Assert.NotEqual(long.MaxValue, failure.NextAttemptUtc);
        Assert.True(failure.NextAttemptUtc <= before + (long)TimeSpan.FromHours(1).TotalMilliseconds,
            $"NextAttemptUtc liegt zu weit in der Zukunft: {failure.NextAttemptUtc - before} ms.");
    }

    private static void ConfigureResolutionWithInaccessibleRepository(ScmDispatchFixture fixture)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                context.RepositoryId.Contains("private-repo", StringComparison.OrdinalIgnoreCase)
                    ? Task.FromException<ScmEventRepository>(new ScmProviderException(ScmProviderFailureKind.Permanent,
                        "GitHub-Anfrage fehlgeschlagen (HTTP 404, github.com/repos/owner/private-repo)."))
                    : Task.FromResult(ScmDispatchFixture.BotRepository with { Konto = context.Konto }));

    private static async Task AddRepositoryAsync(ScmDispatchFixture fixture, string repositoryUrl, string repositoryName)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = repositoryName };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            PluginTyp = fixture.Repository.ProviderPrefix,
            Aktiv = true
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
    }
}
