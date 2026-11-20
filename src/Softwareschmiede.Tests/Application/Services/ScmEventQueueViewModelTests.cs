using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>User-visible loading failures, source recovery and truthful queue commands.</summary>
public sealed class ScmEventQueueViewModelTests
{
    /// <summary>Initial and filter-change failures never masquerade as a successful empty/new-filter result.</summary>
    [Fact]
    public async Task LoadingFailure_ExplainsInitialFailureAndClearsOldFilterRows_ThenRecovers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.AddAsync();
        var scopes = new FailingScopes(fixture.Services.GetRequiredService<IServiceScopeFactory>()) { Fail = true };
        using var view = View(scopes, fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Contains("erneut laden", view.LoadError);
        Assert.False(view.IsEmpty);
        Assert.False(view.IsLoading);
        scopes.Fail = false;
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(view.LoadError);
        Assert.Single(view.Events);
        scopes.Fail = true;
        view.SelectedFilter = "Verarbeitet";
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Empty(view.Events);
        Assert.False(view.IsEmpty);
        Assert.NotNull(view.LoadError);
        scopes.Fail = false;
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(view.LoadError);
        Assert.True(view.IsEmpty);
        Assert.Empty(view.Events);
    }

    /// <summary>CLI selection is enabled only while a real selection or handoff can still take place.</summary>
    [Theory]
    [InlineData("Wartend", false, true)]
    [InlineData("Zugestellt", true, false)]
    [InlineData("InBearbeitung", true, false)]
    [InlineData("ZustellungUngewiss", true, false)]
    [InlineData("VerarbeitungUngeklärt", true, false)]
    public async Task OpenCommand_MatchesUndeliveredSelectionSemantics(string status, bool attempt, bool enabled)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedEvent = new(Guid.NewGuid(), Guid.NewGuid(), "task", "owner/repo", "Issue #1", "Kommentar", "now", status,
            status, "body", "origin", null, null, attempt ? Guid.NewGuid() : null, false, false);
        Assert.Equal(enabled, view.OpenEventCommand.CanExecute(null));
        Assert.True(view.NavigateToTaskCommand.CanExecute(null));
        Assert.Equal(attempt, view.CheckStatusCommand.CanExecute(null));
    }

    /// <summary>Final states disable every mutating command; all other states stay cancellable.</summary>
    [Theory]
    [InlineData("Aufgenommen", true)]
    [InlineData("Wartend", true)]
    [InlineData("Zugestellt", true)]
    [InlineData("VerarbeitungUngeklärt", true)]
    [InlineData("Verarbeitet", false)]
    [InlineData("Abgebrochen", false)]
    public async Task CancelCommand_EnabledOnlyForNonFinalEvents(string status, bool enabled)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedEvent = new(Guid.NewGuid(), Guid.NewGuid(), "task", "owner/repo", "Issue #1", "Kommentar", "now", status,
            status, "body", "origin", null, null, null, false, false);
        Assert.Equal(enabled, view.CancelCommand.CanExecute(null));
        Assert.Equal(enabled, view.RetryCommand.CanExecute(null));
        Assert.Equal(enabled, view.OpenEventCommand.CanExecute(null));
    }

    /// <summary>„Erneut prüfen" bleibt an einem „Verarbeitet"-Ereignis nur verfügbar, solange
    /// eine Outbox-Aktion (z. B. Screenshot-Nachweis) unvollendet ist; „Abgebrochen" bleibt
    /// auch mit offener Aktion gesperrt, alle anderen Endzustände ohnehin.</summary>
    [Theory]
    [InlineData("Verarbeitet", true, true)]
    [InlineData("Verarbeitet", false, false)]
    [InlineData("Abgebrochen", true, false)]
    [InlineData("Abgebrochen", false, false)]
    [InlineData("Wartend", false, true)]
    [InlineData("VerarbeitungUngeklärt", true, true)]
    public async Task RetryCommand_ProcessedEvent_RequiresOpenOutboxAction(string status, bool openActions, bool enabled)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedEvent = new(Guid.NewGuid(), Guid.NewGuid(), "task", "owner/repo", "Issue #1", "Kommentar", "now", status,
            status, "body", "origin", null, null, null, false, openActions);
        Assert.Equal(enabled, view.RetryCommand.CanExecute(null));
    }

    /// <summary>Eine permanent gescheiterte attachments-Aktion an einem „Verarbeitet"-Ereignis
    /// ist aus der Queue bewusst erneut auslösbar: Die Projektion meldet die offene Aktion und
    /// der Klick führt sie tatsächlich erneut aus.</summary>
    [Fact]
    public async Task ProcessedEvent_WithFailedAttachmentsAction_OffersWorkingRetry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var attempts = 0;
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Attachments != null && r.Attachments.Count > 0), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest request, CancellationToken _) =>
            {
                if (++attempts == 1) throw new ScmProviderException(ScmProviderFailureKind.Permanent, "Upload wird nicht unterstützt.");
                fixture.Responses.Add(request);
                return Task.FromResult(new ScmResponseResult("remote", null, false));
            });
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3], TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId)), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "fertig.")), TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedFilter = "Verarbeitet";
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(view.Events);
        Assert.True(item.HasOpenActions);
        Assert.Equal(1, attempts);
        view.SelectedEvent = item;
        Assert.True(view.RetryCommand.CanExecute(null));

        await view.RetryCommand.ExecuteAsync();

        Assert.Equal(2, attempts);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal("Succeeded", (await db.ScmOutgoingActions.SingleAsync(x => x.Purpose == "attachments", cancellationToken: TestContext.Current.CancellationToken)).Status);
    }

    /// <summary>Eine gesperrte Outbox-Aktion benennt in den Integrationshinweisen das betroffene
    /// Ereignis und den heilsamen Weg über das ereignisbezogene „Erneut prüfen" — nicht die
    /// wirkungslose Quellen-Schaltfläche.</summary>
    [Fact]
    public async Task FailedOutgoingAction_DiagnosticNamesEventAndEventRetry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.ScmOutgoingActions.Add(new ScmOutgoingAction
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Purpose = "attachments",
                Status = "Failed",
                Error = "Upload fehlgeschlagen.",
                ErrorCode = "Permanent",
                NextAttemptUtc = long.MaxValue,
                RequestJson = "{}"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Screenshot-Nachweis", view.Diagnostics);
        Assert.Contains("owner/repo Issue #7", view.Diagnostics);
        Assert.Contains("Erneut prüfen", view.Diagnostics);
    }

    /// <summary>Der Hard-Stop-Hinweis passt zum Ereignisstatus: An „Verarbeitet" sagt er den
    /// Filterweg („Verarbeitet" oder „Alle" → „Erneut prüfen"), an „Abgebrochen" verweist er
    /// nicht auf eine deaktivierte Schaltfläche, an offenen Stati bleibt die direkte Anweisung.</summary>
    [Theory]
    [InlineData("Verarbeitet", "Filter „Verarbeitet“", "Erneut prüfen")]
    [InlineData("Abgebrochen", "keine Wiederholung verfügbar", null)]
    [InlineData("Wartend", "beim Ereignis „Erneut prüfen“ wählen", "Erneut prüfen")]
    public async Task FailedOutgoingAction_DiagnosticMatchesEventStatus(string status, string hint, string? retry)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            (await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, cancellationToken: TestContext.Current.CancellationToken)).Status = status;
            db.ScmOutgoingActions.Add(new ScmOutgoingAction
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Purpose = "attachments",
                Status = "Failed",
                Error = "Upload fehlgeschlagen.",
                ErrorCode = "Permanent",
                NextAttemptUtc = long.MaxValue,
                RequestJson = "{}"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Contains(hint, view.Diagnostics);
        if (retry is null) Assert.DoesNotContain("Erneut prüfen", view.Diagnostics);
        else Assert.Contains(retry, view.Diagnostics);
    }

    /// <summary>Eine ungewisse Folgeaktion am Wiederholungsende darf nicht als einfach
    /// retrybar verkauft werden: Sie wird nur noch remote abgeglichen; die Diagnose weist
    /// deshalb auf die manuelle GitHub-Prüfung statt auf einen automatischen Neuversand.</summary>
    [Fact]
    public async Task UncertainOutgoingAction_DiagnosticAdvisesManualGitHubCheck()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            (await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId, cancellationToken: TestContext.Current.CancellationToken)).Status = "Verarbeitet";
            db.ScmOutgoingActions.Add(new ScmOutgoingAction
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Purpose = "attachments",
                Status = "Uncertain",
                Error = "Ausgang ungewiss; Remoteergebnis noch nicht wiedergefunden.",
                ErrorCode = "Permanent",
                NextAttemptUtc = long.MaxValue,
                RequestJson = "{}"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Ausgang ungewiss", view.Diagnostics);
        Assert.Contains("GitHub", view.Diagnostics);
        Assert.Contains("kein automatischer Neuversand", view.Diagnostics);
        Assert.DoesNotContain("Erneut prüfen", view.Diagnostics);
    }

    /// <summary>A cancelled event keeps its attempt identity but disables check, retry, open and cancel.</summary>
    [Fact]
    public async Task CancelledEvent_DisablesAllMutatingCommands()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedEvent = new(Guid.NewGuid(), Guid.NewGuid(), "task", "owner/repo", "Issue #1", "Kommentar", "now", "Abgebrochen",
            "Abgebrochen", "body", "origin", "Vom Anwender abgebrochen.", Guid.NewGuid().ToString("D"), Guid.NewGuid(), false, false);
        Assert.False(view.CancelCommand.CanExecute(null));
        Assert.False(view.CheckStatusCommand.CanExecute(null));
        Assert.False(view.RetryCommand.CanExecute(null));
        Assert.False(view.OpenEventCommand.CanExecute(null));
        Assert.False(view.ResendCommand.CanExecute(null));
    }

    /// <summary>The dedicated filter and "Alle" keep cancelled entries visible; "Wartend" hides them.</summary>
    [Fact]
    public async Task CancelledEvent_ListsUnderAbgebrochenAndAlle_NotWartend()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId, TestContext.Current.CancellationToken);
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedFilter = "Wartend";
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Empty(view.Events);
        view.SelectedFilter = "Abgebrochen";
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(view.Events);
        Assert.Equal("Abgebrochen", item.Status);
        view.SelectedFilter = "Alle";
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Single(view.Events);
    }

    /// <summary>A halted source is visible even with no event; correcting the source clears the notice.</summary>
    [Fact]
    public async Task SourceAuthenticationFailure_IsVisibleWithoutEvents_AndClearsAfterRecovery()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "owner/repo",
                ErrorCode = "Authentication",
                Diagnostic = "GitHub-Anmeldung ungültig. Bitte erneut anmelden und anschließend Quellen erneut prüfen.",
                NextAttemptUtc = long.MaxValue
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Empty(view.Events);
        Assert.Contains("neue Anfragen können fehlen", view.MonitoringNotice);
        Assert.Contains("erneut anmelden", view.MonitoringNotice);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var source = (await db.ScmObservationCheckpoints.FindAsync(new object?[] { "owner/repo" }, TestContext.Current.CancellationToken))!;
            source.Diagnostic = null; source.ErrorCode = null; source.NextAttemptUtc = null;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(view.MonitoringNotice);
    }

    /// <summary>Unassigned references are the expected repo-wide state: no interruption banner and no
    /// diagnostics entry, while real observation errors on other sources still alarm.</summary>
    [Fact]
    public async Task UnassignedSources_NeitherBannerNorDiagnostics_RealErrorsStillAlarm()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "github/github.com/owner%2Frepo/Issue/1/Description",
                Diagnostic = ScmEventStore.UnassignedDiagnostic
            });
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "github/github.com/owner%2Frepo/PullRequest/2/ReviewComment",
                Diagnostic = ScmEventStore.UnassignedDiagnostic
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(view.MonitoringNotice);
        Assert.DoesNotContain(ScmEventStore.UnassignedDiagnostic, view.Diagnostics ?? string.Empty);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "repository/github/owner%2Frepo",
                ErrorCode = "Authentication",
                Diagnostic = "GitHub-Anmeldung ungültig. Bitte erneut anmelden und anschließend Quellen erneut prüfen.",
                NextAttemptUtc = long.MaxValue
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Contains("neue Anfragen können fehlen", view.MonitoringNotice);
        Assert.Contains("erneut anmelden", view.MonitoringNotice);
        Assert.DoesNotContain(ScmEventStore.UnassignedDiagnostic, view.Diagnostics ?? string.Empty);
        Assert.Contains("GitHub-Anmeldung ungültig", view.Diagnostics);
    }

    /// <summary>Die Inbox-Verwerfungsdiagnose bleibt unter Integrationshinweise sichtbar, löst aber
    /// keinen Unterbrechungshinweis aus; echte Fehler anderer Quellen alarmieren weiterhin.</summary>
    [Fact]
    public async Task InboxDiscardDiagnostic_VisibleWithoutMonitoringNotice()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "notifications/softwareschmiede.github/github.com",
                Diagnostic = ScmEventStore.InboxDiscardDiagnosticPrefix + " 1 verarbeitet, 2 verworfen (Repository nicht registriert)."
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(view.MonitoringNotice);
        Assert.Contains("verworfen", view.Diagnostics);
        Assert.Contains("notifications/", view.Diagnostics);
    }

    /// <summary>„Quellen erneut prüfen" stößt den eigenständigen Backmerge-Prüfzyklus auch bei
    /// deaktivierter Ereignisüberwachung an; die Ereignisquellen selbst bleiben ungelesen.</summary>
    [Fact]
    public async Task RetrySourcesCommand_MonitoringDisabled_StillRunsBackmergeCycle()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
            await settings.SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);
            await settings.SetScmBackmergeModeAsync(ScmBackmergeMode.Global, TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);

        await view.RetrySourcesCommand.ExecuteAsync();

        // Der Backmerge-Prüfzyklus lief: Repository-Auflösung und repository-weite Enumeration.
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Die Ereignisüberwachung blieb dagegen abgeschaltet: kein Quellen-Read.
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("deaktiviert", view.Message);
    }

    /// <summary>„Quellen erneut prüfen" stößt auch den Aufbereitungs-Entdeckungszyklus an —
    /// unabhängig vom Ereignisüberwachungs-Schalter; der Dienst prüft seinen eigenen Schalter.</summary>
    [Fact]
    public async Task RetrySourcesCommand_StillRunsAufbereitungCycle()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await fixture.SetSettingAsync(AppSettingService.ScmMonitoringEnabledKey, "false");
        await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, "true");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var project = new Project { Id = Guid.NewGuid(), Name = "Aufbereitung" };
            var repository = new GitRepository
            {
                Id = Guid.NewGuid(),
                Project = project,
                RepositoryUrl = "https://github.com/owner/repo",
                RepositoryName = "repo",
                PluginType = fixture.Repository.ProviderPrefix,
                Active = true,
                IssuePreparationEnabled = true
            };
            db.AddRange(project, repository);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);

        await view.RetrySourcesCommand.ExecuteAsync();

        // Der Aufbereitungs-Entdeckungszyklus lief: Repository-Auflösung und repository-weite Enumeration.
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Die Ereignisüberwachung blieb dagegen abgeschaltet: kein Quellen-Read.
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("deaktiviert", view.Message);
    }

    private static ScmEventQueueViewModel View(IServiceScopeFactory scopes, ScmDispatchFixture fixture)
        => new(scopes, fixture.Services.GetRequiredService<ScmEventChangeNotifier>(), Mock.Of<IDialogService>(),
            NullLogger<ScmEventQueueViewModel>.Instance);

    private sealed class FailingScopes(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        internal bool Fail { get; set; }
        public IServiceScope CreateScope() => Fail ? throw new IOException("Fixture storage is unavailable") : inner.CreateScope();
    }
}
