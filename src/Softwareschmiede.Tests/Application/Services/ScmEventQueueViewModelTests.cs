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

    private static ScmEventQueueViewModel View(IServiceScopeFactory scopes, ScmDispatchFixture fixture)
        => new(scopes, fixture.Services.GetRequiredService<ScmEventChangeNotifier>(), Mock.Of<IDialogService>(), NullLogger<ScmEventQueueViewModel>.Instance);

    private sealed class FailingScopes(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        internal bool Fail { get; set; }
        public IServiceScope CreateScope() => Fail ? throw new IOException("Fixture storage is unavailable") : inner.CreateScope();
    }
}
