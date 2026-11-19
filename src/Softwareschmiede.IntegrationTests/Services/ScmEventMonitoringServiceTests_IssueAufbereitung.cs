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

/// <summary>„Issue erstellt"-Erkennung am repository-weiten Enumerationskanal: Baseline ohne
/// Retro-Scan, dedizierter Aufbereitungsvorgang (markierte Aufgabe + internes, trigger-freies
/// Ereignis) und Isolation von der kanonischen Issue-Ereignisauflösung.</summary>
public sealed class ScmEventMonitoringServiceTests_IssueAufbereitung
{
    /// <summary>Die erste Beobachtung der Quelle legt nur den Beobachtungsbeginn fest: kein
    /// Aufbereitungsvorgang für den vorhandenen Bestand — weder in der Erst- noch in der
    /// Folgebeobachtung (kein Retro-Scan).</summary>
    [Fact]
    public async Task Monitor_FirstObservation_CreatesBaselineOnly()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var baseline = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("issueaufbereitung/test-scm/github.com/R_1", baseline.SourceKey);
        Assert.NotNull(baseline.WatermarkUtc);
    }

    /// <summary>Happy Path: Nach der Baseline meldet die Enumeration ein neu angelegtes Issue
    /// (<see cref="ScmEventObservation.CreatedAt"/> nach dem Beobachtungsbeginn). Es entsteht genau
    /// eine Zweck-markierte Aufgabe mit eigenem SourceKey-Namensraum und ein internes, trigger-freies
    /// Ereignis — sofort fällig: Der Ausführungspfad aus Schritt 6 wird im selben Zyklus geprüft
    /// und wartet hier mit ehrlichem Grund, weil das konfigurierte Plugin nicht ausführbar ist.</summary>
    [Fact]
    public async Task Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungPluginKey, "custom.auf");
        var repositoryId = await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        // Neues Issue nach dem Beobachtungsbeginn; die Beschreibung enthält kein Trigger-Keyword.
        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, task.Zweck);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal("custom.auf", task.KiPluginPrefix);
        Assert.Equal(9, task.IssueReferenz!.IssueNummer);
        Assert.EndsWith("/IssueAufbereitung/9", task.IssueReferenz.SourceKey);
        Assert.DoesNotContain("/Issue/9", task.IssueReferenz.SourceKey);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.AufgabeId);
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, item.Zweck);
        Assert.EndsWith("/IssueAufbereitung", item.EventKey);
        // Sofort fällig (nicht mehr geparkt): Der Dispatch im selben Zyklus scheitert hier
        // ehrlich an der nicht ausführbaren Plugin-Zuordnung "custom.auf".
        Assert.Equal("Wartend", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Contains("custom.auf", item.Reason);
        Assert.Contains("Issue-Aufbereitung", item.Reason);
        var observation = ScmEventStore.Observation(item);
        Assert.Equal(ScmContentKind.Description, observation.ContentKind);
        Assert.DoesNotContain("$Softwareschmiede", observation.Body); // Trigger-Validierung umgangen.
        Assert.Contains(await db.Protokolleintraege.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.AufgabeId == task.Id && x.Typ == ProtokollTyp.SystemMeldung && x.Inhalt.Contains("Issue-Aufbereitung"));
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Wiederholte Beobachtungszyklen über demselben Issue erzeugen keinen zweiten
    /// Aufbereitungsvorgang (Auswertungs-Checkpoint- und EventKey-Dedup).</summary>
    [Fact]
    public async Task Monitor_RepeatedPolls_CreateNoSecondPreparation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Single(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ohne globalen Funktionsschalter erzeugt die Erkennung nichts — auch keinen
    /// Baseline-Checkpoint als Nebeneffekt.</summary>
    [Fact]
    public async Task Monitor_GlobalSwitchDisabled_CreatesNothing()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow));
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ohne Repository-Freischaltung wird die Beobachtung ignoriert — auch der
    /// Beobachtungsbeginn wird nicht verbraucht.</summary>
    [Fact]
    public async Task Monitor_RepositoryFlagDisabled_CreatesNothing()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: false);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow));
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ein freigeschaltetes Repository hinter einem Plugin ohne <see cref="IScmEventSource"/>
    /// wird nie enumeriert und löst daher nichts aus.</summary>
    [Fact]
    public async Task Monitor_RepositoryWithoutEventCapablePlugin_CreatesNothing()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true, pluginTyp: "Other.Plugin");
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ein erst nach der Baseline entdecktes Issue, dessen Erstellzeitpunkt vor dem
    /// Beobachtungsbeginn liegt, ist Altbestand und löst keinen Auftrag aus.</summary>
    [Fact]
    public async Task Monitor_IssueCreatedBeforeBaseline_CreatesNothing()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 10));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Eine spätere Beschreibungsänderung — einschließlich der vom Anwendungspfad selbst
    /// geschriebenen Beschreibung mit Marker — ist keine Erstbeobachtung und erzeugt keine
    /// Aufbereitungs-Schleife.</summary>
    [Fact]
    public async Task Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        // Dieselbe Beschreibungsquelle (ContentKey), neue Revision mit eigenem Marker-Body.
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt, "Vom Aufbereiter ersetzt.\n<!-- softwareschmiede:action:x -->",
                DateTimeOffset.UtcNow)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Single(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ein Trigger-Kommentar auf demselben Issue wird regulär aufgelöst: Die
    /// Aufbereitungs-Aufgabe ist weder Treffer noch Mehrdeutigkeitsbeitrag; die kanonische
    /// Aufgabe mit <c>…/Issue/{n}</c>-SourceKey wird parallel angelegt.</summary>
    [Fact]
    public async Task Monitor_TriggerOnPreparedIssue_ResolvesToCanonicalTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        // Externer Trigger-Kommentar auf dem aufbereiteten Issue.
        ConfigureDescriptions(fixture,
            request => request.Reference.Number == 9
                ? DescriptionObservation(request, createdAt)
                : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)),
            request => request.Reference.Number == 9
                ? new ScmEventObservation(request.Reference, ScmContentKind.IssueComment,
                    "C_" + request.Reference.Number, "42", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null,
                    "U_1", "owner", null, null, "$Softwareschmiede test",
                    "https://github.com/owner/repo/issues/" + request.Reference.Number, DateTimeOffset.UtcNow)
                : null);
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var tasks = await db.Aufgaben.Include(x => x.IssueReferenz).OrderBy(x => x.ErstellungsDatum).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, tasks.Count);
        var aufbereitung = Assert.Single(tasks, x => x.Zweck == ScmIssueAufbereitungsService.Zweck);
        var kanonisch = Assert.Single(tasks, x => x.Zweck == null);
        Assert.EndsWith("/Issue/9", kanonisch.IssueReferenz!.SourceKey);
        Assert.Equal(9, kanonisch.IssueReferenz.IssueNummer);
        var events = await db.ScmIncomingEvents.OrderBy(x => x.Sequence).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        var trigger = Assert.Single(events, x => x.Zweck == null);
        Assert.Equal(kanonisch.Id, trigger.AufgabeId);
        Assert.Equal(ScmContentKind.IssueComment, ScmEventStore.Observation(trigger).ContentKind);
        Assert.DoesNotContain(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Diagnostic == ScmEventStore.AmbiguousAssignmentDiagnostic);
        Assert.Equal(aufbereitung.Id, Assert.Single(events, x => x.Zweck != null).AufgabeId);
    }

    /// <summary>Der interne Auftrag nutzt den eigenen Ausführungspfad: Ein expliziter
    /// Dispatch ohne ausführbare CLI endet ehrlich wartend ohne Start, Prompt oder Hostaktion.</summary>
    [Fact]
    public async Task Monitor_InternalEvent_ExplicitDispatch_WaitsWithHonestReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        Guid eventId;
        using (var scope = fixture.Services.CreateScope())
            eventId = (await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>()
                .ScmIncomingEvents.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).Id;

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Contains("Aufbereitung", item.Reason);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
    }

    private static async Task EnableAsync(ScmDispatchFixture fixture)
        => await fixture.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungEnabledKey, "true");

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture, bool aufbereitung,
        string url = "https://github.com/owner/repo", string? pluginTyp = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Aufbereitung" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = url,
            RepositoryName = "repo",
            PluginTyp = pluginTyp ?? fixture.Repository.ProviderPrefix,
            Aktiv = true,
            IssueAufbereitungAktiv = aufbereitung
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureDescriptions(ScmDispatchFixture fixture,
        Func<ScmEventReadRequest, ScmEventObservation?> descriptionFor,
        Func<ScmEventReadRequest, ScmEventObservation?>? commentFor = null)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(
                It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                (request.ContentKind switch
                {
                    ScmContentKind.Description => descriptionFor(request),
                    ScmContentKind.IssueComment => commentFor?.Invoke(request),
                    _ => null
                }) is { } observation ? new[] { observation } : Array.Empty<ScmEventObservation>())));

    private static ScmEventObservation DescriptionObservation(ScmEventReadRequest request,
        DateTimeOffset createdAt, string body = "Issue-Beschreibung", DateTimeOffset? lastEditedAt = null)
        => new(request.Reference, ScmContentKind.Description, "I_" + request.Reference.Number, null,
            createdAt, lastEditedAt ?? createdAt, lastEditedAt, "U_1", "owner", null, null, body,
            "https://github.com/owner/repo/issues/" + request.Reference.Number, DateTimeOffset.UtcNow);
}
