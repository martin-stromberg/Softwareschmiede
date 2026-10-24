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
    private readonly Mock<IGitPlugin> _plugin = new();
    private readonly ScmEventRepository _remote = new("github", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "viewer", true);
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly GitRepository _lokal;

    /// <summary>Initialisiert DB, Einstellungen und das lokale Repository mit Basisbranch <c>staging</c>.</summary>
    public ScmBackmergeServiceTests()
    {
        _einstellungen = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        _sut = new ScmBackmergeService(_db, _einstellungen, new ScmEventChangeNotifier(),
            new FakeTimeProvider(DateTimeOffset.UtcNow), Options.Create(new ScmEventOptions()),
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

    private void SetStatus(PullRequestStatus status = PullRequestStatus.Open,
        PullRequestMergeStatus mergeStatus = PullRequestMergeStatus.Mergeable,
        string source = "main", string target = "staging")
        => _plugin.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestStatusInfo(PullRequestProvider.GitHub, "owner/repo", 7, "PR_7",
                "https://github.com/owner/repo/pull/7", "Backmerge", source, target, "sha", null,
                status, mergeStatus, DateTimeOffset.UtcNow));

    private Task EvaluateAsync(params ScmEventReference[] referenzen)
        => _sut.EvaluateDiscoveredReferencesAsync(_plugin.Object, _remote, referenzen, [_lokal]);

    private ScmEventReference Pr(int nummer = 7) => new(_remote, ScmReferenceKind.PullRequest, nummer);

    private void VerifyKeinProviderAufruf()
    {
        _plugin.Verify(x => x.GetPullRequestStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _plugin.Verify(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyAdminMerge(Times times)
        => _plugin.Verify(x => x.CompletePullRequestAsync("owner/repo", 7,
            It.Is<PullRequestCompletionOptions>(o => o.Strategy == PullRequestCompletionStrategy.Merge
                && o.MergeMethod == PullRequestMergeMethod.Merge && o.AllowProtectedBranchBypass),
            It.IsAny<CancellationToken>()), times);

    /// <summary>Deaktivierter Modus: keine Statusabfrage und kein Merge.</summary>
    [Fact]
    public async Task Evaluate_ModusDeaktiviert_KeinMerge()
    {
        await EvaluateAsync(Pr());
        VerifyKeinProviderAufruf();
    }

    /// <summary>Global aktiviert: Backmerge-PR (main → staging) wird per Admin-Merge bestätigt.</summary>
    [Fact]
    public async Task Evaluate_ModusGlobal_BackmergeWirdGemergt()
    {
        await _einstellungen.SetScmBackmergeModusAsync(ScmBackmergeModus.Global);
        SetStatus();
        _plugin.Setup(x => x.CompletePullRequestAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<PullRequestCompletionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletionResult.Completed("merge-sha"));

        await EvaluateAsync(Pr());

        VerifyAdminMerge(Times.Once());
        var checkpoint = await _db.ScmObservationCheckpoints.FindAsync(ScmBackmergeService.CheckpointKey(_remote, 7));
        checkpoint.Should().NotBeNull();
        checkpoint!.NextAttemptUtc.Should().Be(long.MaxValue);
        checkpoint.Diagnostic.Should().Contain("automatisch gemergt");
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
        VerifyAdminMerge(Times.Once());
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
}
