using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die Projektbranch-Auswahl/-Anlage und die Promptvorlagen-Auswahl in <see cref="AutonomAufgabeInitialisierungsDialogViewModel"/>.</summary>
public sealed class AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<IPluginManager> _pluginManagerMock = new();
    private readonly Mock<IGitPlugin> _gitPluginMock;
    private readonly Aufgabe _aufgabe;

    /// <summary>AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen.</summary>
    public AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen()
    {
        _db = TestDbContextFactory.Create();

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
            LokalerKlonPfad = @"C:\temp\klon",
            BranchName = "main",
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjektId = projektId,
                PluginTyp = "GitHub",
                RepositoryUrl = "https://example.com/repo.git",
                RepositoryName = "repo"
            }
        };
        _db.Aufgaben.Add(_aufgabe);
        _db.SaveChanges();

        _gitPluginMock = new Mock<IGitPlugin>();
        _gitPluginMock.Setup(p => p.PluginPrefix).Returns("GitHub");
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([_gitPluginMock.Object]);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private AutonomAufgabeInitialisierungsDialogViewModel CreateSut(PromptVorlagenService? promptVorlagenService = null, PromptVorlagenPlatzhalterService? platzhalterService = null)
    {
        var initialisierungsService = new AutonomAufgabenInitialisierungsService(
            _db,
            Mock.Of<ICliRunner>(),
            Options.Create(new AutonomAufgabenOptions()),
            NullLogger<AutonomAufgabenInitialisierungsService>.Instance);

        var sut = new AutonomAufgabeInitialisierungsDialogViewModel(
            initialisierungsService,
            Options.Create(new AutonomAufgabenOptions()),
            NullLogger<AutonomAufgabeInitialisierungsDialogViewModel>.Instance,
            _pluginManagerMock.Object,
            promptVorlagenService ?? new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance),
            platzhalterService ?? new PromptVorlagenPlatzhalterService());
        sut.Initialize(_aufgabe);
        return sut;
    }

    /// <summary>LadeAsync lädt die verfügbaren Remote-Branches des Repositories der Aufgabe.</summary>
    [Fact]
    public async Task LadeAsync_LaedtVerfuegbareProjektbranches()
    {
        _gitPluginMock
            .Setup(p => p.GetRemoteBranchesAsync("https://example.com/repo.git", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["main", "develop"]);
        var sut = CreateSut();

        await sut.LadeAsync();

        sut.AvailableProjectBranches.Should().Contain(["main", "develop"]);
        sut.IsProjectBranchManualInput.Should().BeFalse();
    }

    /// <summary>Wenn kein passendes Git-Plugin gefunden werden kann, fällt die Branch-Auswahl auf manuelle Eingabe zurück.</summary>
    [Fact]
    public async Task LadeAsync_FaelltAufManuelleEingabeZurueck_WennKeinPluginGefundenWird()
    {
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        var sut = CreateSut();

        await sut.LadeAsync();

        sut.IsProjectBranchManualInput.Should().BeTrue();
    }

    /// <summary>CreateBranchCommand legt über das Git-Plugin einen neuen Branch an und wählt ihn aus.</summary>
    [Fact]
    public async Task CreateBranchCommand_LegtBranchAnUndWaehltIhnAus()
    {
        _gitPluginMock
            .Setup(p => p.CreateBranchAsync(_aufgabe.LokalerKlonPfad!, "feature-neu", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.ShowCreateBranchCommand.Execute(null);
        sut.NewBranchName = "feature-neu";

        await ((AsyncRelayCommand)sut.CreateBranchCommand).ExecuteAsync();

        sut.SelectedProjectBranch.Should().Be("feature-neu");
        sut.AvailableProjectBranches.Should().Contain("feature-neu");
        sut.IsCreatingBranch.Should().BeFalse();
        sut.NewBranchError.Should().BeNull();
        _gitPluginMock.Verify(p => p.CreateBranchAsync(_aufgabe.LokalerKlonPfad!, "feature-neu", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CreateBranchCommand setzt NewBranchError, wenn die Aufgabe keinen lokalen Klon besitzt.</summary>
    [Fact]
    public async Task CreateBranchCommand_SetztFehler_WennKeinLokalerKlonVorhanden()
    {
        _aufgabe.LokalerKlonPfad = null;
        var sut = CreateSut();
        sut.ShowCreateBranchCommand.Execute(null);
        sut.NewBranchName = "feature-neu";

        await ((AsyncRelayCommand)sut.CreateBranchCommand).ExecuteAsync();

        sut.NewBranchError.Should().NotBeNullOrWhiteSpace();
        sut.IsCreatingBranch.Should().BeTrue();
        _gitPluginMock.Verify(p => p.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Auswahl einer Promptvorlage befüllt InitialPrompt mit dem aufgelösten Vorlagentext.</summary>
    [Fact]
    public void SelectedInitialPromptVorlage_BefuelltInitialPrompt()
    {
        var platzhalterService = new PromptVorlagenPlatzhalterService();
        var sut = CreateSut(platzhalterService: platzhalterService);
        var vorlage = new PromptVorlage { Id = Guid.NewGuid(), Name = "Start", Prompttext = "Aufgabe: %TaskName%" };

        sut.SelectedInitialPromptVorlage = vorlage;

        sut.InitialPrompt.Should().Be("Aufgabe: Testaufgabe");
    }
}
