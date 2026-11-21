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
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly TodoService _todoService;
    private readonly AiExecutionService _aiService;
    private readonly DevelopmentProcessService _developmentprocessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptTemplateService _promptTemplatesService;
    private readonly PromptTemplatePlaceholderService _promptTemplatesPlaceholderService = new();
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly AppSettingService _appSettingService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ICliReplayExportService> _exportServiceMock = new();
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>Initialisiert die Testinfrastruktur für CLI-Replay-Export-Tests.</summary>
    public TaskDetailViewModelTests_CliReplayExport()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aiService = TestAiExecutionServiceFactory.Create();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(_appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var workingDirectoryMock = new Mock<IWorkingDirectoryResolver>();
        _developmentprocessService = new DevelopmentProcessService(
            _workItemService,
            _logService,
            gitPluginMock.Object,
            _pluginSelectionService,
            workingDirectoryMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _aiService),
            NullLogger<DevelopmentProcessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();

        _exportServiceMock.Setup(s => s.HasRecording(It.IsAny<Guid>())).Returns(true);
    }

    /// <summary>Räumt Testressourcen und Hintergrunddienste auf.</summary>
    public void Dispose()
    {
        _aiService.Dispose();
        _db.Dispose();
    }

    private TaskDetailViewModel CreateSut()
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();
        var workingDirectoryOpenService = TaskDetailViewModelTestFactory.CreateWorkingDirectoryOpenService();
        var serviceProvider = new Mock<IServiceProvider>().Object;

        return new TaskDetailViewModel(
            _workItemService,
            _logService,
            _aiService,
            _developmentprocessService,
            _pluginSelectionService,
            _promptTemplatesService,
            _promptTemplatesPlaceholderService,
            _promptScheduleDispatchService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProvider,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            workingDirectoryOpenService,
            cliReplayExportService: _exportServiceMock.Object);
    }

    private async Task<WorkItem> CreateWorkItemAsync(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Replayexport-Aufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
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
    public async Task ExportCliReplayCommand_CanExecute_WhenWorkItemLoaded()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ExportCanCliReplay.Should().BeTrue();
        sut.ExportCliReplayCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Beim Abbruch des Save-Dialogs wird kein Export durchgeführt und keine Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldAbort_WhenDialogCancelled()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetupSaveDialog(null);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Der Export ruft den Service mit der Aufgaben-ID und dem gewählten Pfad auf.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_CallsExportServiceWithPath()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var targetPath = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(targetPath);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(workItem.Id, targetPath, It.IsAny<CancellationToken>()),
            Times.Once);
        sut.ErrorMessage.Should().BeNull();
    }

    /// <summary>Ein Export-Zielpfad ohne .clireplay-Endung wird abgelehnt.</summary>
    [Fact]
    public async Task ExportCliReplayAsync_ShouldSetErrorMessage_WhenTargetPathIsNotCliReplay()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetupSaveDialog(Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.txt"));

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Be("Export-Zielpfad muss auf .clireplay enden.");
        _exportServiceMock.Verify(
            s => s.ExportCliReplayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Liegt keine Aufzeichnung vor, wird der Speicherdialog gar nicht erst geöffnet —
    /// die Fehlermeldung erscheint direkt (keine umsonst investierte Dateiauswahl).</summary>
    [Fact]
    public async Task ExportCliReplayAsync_WithoutRecording_ShowsErrorWithoutSavedialog()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        _exportServiceMock.Setup(s => s.HasRecording(workItem.Id)).Returns(false);

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("keine Aufzeichnung");
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
    public async Task ExportCliReplayAsync_ShouldSetErrorMessage_WhenServiceFails()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        var targetPath = Path.Combine(Path.GetTempPath(), $"cli-replay-{Guid.NewGuid():N}.clireplay");
        SetupSaveDialog(targetPath);
        _exportServiceMock
            .Setup(s => s.ExportCliReplayAsync(workItem.Id, targetPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Für diese Aufgabe liegt keine Aufzeichnung vor."));

        await ((AsyncRelayCommand)sut.ExportCliReplayCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        sut.ErrorMessage.Should().Contain("CLI-Aufzeichnung konnte nicht exportiert werden");
    }
}
