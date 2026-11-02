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

    private static readonly ScmEventRepository BotRepository = new("test-scm", "github.com", "R_1",
        "owner", "repo", "U_1", "BOT", "scm-bot", true, ScmKontoModus.SeparatesKonto);

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
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureResolutionWithInaccessibleRepository(fixture);
        ConfigureMentionObservations(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) =>
                new Issue(reference.Number, "Titel", "Body", [], null, "https://github.com/owner/repo/issues/" + reference.Number));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 27, "mention", DateTimeOffset.UtcNow));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(27, ScmEventStore.Observation(item).Reference.Number);
        var inbox = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey == "notifications/test-scm/github.com");
        Assert.NotNull(inbox.WatermarkUtc);
        var failure = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("repository/"));
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
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureResolutionWithInaccessibleRepository(fixture);
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var failure = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("repository/"));
        Assert.NotNull(failure.NextAttemptUtc);
        Assert.NotEqual(long.MaxValue, failure.NextAttemptUtc);
        Assert.True(failure.NextAttemptUtc <= before + (long)TimeSpan.FromHours(1).TotalMilliseconds,
            $"NextAttemptUtc liegt zu weit in der Zukunft: {failure.NextAttemptUtc - before} ms.");
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task SetKontoModusAsync(ScmDispatchFixture fixture, ScmKontoModus modus)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>().SetScmKontoModusAsync(modus);
    }

    private static void ConfigureResolutionWithInaccessibleRepository(ScmDispatchFixture fixture)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                context.RepositoryId.Contains("private-repo", StringComparison.OrdinalIgnoreCase)
                    ? Task.FromException<ScmEventRepository>(new ScmProviderException(ScmProviderFailureKind.Permanent,
                        "GitHub-Anfrage fehlgeschlagen (HTTP 404, github.com/repos/owner/private-repo)."))
                    : Task.FromResult(BotRepository with { Konto = context.Konto }));

    private static void ConfigureMentionObservations(ScmDispatchFixture fixture)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, "U_1", "owner", null, null,
                    "@scm-bot bitte übernehmen", "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }

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
