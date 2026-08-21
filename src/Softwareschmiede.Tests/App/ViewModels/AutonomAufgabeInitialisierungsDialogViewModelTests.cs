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

        var cliRunnerMock = AutonomAufgabenInitialisierungsServiceTestFactory.CreateCliRunnerMockMitErfolgreichemGitKlon();
        _initialisierungsService = AutonomAufgabenInitialisierungsServiceTestFactory.CreateService(_db, cliRunnerMock.Object);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        var promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);
        var promptVorlagenPlatzhalterService = new PromptVorlagenPlatzhalterService();

        var projektId = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleProjekt(_db);
        _aufgabe = AutonomAufgabenInitialisierungsServiceTestFactory.ErstelleAufgabeMitLokalemKlon(_db, projektId, _testRoot, "Testaufgabe", "main");
        _db.SaveChanges();

        _sut = new AutonomAufgabeInitialisierungsDialogViewModel(
            _initialisierungsService,
            Options.Create(new AutonomAufgabenOptions()),
            NullLogger<AutonomAufgabeInitialisierungsDialogViewModel>.Instance,
            pluginManagerMock.Object,
            promptVorlagenService,
            promptVorlagenPlatzhalterService);
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

    /// <summary>BestaetigenAsync schlägt bei einem zu kurzen Initialprompt mit einer Fehlermeldung fehl, ohne den Service aufzurufen.</summary>
    [Fact]
    public async Task BestaetigenAsync_FailsOnInvalidInitialPrompt()
    {
        _sut.InitialPrompt = "zu kurz";
        _sut.TokenBudget = 500000;
        _sut.RuntimeLimitMinutes = 480;

        await _sut.BestaetigenAsync();

        _sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _sut.ErstellteKonfiguration.Should().BeNull();
        _closeRequestedResult.Should().BeNull();
    }

    /// <summary>BestaetigenAsync schlägt bei einer ungültigen Laufzeitbegrenzung mit einer Fehlermeldung fehl, ohne den Service aufzurufen.</summary>
    [Fact]
    public async Task BestaetigenAsync_FailsOnInvalidRuntimeLimit()
    {
        _sut.InitialPrompt = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";
        _sut.TokenBudget = 500000;
        _sut.RuntimeLimitMinutes = 5;

        await _sut.BestaetigenAsync();

        _sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _sut.ErstellteKonfiguration.Should().BeNull();
        _closeRequestedResult.Should().BeNull();
    }

    /// <summary>BestaetigenAsync setzt ErrorMessage und schließt den Dialog nicht, wenn der Initialisierungsservice fehlschlägt.</summary>
    [Fact]
    public async Task BestaetigenAsync_SetsErrorMessage_WhenServiceThrows()
    {
        var fehlerhafteAufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = _aufgabe.ProjektId,
            Titel = "Aufgabe ohne Klon",
            Status = AufgabeStatus.Neu,
            AusfuehrungsStatus = AufgabeAusfuehrungsStatus.NichtGestartet,
            ErstellungsDatum = DateTimeOffset.UtcNow,
            LokalerKlonPfad = null,
            BranchName = "main"
        };
        _db.Aufgaben.Add(fehlerhafteAufgabe);
        _db.SaveChanges();

        _sut.Initialize(fehlerhafteAufgabe);
        _sut.InitialPrompt = "Implementiere die Autonome Aufgabe vollständig gemäß Anforderung.";
        _sut.TokenBudget = 500000;
        _sut.RuntimeLimitMinutes = 480;

        await _sut.BestaetigenAsync();

        _sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _sut.ErstellteKonfiguration.Should().BeNull();
        _sut.IsSubmitting.Should().BeFalse();
        _closeRequestedResult.Should().BeNull();
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
