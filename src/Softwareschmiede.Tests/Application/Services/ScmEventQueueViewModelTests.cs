using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
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
        await view.RefreshAsync();
        Assert.Contains("erneut laden", view.LoadError);
        Assert.False(view.IsEmpty);
        Assert.False(view.IsLoading);
        scopes.Fail = false;
        await view.RefreshAsync();
        Assert.Null(view.LoadError);
        Assert.Single(view.Events);
        scopes.Fail = true;
        view.SelectedFilter = "Verarbeitet";
        await view.RefreshAsync();
        Assert.Empty(view.Events);
        Assert.False(view.IsEmpty);
        Assert.NotNull(view.LoadError);
        scopes.Fail = false;
        await view.RefreshAsync();
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
            status, "body", "origin", null, null, attempt ? Guid.NewGuid() : null, false);
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
            status, "body", "origin", null, null, null, false);
        Assert.Equal(enabled, view.CancelCommand.CanExecute(null));
        Assert.Equal(enabled, view.RetryCommand.CanExecute(null));
        Assert.Equal(enabled, view.OpenEventCommand.CanExecute(null));
    }

    /// <summary>A cancelled event keeps its attempt identity but disables check, retry, open and cancel.</summary>
    [Fact]
    public async Task CancelledEvent_DisablesAllMutatingCommands()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedEvent = new(Guid.NewGuid(), Guid.NewGuid(), "task", "owner/repo", "Issue #1", "Kommentar", "now", "Abgebrochen",
            "Abgebrochen", "body", "origin", "Vom Anwender abgebrochen.", Guid.NewGuid().ToString("D"), Guid.NewGuid(), false);
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
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().CancelAsync(eventId);
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        view.SelectedFilter = "Wartend";
        await view.RefreshAsync();
        Assert.Empty(view.Events);
        view.SelectedFilter = "Abgebrochen";
        await view.RefreshAsync();
        var item = Assert.Single(view.Events);
        Assert.Equal("Abgebrochen", item.Status);
        view.SelectedFilter = "Alle";
        await view.RefreshAsync();
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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "owner/repo",
                ErrorCode = "Authentication",
                Diagnostic = "GitHub-Anmeldung ungültig. Bitte erneut anmelden und anschließend Quellen erneut prüfen.",
                NextAttemptUtc = long.MaxValue
            });
            await db.SaveChangesAsync();
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync();
        Assert.Empty(view.Events);
        Assert.Contains("neue Anfragen können fehlen", view.MonitoringNotice);
        Assert.Contains("erneut anmelden", view.MonitoringNotice);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var source = (await db.ScmObservationCheckpoints.FindAsync("owner/repo"))!;
            source.Diagnostic = null; source.ErrorCode = null; source.NextAttemptUtc = null;
            await db.SaveChangesAsync();
        }
        await view.RefreshAsync();
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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
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
            await db.SaveChangesAsync();
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync();
        Assert.Null(view.MonitoringNotice);
        Assert.DoesNotContain(ScmEventStore.UnassignedDiagnostic, view.Diagnostics ?? string.Empty);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "repository/github/owner%2Frepo",
                ErrorCode = "Authentication",
                Diagnostic = "GitHub-Anmeldung ungültig. Bitte erneut anmelden und anschließend Quellen erneut prüfen.",
                NextAttemptUtc = long.MaxValue
            });
            await db.SaveChangesAsync();
        }
        await view.RefreshAsync();
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
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = "notifications/softwareschmiede.github/github.com",
                Diagnostic = ScmEventStore.InboxDiscardDiagnosticPrefix + " 1 verarbeitet, 2 verworfen (Repository nicht registriert)."
            });
            await db.SaveChangesAsync();
        }
        using var view = View(fixture.Services.GetRequiredService<IServiceScopeFactory>(), fixture);
        await view.RefreshAsync();
        Assert.Null(view.MonitoringNotice);
        Assert.Contains("verworfen", view.Diagnostics);
        Assert.Contains("notifications/", view.Diagnostics);
    }

    private static ScmEventQueueViewModel View(IServiceScopeFactory scopes, ScmDispatchFixture fixture)
        => new(scopes, fixture.Services.GetRequiredService<ScmEventChangeNotifier>(), Mock.Of<IDialogService>(), NullLogger<ScmEventQueueViewModel>.Instance);

    private sealed class FailingScopes(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        internal bool Fail { get; set; }
        public IServiceScope CreateScope() => Fail ? throw new IOException("Fixture storage is unavailable") : inner.CreateScope();
    }
}
