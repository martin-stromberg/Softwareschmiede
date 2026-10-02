using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für den CLI-Replay-Export (.clireplay) von TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests_CliReplayExport : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly TodoService _todoService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly AppEinstellungService _appEinstellungService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ICliReplayExportService> _exportServiceMock = new();
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>Initialisiert die Testinfrastruktur für CLI-Replay-Export-Tests.</summary>
    public TaskDetailViewModelTests_CliReplayExport()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _kiService = TestKiAusfuehrungsServiceFactory.Create();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _appEinstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(_appEinstellungService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        _entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            _pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: _kiService),
            NullLogger<EntwicklungsprozessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();

        _exportServiceMock.Setup(s => s.HatAufzeichnung(It.IsAny<Guid>())).Returns(true);
    }

    /// <summary>Räumt Testressourcen und Hintergrunddienste auf.</summary>
    public void Dispose()
    {
        _kiService.Dispose();
        _db.Dispose();
    }

    private TaskDetailViewModel CreateSut()
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();
        var arbeitsverzeichnisOeffnenService = TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService();
        var serviceProvider = TaskDetailViewModelTestFactory.CreateDefaultServiceProvider(_db, _kiService);
        var autonomAufgabeStartService = TaskDetailViewModelTestFactory.CreateAutonomAufgabeStartService(
            serviceProvider,
            _dialogServiceMock.Object,
            _aufgabeService,
            _db,
            appEinstellungService: _appEinstellungService);

        return new TaskDetailViewModel(
            _aufgabeService,
            _protokollService,
            _kiService,
            _entwicklungsprozessService,
            _pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptZeitVersandService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProvider,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService,
            autonomAufgabeStartService,
            _appEinstellungService,
            Options.Create(new AutonomAufgabenOptions()),
            cliReplayExportService: _exportServiceMock.Object);
    }

    private async Task<Aufgabe> ErstelleAufgabeAsync(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Replayexport-Aufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
    }

    private void SetupSaveDialog(string? pfad)
    {
        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pfad);
    }

    /// <summary>Der Replay-Export-Command ist ausführbar, sobald eine Aufgabe geladen wurde.</summary>
    [Fact]
    public async Task ExportCliReplayCommand_CanExecute_WhenAufgabeGeladen()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannCliReplayExportieren.Should().BeTrue();
        sut.ExportCliReplayCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Beim Abbruch des Save-Dialogs wird kein Export durchgeführt und keine Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldAbort_WhenDialogCancelled()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetupSaveDialog(null);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Der Export ruft den Service mit der Aufgaben-ID und dem gewählten Pfad auf.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_RuftExportServiceMitPfad()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var zielPfad = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(zielPfad);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(aufgabe.Id, zielPfad, It.IsAny<CancellationToken>()),
            Times.Once);
        sut.FehlerMeldung.Should().BeNull();
    }

    /// <summary>Ein Export-Zielpfad ohne .clireplay-Endung wird abgelehnt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldSetFehlerMeldung_WhenTargetPathIsNotCliReplay()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetupSaveDialog(Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.txt"));

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Be("Export-Zielpfad muss auf .clireplay enden.");
        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Liegt keine Aufzeichnung vor, wird der Speicherdialog gar nicht erst geöffnet —
    /// die Fehlermeldung erscheint direkt (keine umsonst investierte Dateiauswahl).</summary>
    [Fact]
    public async Task ExportCliReplayAsync_OhneAufzeichnung_ZeigtFehlerOhneSpeicherdialog()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        _exportServiceMock.Setup(s => s.HatAufzeichnung(aufgabe.Id)).Returns(false);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("keine Aufzeichnung");
        _dialogServiceMock.Verify(
            d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Fehlt die Aufzeichnung (Service wirft), wird die Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldSetFehlerMeldung_WhenServiceFails()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var zielPfad = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(zielPfad);
        _exportServiceMock
            .Setup(s => s.ExportCliReplayAsync(aufgabe.Id, zielPfad, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Für diese Aufgabe liegt keine Aufzeichnung vor."));

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
        sut.FehlerMeldung.Should().Contain("CLI-Aufzeichnung konnte nicht exportiert werden");
    }
}
