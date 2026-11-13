using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für <see cref="ScmBackmergeService"/> (Erkennung, Aktivierungsmodi, Merge-Pfad, Checkpoints).</summary>
public sealed class ScmBackmergeServiceTests : IDisposable
{
    private readonly SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AppEinstellungService _einstellungen;
    private readonly ScmBackmergeService _sut;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly Mock<IGitPlugin> _plugin = new();
    private readonly ScmEventRepository _remote = new("github", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "viewer", true);
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly GitRepository _lokal;

    /// <summary>Initialisiert DB, Einstellungen und das lokale Repository mit Basisbranch <c>staging</c>.</summary>
    public ScmBackmergeServiceTests()
    {
        _einstellungen = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        _sut = new ScmBackmergeService(_db, _einstellungen, new ScmEventChangeNotifier(),
            _time, Options.Create(new ScmEventOptions()),
            NullLogger<ScmBackmergeService>.Instance);
        _lokal = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "github",
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            Aktiv = true,
            DefaultSourceBranchName = "staging"
        };
        _plugin.Setup(x => x.GetDefaultBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
    }

    /// <inheritdoc/>
    public void Dispose() => _db.Dispose();

    private static PullRequestStatusInfo StatusInfo(PullRequestStatus status = PullRequestStatus.Open,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable,
        string source = "main", string target = "staging", bool isDraft = false,
        PullRequestReviewDecision review = PullRequestReviewDecision.Unknown,
        PullRequestChecksState checks = PullRequestChecksState.Unknown)
        => new(PullRequestProvider.GitHub, "owner/repo", 7, "PR_7",
            "https://github.com/owner/repo/pull/7", "Backmerge", source, target, "sha", null,
            status, mergeStatus, DateTimeOffset.UtcNow, isDraft, review, checks);

