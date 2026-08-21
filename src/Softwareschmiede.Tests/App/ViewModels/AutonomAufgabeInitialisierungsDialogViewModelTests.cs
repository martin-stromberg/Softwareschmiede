using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für den AutonomAufgabeInitialisierungsDialogViewModel.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AutonomAufgabenInitialisierungsService _initialisierungsService;
    private readonly AutonomAufgabeInitialisierungsDialogViewModel _sut;
    private readonly string _testRoot;
    private readonly Aufgabe _aufgabe;
    private bool? _closeRequestedResult;

    /// <summary>AutonomAufgabeInitialisierungsDialogViewModelTests.</summary>
    public AutonomAufgabeInitialisierungsDialogViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _testRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "InitDialogVm", Guid.NewGuid().ToString("N"));
        var quellRepo = _testRoot + "-quelle";
        Directory.CreateDirectory(quellRepo);

        var cliRunnerMock = new Mock<ICliRunner>();
        cliRunnerMock
            .Setup(r => r.RunAsync("git", It.Is<IEnumerable<string>>(a => a.Contains("clone")), It.IsAny<string?>(), It.IsAny<IDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, string?, IDictionary<string, string>?, CancellationToken>((_, args, _, _, _) => Directory.CreateDirectory(args.Last()))
            .ReturnsAsync(new CliResult(0, string.Empty, string.Empty));

        _initialisierungsService = new AutonomAufgabenInitialisierungsService(_db, cliRunnerMock.Object, Options.Create(new AutonomAufgabenOptions()), NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

        var projektId = Guid.NewGuid();
        _db.Projekte.Add(new Projekt { Id = projektId, Name = "Testprojekt", ErstellungsDatum = DateTimeOffset.UtcNow, Status = ProjektStatus.Aktiv });
        _aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Testaufgabe",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = quellRepo,
            BranchName = "main"
        };
        _db.Aufgaben.Add(_aufgabe);
        _db.SaveChanges();

        _sut = new AutonomAufgabeInitialisierungsDialogViewModel(_initialisierungsService, NullLogger<AutonomAufgabeInitialisierungsDialogViewModel>.Instance);
        _sut.Initialize(_aufgabe);
        _sut.CloseRequested += (_, result) => _closeRequestedResult = result;
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }

        if (Directory.Exists(_testRoot + "-quelle"))
        {
            Directory.Delete(_testRoot + "-quelle", recursive: true);
        }
    }

    /// <summary>BestaetigenAsync validiert die Eingaben und ruft AutonomAufgabenInitialisierungsService.InitialisiereAsync auf, wenn alle Werte gültig sind.</summary>
    [Fact]
    public async Task BestaetigenAsync_ValidatesInputsAndCallsService()
    {
        _sut.InitialPrompt = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";
        _sut.TokenBudget = 500000;
        _sut.RuntimeLimitMinutes = 480;

        await _sut.BestaetigenAsync();

        _sut.ErrorMessage.Should().BeNull();
        _sut.ErstellteKonfiguration.Should().NotBeNull();
        _closeRequestedResult.Should().BeTrue();

        var aufgabeAktualisiert = await _db.Aufgaben.FindAsync(_aufgabe.Id);
        aufgabeAktualisiert!.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.AutonomAufgabe);
    }

    /// <summary>BestaetigenAsync schlägt bei einem ungültigen Token-Budget mit einer Fehlermeldung fehl, ohne den Service aufzurufen.</summary>
    [Fact]
    public async Task BestaetigenAsync_FailsOnInvalidTokenBudget()
    {
        _sut.InitialPrompt = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";
        _sut.TokenBudget = 0;
        _sut.RuntimeLimitMinutes = 480;

        await _sut.BestaetigenAsync();

        _sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _sut.ErstellteKonfiguration.Should().BeNull();
        _closeRequestedResult.Should().BeNull();

        var aufgabeUnveraendert = await _db.Aufgaben.FindAsync(_aufgabe.Id);
        aufgabeUnveraendert!.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.NichtGestartet);
    }

    /// <summary>Abbrechen schließt den Dialog, ohne den Service aufzurufen.</summary>
    [Fact]
    public void Abbrechen_ClosesDialog()
    {
        _sut.Abbrechen();

        _closeRequestedResult.Should().BeFalse();
        _sut.ErstellteKonfiguration.Should().BeNull();
    }
}
