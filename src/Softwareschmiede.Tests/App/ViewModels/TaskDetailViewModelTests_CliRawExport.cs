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
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für den CLI-Rohausgabe-Export von TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests_CliRawExport : IDisposable
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
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>Initialisiert die Testinfrastruktur für CLI-Raw-Export-Tests.</summary>
    public TaskDetailViewModelTests_CliRawExport()
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
            Options.Create(new AutonomAufgabenOptions()));
    }

    private async Task<Aufgabe> ErstelleAufgabeAsync(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Exportaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
    }

    /// <summary>Der Export-Command ist ausführbar, sobald eine Aufgabe geladen wurde.</summary>
    [Fact]
    public async Task ExportCliRawCommand_CanExecute_WhenAufgabeGeladen()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannCliRawExportieren.Should().BeTrue();
        sut.ExportCliRawCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Beim Abbruch des Save-Dialogs wird kein Export durchgeführt und keine Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliRawAsync_ShouldAbort_WhenDialogCancelled()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _protokollService.AddCliOutputAsync(aufgabe.Id, "cli-output-1");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await ((AsyncRelayCommand)sut.ExportCliRawCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
    }

    /// <summary>Der Export enthält ausschließlich <c>CliOutput</c>-Zeilen in chronologischer Reihenfolge.</summary>
    [Fact]
    public async Task ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "prompt-1");
        await Task.Delay(5);
        await _protokollService.AddCliOutputAsync(aufgabe.Id, "cli-1");
        await Task.Delay(5);
        await _protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.SystemMeldung, "sys-1");
        await Task.Delay(5);
        await _protokollService.AddCliOutputAsync(aufgabe.Id, "cli-2");

        var exportPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.raw");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportPfad);

        try
        {
            await ((AsyncRelayCommand)sut.ExportCliRawCommand).ExecuteAsync();

            File.Exists(exportPfad).Should().BeTrue();
            var inhalt = await File.ReadAllTextAsync(exportPfad);
            inhalt.Should().Be($"cli-1{Environment.NewLine}cli-2");
        }
        finally
        {
            if (File.Exists(exportPfad))
                File.Delete(exportPfad);
        }
    }

    /// <summary>Ein ungültiger Exportpfad endet mit .raw darf nicht verarbeitet werden.</summary>
    [Fact]
    public async Task ExportCliRawAsync_ShouldSetFehlerMeldung_WhenTargetPathIsNotRaw()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _protokollService.AddCliOutputAsync(aufgabe.Id, "cli-1");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.txt"));

        await ((AsyncRelayCommand)sut.ExportCliRawCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Be("Export-Zielpfad muss auf .raw enden.");
    }

    /// <summary>Schlägt das Dateischreiben fehl, wird eine benutzerseitige Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _protokollService.AddCliOutputAsync(aufgabe.Id, "cli-1");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var verzeichnisPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-dir-{Guid.NewGuid():N}.raw");
        Directory.CreateDirectory(verzeichnisPfad);

        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(verzeichnisPfad);

        try
        {
            await ((AsyncRelayCommand)sut.ExportCliRawCommand).ExecuteAsync();

            sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
            sut.FehlerMeldung.Should().Contain("CLI-Rohausgabe konnte nicht exportiert werden");
        }
        finally
        {
            if (Directory.Exists(verzeichnisPfad))
                Directory.Delete(verzeichnisPfad, recursive: true);
        }
    }
}
