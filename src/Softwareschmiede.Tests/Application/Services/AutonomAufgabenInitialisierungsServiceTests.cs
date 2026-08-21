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
    private readonly AutonomAufgabenInitialisierungsService _sut;
    private readonly string _testRoot;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>AutonomAufgabenInitialisierungsServiceTests.</summary>
    public AutonomAufgabenInitialisierungsServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _cliRunnerMock = new Mock<ICliRunner>();

        // Simuliert einen erfolgreichen "git clone", indem das Zielverzeichnis inklusive Marker-Datei angelegt wird.
        _cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("clone")),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) =>
            {
                var zielPfad = args.Last();
                Directory.CreateDirectory(zielPfad);
                File.WriteAllText(Path.Combine(zielPfad, ".git-marker"), "cloned");
            })
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        _sut = new AutonomAufgabenInitialisierungsService(_db, _cliRunnerMock.Object, Options.Create(new AutonomAufgabenOptions()), NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "AutonomAufgabenInit", Guid.NewGuid().ToString("N"));

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
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
        var quellRepo = Path.Combine(arbeitsverzeichnispPfad + "-quelle");
        Directory.CreateDirectory(quellRepo);

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Testaufgabe",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = quellRepo
        };
        _db.Aufgaben.Add(aufgabe);
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
        _cliRunnerMock.Verify(
            r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
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

    /// <summary>InitialisiereAsync wirft eine InvalidOperationException, wenn die Aufgabe keinen lokalen Klon-Pfad besitzt.</summary>
    [Fact]
    public async Task InitialisiereAsync_WirftInvalidOperationException_OhneLokalenKlonPfad()
    {
        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            Titel = "Autonome Testaufgabe ohne Klon",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = null
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
        _cliRunnerMock
            .Setup(r => r.RunAsync(
                "git",
                It.Is<IEnumerable<string>>(args => args.Contains("clone")),
                It.IsAny<string?>(),
                It.IsAny<IDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliResult(1, string.Empty, "fatal: Klon fehlgeschlagen"));

        var aufgabe = ErstelleUndPersistiereAufgabe(_testRoot);
        var anfrage = ErstelleAnfrage(_testRoot);

        var akt = () => _sut.InitialisiereAsync(aufgabe, anfrage);

        await akt.Should().ThrowAsync<InvalidOperationException>();
    }
}