    private void SetStatus(PullRequestStatus status = PullRequestStatus.Open,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable,
        string source = "main", string target = "staging", bool isDraft = false,
        PullRequestReviewDecision review = PullRequestReviewDecision.Unknown,
        PullRequestChecksState checks = PullRequestChecksState.Unknown)
        => _plugin.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatusInfo(status, mergeStatus, source, target, isDraft, review, checks));

    private Task EvaluateAsync(params ScmEventReference[] referenzen)
        => _sut.EvaluateDiscoveredReferencesAsync(_plugin.Object, _remote, referenzen, [_lokal]);

    private ScmEventReference Pr(int nummer = 7) => new(_remote, ScmReferenceKind.PullRequest, nummer);

    private void VerifyKeinProviderAufruf()
    {
        _plugin.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyMerge(bool allowProtectedBranchBypass, Times times)
        => _plugin.Verify(x => x.CompletePullRequestAsync("owner/repo", 7,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                && o.MergeMethod == PullRequestMergeMethod.Merge
                && o.AllowProtectedBranchBypass == allowProtectedBranchBypass),
            It.IsAny<CancellationToken>()), times);

    /// <summary>Deaktivierter Modus: keine Statusabfrage und kein Merge.</summary>
    [Fact]
    public async Task Evaluate_ModusDeaktiviert_KeinMerge()
    {
        await EvaluateAsync(Pr());
        VerifyKeinProviderAufruf();
    }

    /// <summary>Global aktiviert: Backmerge-PR (main → staging) wird zuerst regulär gemergt; der
    /// Admin-Fallback wird ohne gesetzte Option nicht aufgerufen.</summary>
    [Fact]
    public async Task Evaluate_ModusGlobal_BackmergeWirdGemergt()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Never());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint.Should().NotBeNull();
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.ErrorCode.Should().Be("Merged");
        checkpoint.Diagnostic.Should().Contain("automatisch gemergt");
    }

    /// <summary>Eine endgültige Entscheidung überschreibt den Zwischen-Fehlercode früherer
    /// Versuche — ein veraltetes „WaitingForMergeable“ bleibt nicht stehen.</summary>
    [Fact]
    public async Task Evaluate_EntscheidungNachWarten_LoeschtZwischenErrorCode()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked);
        await EvaluateAsync(Pr());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("WaitingForMergeable");

        SetStatus(status: PullRequestStatus.Closed);
        _time.Advance(TimeSpan.FromHours(1));
        await EvaluateAsync(Pr());

        checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("Decided");
        checkpoint.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("Kein offener Pull Request");
    }

    /// <summary>Projektbezogener Modus ohne Projekt-Flag: kein Merge.</summary>
    [Fact]
    public async Task Evaluate_ModusProjektbezogenOhneFlag_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Projektbezogen);
        await EvaluateAsync(Pr());
        VerifyKeinProviderAufruf();
    }

    /// <summary>Projektbezogener Modus mit gesetztem Projekt-Flag: Merge wird ausgeführt.</summary>
    [Fact]
    public async Task Evaluate_ModusProjektbezogenMitFlag_WirdGemergt()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Projektbezogen);
        await _einstellungen.SetBackmergeProjektAktivAsync(_projektId, true);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());
        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Never());
    }

    /// <summary>Kein Backmerge-Muster (head ≠ Hauptbranch): kein Merge, endgültige Entscheidung.</summary>
    [Fact]
    public async Task Evaluate_KeinBackmergeMuster_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(source: "feature/x", target: "staging");

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("Kein Backmerge");
    }

    /// <summary>Falsches Ziel (base ≠ Basisbranch): kein Merge.</summary>
    [Fact]
    public async Task Evaluate_FalschesZiel_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(source: "main", target: "main");

        await EvaluateAsync(Pr());
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Geschlossener/gemergter PR: kein Merge.</summary>
    [Theory]
    [InlineData(PullRequestStatus.Closed)]
    [InlineData(PullRequestStatus.Merged)]
    public async Task Evaluate_GeschlossenerPr_KeinMerge(PullRequestStatus status)
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(status: status);

        await EvaluateAsync(Pr());
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Noch nicht mergebarer Backmerge (z. B. Draft/blockiert): kein Merge, aber erneute Prüfung eingeplant.</summary>
    [Theory]
    [InlineData(PullRequestMergeStatus.Blocked)]
    [InlineData(PullRequestMergeStatus.Conflicting)]
    [InlineData(PullRequestMergeStatus.Unknown)]
    public async Task Evaluate_NichtMergebar_WartetUndPrueftErneut(PullRequestMergeStatus mergeStatus)
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: mergeStatus);

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("wartet auf Mergebarkeit");
    }

    /// <summary>Token ohne Schreibrecht: keine Provideraufrufe.</summary>
    [Fact]
    public async Task Evaluate_OhneSchreibrecht_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        var remoteOhneRecht = _remote with { CanWrite = false };
        await _sut.EvaluateDiscoveredReferencesAsync(_plugin.Object, remoteOhneRecht, [Pr()], [_lokal]);
        VerifyKeinProviderAufruf();
    }

    /// <summary>Inaktives lokales Repository: kein Merge.</summary>
    [Fact]
    public async Task Evaluate_InaktivesRepository_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        _lokal.Aktiv = false;
        await EvaluateAsync(Pr());
        VerifyKeinProviderAufruf();
    }

    /// <summary>Ohne konfigurierten Basisbranch: kein Merge.</summary>
    [Fact]
    public async Task Evaluate_OhneBasisbranch_KeinMerge()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        _lokal.DefaultSourceBranchName = null;
        await EvaluateAsync(Pr());
        VerifyKeinProviderAufruf();
    }

    /// <summary>Fehlgeschlagener Merge: Backoff-Wiederholung statt endgültiger Entscheidung.</summary>
    [Fact]
    public async Task Evaluate_MergeFehlgeschlagen_BackoffWiederholung()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Failed("merge verweigert"));

        await EvaluateAsync(Pr());

        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.Failures.Should().Be(1);
        checkpoint.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("merge verweigert");
    }

    /// <summary>Permanente Providerfehler sperren die Bewertung endgültig (kein Dauer-Retry).</summary>
    [Fact]
    public async Task Evaluate_PermanenterProviderfehler_EndgueltigGesperrt()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        _plugin.Setup(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permission, "kein Zugriff"));

        await EvaluateAsync(Pr());

        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.ErrorCode.Should().Be("Permission");
    }

    /// <summary>Eine in der Wartezeit liegende Referenz wird nicht erneut beim Provider abgefragt.</summary>
    [Fact]
    public async Task Evaluate_CheckpointInWartezeit_KeineErneuteAbfrage()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked);

        await EvaluateAsync(Pr());
        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Ein nicht passender Issue-Eintrag wird ignoriert.</summary>
    [Fact]
    public async Task Evaluate_IssueReferenz_WirdIgnoriert()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        await EvaluateAsync(new ScmEventReference(_remote, ScmReferenceKind.Issue, 7));
        VerifyKeinProviderAufruf();
    }

    /// <summary>Draft-Backmerge ohne aktivierte Draft-Aktivierung: bisheriges Warte-Verhalten,
    /// weder Ready-Aufruf noch Merge.</summary>
    [Fact]
    public async Task Evaluate_DraftOhneAktivierung_WartetOhneReadyAufruf()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked, isDraft: true);

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("WaitingForMergeable");
        checkpoint.Diagnostic.Should().Contain("Entwurf");
        checkpoint.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
    }

    /// <summary>Draft-Backmerge mit aktivierter Option: Ready-Aufruf, erneuter Status-Read und
    /// regulärer Merge ohne Admin-Fallback.</summary>
    [Fact]
    public async Task Evaluate_DraftMitAktivierung_WirdBereitGemachtUndGemergt()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        await _einstellungen.SetScmBackmergeDraftAktivierungAsync(true);
        var abfragen = 0;
        _plugin.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++abfragen == 1
                ? StatusInfo(mergeStatus: PullRequestMergeStatus.Blocked, isDraft: true)
                : StatusInfo(mergeStatus: PullRequestMergeStatus.Mergeable, isDraft: false));
        _plugin.Setup(x => x.MarkPullRequestReadyAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("bereit"));
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.MarkPullRequestReadyAsync("owner/repo", 7, It.IsAny<CancellationToken>()), Times.Once);
        _plugin.Verify(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()), Times.Exactly(2));
        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Never());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("automatisch gemergt");
    }

    /// <summary>Fehlschlagende Draft-Aktivierung: Temporary-Backoff ohne erneuten Status-Read und ohne Merge.</summary>
    [Fact]
    public async Task Evaluate_DraftAktivierungFehlgeschlagen_BackoffWiederholung()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        await _einstellungen.SetScmBackmergeDraftAktivierungAsync(true);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked, isDraft: true);
        _plugin.Setup(x => x.MarkPullRequestReadyAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Failed("kein Zugriff"));

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()), Times.Once);
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.Failures.Should().Be(1);
        checkpoint.ErrorCode.Should().Be("Temporary");
        checkpoint.Diagnostic.Should().Contain("Draft-Aktivierung fehlgeschlagen").And.Contain("kein Zugriff");
        checkpoint.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
    }

    /// <summary>Blockade nur durch Review-Pflicht bei grünen Checks: Der Dienst genehmigt den
    /// Backmerge-PR selbst (ApprovalOnly genügt Schreibrecht) und mergt danach regulär.</summary>
    [Fact]
    public async Task Evaluate_NurReviewBlockiert_GenehmigtUndMergedRegulaer()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        var abfragen = 0;
        _plugin.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++abfragen == 1
                ? StatusInfo(mergeStatus: PullRequestMergeStatus.Blocked,
                    review: PullRequestReviewDecision.ReviewRequired, checks: PullRequestChecksState.Success)
                : StatusInfo(mergeStatus: PullRequestMergeStatus.Mergeable,
                    review: PullRequestReviewDecision.Approved, checks: PullRequestChecksState.Success));
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("genehmigt"));
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.CompletePullRequestAsync("owner/repo", 7,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
            It.IsAny<CancellationToken>()), Times.Once);
        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("Merged");
    }

    /// <summary>Review-Blockade mit wirkungsloser Genehmigung: Bei aktiviertem Admin-Merge wird der
    /// reguläre Merge versucht und der Admin-Fallback ausgeführt.</summary>
    [Fact]
    public async Task Evaluate_NurReviewBlockiertMitAdminMerge_BypassFallback()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        await _einstellungen.SetScmBackmergeAdminMergeAsync(true);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked,
            review: PullRequestReviewDecision.ReviewRequired, checks: PullRequestChecksState.Success);
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("genehmigt"));
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                    && !o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.BlockedResult("review erforderlich"));
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                    && o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Once());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("Merged");
    }

    /// <summary>Review-Blockade bei laufenden Checks: weder Genehmigung noch Merge, nur Warten.</summary>
    [Fact]
    public async Task Evaluate_ReviewBlockiertChecksPending_WartetOhneGenehmigung()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked,
            review: PullRequestReviewDecision.ReviewRequired, checks: PullRequestChecksState.Pending);

        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("WaitingForMergeable");
        checkpoint.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
    }

    /// <summary>Eine bereits gesendete Genehmigung wird im nächsten Zyklus nicht wiederholt —
    /// stattdessen wartet der Dienst oder greift auf den Admin-Merge zurück.</summary>
    [Fact]
    public async Task Evaluate_GenehmigungGesendet_KeineErneuteGenehmigung()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus(mergeStatus: PullRequestMergeStatus.Blocked,
            review: PullRequestReviewDecision.ReviewRequired, checks: PullRequestChecksState.Success);
        _plugin.Setup(x => x.CompletePullRequestAsync("owner/repo", 7,
                It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Approved("genehmigt"));

        await EvaluateAsync(Pr());
        _time.Advance(TimeSpan.FromHours(1));
        await EvaluateAsync(Pr());

        _plugin.Verify(x => x.CompletePullRequestAsync("owner/repo", 7,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.ApprovalOnly),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Blockierter regulärer Merge mit aktivierter Admin-Option: zweiter Versuch mit Admin-Rechten.</summary>
    [Fact]
    public async Task Evaluate_AdminMergeFallback_ZweiterVersuchMitAdminRechten()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        await _einstellungen.SetScmBackmergeAdminMergeAsync(true);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.Is<PullRequestCompletionOptions>(o => !o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.BlockedResult("protected branch"));
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.Is<PullRequestCompletionOptions>(o => o.AllowProtectedBranchBypass), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Once());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("automatisch gemergt");
    }

    /// <summary>Blockierter regulärer Merge ohne aktivierte Admin-Option: kein Fallback, Backoff-Retry.</summary>
    [Fact]
    public async Task Evaluate_AdminMergeOhneOption_NurRegulaererVersuch()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.BlockedResult("protected branch"));

        await EvaluateAsync(Pr());

        VerifyMerge(allowProtectedBranchBypass: false, Times.Once());
        VerifyMerge(allowProtectedBranchBypass: true, Times.Never());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint!.ErrorCode.Should().Be("Blocked");
        checkpoint.Diagnostic.Should().Contain("protected branch");
        checkpoint.NextAttemptUtc.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .And.BeLessThan(long.MaxValue);
    }
}
