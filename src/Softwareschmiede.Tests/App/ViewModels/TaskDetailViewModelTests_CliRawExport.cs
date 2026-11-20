using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly TodoService _todoService;
    private readonly AiExecutionService _kiService;
    private readonly DevelopmentProcessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptTemplateService _promptVorlagenService;
    private readonly PromptTemplatePlaceholderService _promptVorlagenPlatzhalterService = new();
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly AppSettingService _appSettingService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>Initialisiert die Testinfrastruktur für CLI-Raw-Export-Tests.</summary>
    public TaskDetailViewModelTests_CliRawExport()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
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
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_kiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IWorkingDirectoryResolver>();
        _entwicklungsprozessService = new DevelopmentProcessService(
            _workItemService,
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
            _workItemService,
            _logService,
            _kiService,
            _entwicklungsprozessService,
            _pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptScheduleDispatchService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProvider,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService);
    }

    private async Task<WorkItem> ErstelleAufgabeAsync(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projektId, "Exportaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    /// <summary>Der Export-Command ist ausführbar, sobald eine Aufgabe geladen wurde.</summary>
    [Fact]
    public async Task ExportCliRawCommand_CanExecute_WhenAufgabeGeladen()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannCliRawExportieren.Should().BeTrue();
        sut.ExportCliRawCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Beim Abbruch des Save-Dialogs wird kein Export durchgeführt und keine Fehlermeldung gesetzt.</summary>
    [Fact]
    public async Task ExportCliRawAsync_ShouldAbort_WhenDialogCancelled()
    {
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        await _logService.AddCliOutputAsync(workItem.Id, "cli-output-1", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        await _logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "prompt-1", ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await _logService.AddCliOutputAsync(workItem.Id, "cli-1", TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await _logService.AddEntryAsync(workItem.Id, LogEntryType.SystemMessage, "sys-1", ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await _logService.AddCliOutputAsync(workItem.Id, "cli-2", TestContext.Current.CancellationToken);

        var exportPfad = Path.Combine(Path.GetTempPath(), $"cli-raw-export-{Guid.NewGuid():N}.raw");
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
            var content = await File.ReadAllTextAsync(exportPfad, TestContext.Current.CancellationToken);
            content.Should().Be($"cli-1{Environment.NewLine}cli-2");
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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        await _logService.AddCliOutputAsync(workItem.Id, "cli-1", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
        var workItem = await ErstelleAufgabeAsync(WorkItemStatus.Started);
        await _logService.AddCliOutputAsync(workItem.Id, "cli-1", TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
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
