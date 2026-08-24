using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Exceptions;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den AutonomAufgabenInitialisierungsService.</summary>
public sealed class AutonomAufgabenInitialisierungsServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ICliRunner> _cliRunnerMock;
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly AutonomAufgabenInitialisierungsService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId;

    /// <summary>AutonomAufgabenInitialisierungsServiceTests.</summary>
    public AutonomAufgabenInitialisierungsServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _cliRunnerMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateCliRunnerMockMitErfolgreicherGitAusfuehrung();
        _gitPluginMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateGitPluginMockMitErfolgreichemKlon();
        _sut = AutonomAufgabenInitialisierungsServiceTestFactory.CreateService(_db, _cliRunnerMock.Object, _gitPluginMock.Object);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "AutonomAufgabenInit", Guid.NewGuid().ToString("N"));

        _projektId = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleProjekt(_db);
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private Aufgabe ErstelleUndPersistiereAufgabe(string arbeitsverzeichnispPfad)
    {
        var aufgabe = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleAufgabeMitLokalemKlon(_db, _projektId, arbeitsverzeichnispPfad, "Autonome Testaufgabe");
        _db.SaveChanges();
        return aufgabe;
    }

    private AutonomAufgabeInitialisierungsAnfrage ErstelleAnfrage(string arbeitsverzeichnispPfad) => new(
        ProjektBranchName: "feature/autonom-test",
        InitialPrompt: "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.",
        ArbeitsverzeichnisPfad: arbeitsverzeichnispPfad,
        RessourcenLimits: new RessourcenLimits(TokenBudget: 500000, TokenBudgetErweitert: null, LaufzeitLimitMinuten: 480),
        PersistenzModus: PersistenzModus.Standard,
        SkillAutogeneration: false);

    /// <summary>InitialisiereAsync erstellt die vollständige Arbeitsverzeichnisstruktur.</summary>
    [Fact]
    public async Task InitialisiereAsync_ErzeugtArbeitsverzeichnis()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        Directory.Exists(_testRoot).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "skills")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "skills", "archive")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "clones")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "tasks")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testRoot, "logs")).Should().BeTrue();
        File.Exists(Path.Combine(_testRoot, "plan.md")).Should().BeTrue();
        File.Exists(Path.Combine(_testRoot, "progress.md")).Should().BeTrue();
        File.Exists(Path.Combine(_testRoot, "governance.md")).Should().BeTrue();
    }

    /// <summary>InitialisiereAsync erzeugt den Repository-Klon im clones/repo_main/-Verzeichnis.</summary>
    [Fact]
    public async Task InitialisiereAsync_ErzeugtRepositoryKlon()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");
        Directory.Exists(repoMainPfad).Should().BeTrue();
        File.Exists(Path.Combine(repoMainPfad, ".git-marker")).Should().BeTrue();
        _gitPluginMock.Verify(p => p.CloneRepositoryAsync(aufgabe.GitRepository!.RepositoryUrl, repoMainPfad, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>InitialisiereAsync klont direkt von aufgabe.GitRepository.RepositoryUrl, nicht von aufgabe.LokalerKlonPfad.</summary>
    [Fact]
    public async Task InitialisiereAsync_KlontDirectVonRepositoryUrl()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);
        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        _gitPluginMock.Verify(
            p => p.CloneRepositoryAsync(aufgabe.GitRepository!.RepositoryUrl, repoMainPfad, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>InitialisiereAsync erstellt nach dem Klon den Projektbranch im geklonten Repository per IGitPlugin.CreateBranchAsync (checkt ihn dabei zugleich aus).</summary>
    [Fact]
    public async Task InitialisiereAsync_ErstelltProjektBranchNachKlon()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);
        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        _gitPluginMock.Verify(
            p => p.CreateBranchAsync(repoMainPfad, anfrage.ProjektBranchName, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>ErstelleProjektbranchAsync legt einen neuen lokalen Branch via IGitPlugin.CreateBranchAsync (checkout -b) an, wenn der Branch nicht remote existiert und lokal noch nicht existiert.</summary>
    [Fact]
    public async Task ErstelleProjektbranchAsync_AnlegtNeuenBranchMitGit()
    {
        _gitPluginMock
            .Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);
        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        _gitPluginMock.Verify(
            p => p.CreateBranchAsync(repoMainPfad, anfrage.ProjektBranchName, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _gitPluginMock.Verify(p => p.CheckoutRemoteBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>ErstelleProjektbranchAsync überspringt die Branch-Neuanlage, wenn der lokale Branch bereits existiert (Retry-Fall: ein vorheriger Initialisierungsversuch ist nach erfolgreicher Branch-Anlage, aber vor Abschluss fehlgeschlagen), statt mit "branch already exists" zu scheitern.</summary>
    [Fact]
    public async Task ErstelleProjektbranchAsync_UeberspringtAnlage_WennLokalerBranchBereitsExistiert()
    {
        var anfrage = ErstelleAnfrage(_testRoot);
        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");
        _cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("--list") && a.Contains(anfrage.ProjektBranchName)), repoMainPfad, It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(0, $"  {anfrage.ProjektBranchName}\n", string.Empty));
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);

        var konfiguration = await _sut.InitialisiereAsync(aufgabe, anfrage);

        konfiguration.ProjektBranchName.Should().Be(anfrage.ProjektBranchName);
        _gitPluginMock.Verify(p => p.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>ErstelleProjektbranchAsync checkt den bestehenden Remote-Branch aus, statt einen neuen anzulegen, wenn der Branch bereits remote existiert.</summary>
    [Fact]
    public async Task ErstelleProjektbranchAsync_CheckoutRemoteBranch_WennExistent()
    {
        var anfrage = ErstelleAnfrage(_testRoot);
        _gitPluginMock
            .Setup(p => p.GetRemoteBranchesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([anfrage.ProjektBranchName]);
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var repoMainPfad = Path.Combine(_testRoot, "clones", "repo_main");

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        _gitPluginMock.Verify(p => p.CheckoutRemoteBranchAsync(repoMainPfad, anfrage.ProjektBranchName, It.IsAny<CancellationToken>()), Times.Once);
        _cliRunnerMock.Verify(
            r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("branch")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>ErstelleProjektbranchAsync wirft eine InvalidOperationException, wenn IGitPlugin.CreateBranchAsync ("git checkout -b") fehlschlägt.</summary>
    [Fact]
    public async Task ErstelleProjektbranchAsync_WirftException_BeiGitFehler()
    {
        _gitPluginMock
            .Setup(p => p.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("git checkout -b fehlgeschlagen: fatal: Branch konnte nicht angelegt werden"));

        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>InitialisiereAsync erzeugt state.json mit korrektem Schema und den erforderlichen Top-Level-Keys.</summary>
    [Fact]
    public async Task InitialisiereAsync_ErzeugtStateJson()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        await _sut.InitialisiereAsync(aufgabe, anfrage);

        var stateJsonPfad = Path.Combine(_testRoot, "state.json");
        File.Exists(stateJsonPfad).Should().BeTrue();

        var json = await File.ReadAllTextAsync(stateJsonPfad);
        var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("task_id", out var taskId).Should().BeTrue();
        taskId.GetGuid().Should().Be(aufgabe.Id);
        root.TryGetProperty("runtime", out _).Should().BeTrue();
        root.TryGetProperty("governance", out _).Should().BeTrue();
        root.TryGetProperty("clones", out _).Should().BeTrue();
        root.TryGetProperty("subagents", out _).Should().BeTrue();
    }

    /// <summary>InitialisiereAsync erzeugt permissions.json mit Berechtigungen und Limits.</summary>
    [Fact]
    public async Task InitialisiereAsync_ErzeugtPermissionsJson()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        var konfiguration = await _sut.InitialisiereAsync(aufgabe, anfrage);

        var permissionsPfad = Path.Combine(_testRoot, "permissions.json");
        File.Exists(permissionsPfad).Should().BeTrue();
        konfiguration.PermissionsJsonPfad.Should().Be(permissionsPfad);

        var json = await File.ReadAllTextAsync(permissionsPfad);
        var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("allowed_actions", out var allowedActions).Should().BeTrue();
        allowedActions.GetArrayLength().Should().BeGreaterThan(0);
        root.TryGetProperty("limits", out var limits).Should().BeTrue();
        limits.GetProperty("token_budget").GetInt32().Should().Be(anfrage.RessourcenLimits.TokenBudget);
    }

    /// <summary>InitialisiereAsync lehnt ein ungültiges TokenBudget mit ArgumentException ab.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftArgumentException_BeiUngueltigemTokenBudget()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var basisAnfrage = ErstelleAnfrage(_testRoot);
        var anfrage = basisAnfrage with { RessourcenLimits = basisAnfrage.RessourcenLimits with { TokenBudget = 0 } };

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>ErstelleArbeitsverzeichnisStrukturAsync wirft eine ArgumentException bei relativem Pfad.</summary>
    [Fact]
    public async Task ErstelleArbeitsverzeichnisStrukturAsync_WirftArgumentException_BeiRelativemPfad()
    {
        var akt = () => _sut.ErstelleArbeitsverzeichnisStrukturAsync("relativer/pfad");

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>InitialisiereAsync lehnt einen ungültigen ProjektBranchName mit ArgumentException ab.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftArgumentException_BeiUngueltigemProjektBranchName()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot) with { ProjektBranchName = "ungueltig~branch" };

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>InitialisiereAsync lehnt einen zu kurzen InitialPrompt mit ArgumentException ab.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftArgumentException_BeiZuKurzemInitialPrompt()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot) with { InitialPrompt = "kurz" };

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>InitialisiereAsync lehnt ein ungültiges LaufzeitLimitMinuten mit ArgumentException ab.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftArgumentException_BeiUngueltigemLaufzeitLimit()
    {
        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var basisAnfrage = ErstelleAnfrage(_testRoot);
        var anfrage = basisAnfrage with { RessourcenLimits = basisAnfrage.RessourcenLimits with { LaufzeitLimitMinuten = 5 } };

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>InitialisiereAsync wirft eine InvalidOperationException, wenn die Aufgabe kein verknüpftes GitRepository mit RepositoryUrl besitzt.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftInvalidOperationException_OhneGitRepository()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Testaufgabe ohne Repository",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            GitRepository = null
        };
        _db.Aufgaben.Add(aufgabe);
        await _db.SaveChangesAsync();
        var anfrage = ErstelleAnfrage(_testRoot);

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>InitialisiereAsync wirft eine InvalidOperationException, wenn der Repository-Klon fehlschlägt.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftInvalidOperationException_BeiFehlgeschlagenemGitKlon()
    {
        _gitPluginMock
            .Setup(p => p.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fatal: Klon fehlgeschlagen"));

        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }
}
