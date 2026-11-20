using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _aufgabeService;
    private readonly LogService _logService;
    private readonly TodoService _todoService;
    private readonly AiExecutionService _kiService;
    private readonly DevelopmentProcessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptTemplateService _promptVorlagenService;
    private readonly PromptTemplatePlaceholderService _promptVorlagenPlatzhalterService = new();
    private readonly PromptScheduleDispatchService _promptZeitVersandService;
    private readonly AppSettingService _appSettingService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ICliReplayExportService> _exportServiceMock = new();
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>Initialisiert die Testinfrastruktur für CLI-Replay-Export-Tests.</summary>
    public TaskDetailViewModelTests_CliReplayExport()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _kiService = TestKiAusfuehrungsServiceFactory.Create();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(_appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptVorlagenService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _promptZeitVersandService = new PromptScheduleDispatchService(_kiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IWorkingDirectoryResolver>();
        _entwicklungsprozessService = new DevelopmentProcessService(
            _aufgabeService,
            _logService,
            gitPluginMock.Object,
            _pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _kiService),
            NullLogger<DevelopmentProcessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
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
        var serviceProvider = new Mock<IServiceProvider>().Object;

        return new TaskDetailViewModel(
            _aufgabeService,
            _logService,
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
            cliReplayExportService: _exportServiceMock.Object);
    }

    private async Task<WorkItem> ErstelleAufgabeAsync(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _aufgabeService.CreateAsync(_projektId, "Replayexport-Aufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _aufgabeService.StatusSetzenAsync(workItem.Id, status);
        return await _aufgabeService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    private void SetupSaveDialog(string? path)
    {
        _dialogServiceMock
            .Setup(d => d.ShowSaveFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(path);
    }

    /// <summary>Der Replay-Export-Command ist ausführbar, sobald eine Aufgabe geladen wurde.</summary>
    [Fact]
    public async Task ExportCliReplayCommand_CanExecute_WhenAufgabeGeladen()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannCliReplayExportieren.Should().BeTrue();
        sut.ExportCliReplayCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Beim Abbruch des Save-Dialogs wird kein Export durchgeführt und keine Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldAbort_WhenDialogCancelled()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var targetPath = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(targetPath);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(workItem.Id, targetPath, It.IsAny<CancellationToken>()),
            Times.Once);
        sut.FehlerMeldung.Should().BeNull();
    }

    /// <summary>Ein Export-Zielpfad ohne .clireplay-Endung wird abgelehnt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldSetFehlerMeldung_WhenTargetPathIsNotCliReplay()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        _exportServiceMock.Setup(s => s.HatAufzeichnung(workItem.Id)).Returns(false);

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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        var targetPath = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(targetPath);
        _exportServiceMock
            .Setup(s => s.ExportCliReplayAsync(workItem.Id, targetPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Für diese Aufgabe liegt keine Aufzeichnung vor."));

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
        sut.FehlerMeldung.Should().Contain("CLI-Aufzeichnung konnte nicht exportiert werden");
    }
}
