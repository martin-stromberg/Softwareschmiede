using System.Reflection;
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

/// <summary>Unit-Tests für die Pausen-Funktion von TaskDetailViewModel (Issue 151).</summary>
public sealed class TaskDetailViewModelTests_Pause : IDisposable
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
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly WorkItemRunDataChangedNotifier _runDataChangedNotifier;
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_Pause.</summary>
    public TaskDetailViewModelTests_Pause()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aiService = TestAiExecutionServiceFactory.Create();
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        _appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(_appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);

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
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(PauseWorkItemDialogViewModel)))
            .Returns(new PauseWorkItemDialogViewModel());
        _runDataChangedNotifier = new WorkItemRunDataChangedNotifier();

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
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
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);

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
            _serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            TaskDetailViewModelTestFactory.CreateStub(),
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            TaskDetailViewModelTestFactory.CreateWorkingDirectoryOpenService(),
            runDataChangedNotifier: _runDataChangedNotifier);
    }

    private async Task<TaskDetailViewModel> LoadSutAsync(WorkItem workItem)
    {
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        return sut;
    }

    private async Task<WorkItem> CreateWorkItemAsync(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    private async Task<WorkItem> CreateWorkItemWithExecutionStatusAsync(WorkItemStatus status, WorkItemExecutionStatus executionStatus)
    {
        var workItem = await CreateWorkItemAsync(status);
        var tracked = await _db.WorkItems.FindAsync(workItem.Id);
        tracked!.ExecutionStatus = executionStatus;
        await _db.SaveChangesAsync();
        return tracked;
    }

    private static void SetIsCliRunning(TaskDetailViewModel sut, bool value)
        => typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, value);

    /// <summary>KannPausieren ist für die pausierbaren Stati Neu, Gestartet und Wartend true.</summary>
    [Theory]
    [InlineData(WorkItemStatus.New)]
    [InlineData(WorkItemStatus.Started)]
    [InlineData(WorkItemStatus.Waiting)]
    public async Task PauseCan_ShouldBeTrue_ForPausableStates(WorkItemStatus status)
    {
        var workItem = await CreateWorkItemAsync(status);
        var sut = await LoadSutAsync(workItem);

        sut.PauseCan.Should().BeTrue();
        sut.SetPauseCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>KannPausieren ist für beendete Aufgaben false.</summary>
    [Fact]
    public async Task PauseCan_ShouldBeFalse_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Completed);
        var sut = await LoadSutAsync(workItem);

        sut.PauseCan.Should().BeFalse();
        sut.SetPauseCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IsPaused und PauseDisplayText spiegeln den geladenen PausiertBisUtc-Wert wider.</summary>
    [Fact]
    public async Task IsPaused_AndPauseDisplayText_ReflectLoadedValue()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        await _workItemService.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);
        var sut = await LoadSutAsync(workItem);

        sut.IsPaused.Should().BeTrue();
        sut.PausedUntilUtc.Should().NotBeNull();
        sut.PauseDisplayText.Should().StartWith("⏸ Pausiert bis ");
    }

    /// <summary>Bei aktiver Pause darf der Start-Command nicht ausführbar sein (Issue 151).</summary>
    [Fact]
    public async Task StartCommand_CanExecuteFalse_WhenWorkItemPaused()
    {
        var workItem = await CreateWorkItemAsync(WorkItemStatus.New);
        var sut = await LoadSutAsync(workItem);
        sut.StartCommand.CanExecute(null).Should().BeTrue("ohne Pause ist der Start erlaubt");

        await _workItemService.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCommand.CanExecute(null).Should().BeFalse("eine pausierte Aufgabe darf nicht gestartet werden");
    }

    /// <summary>Bei aktiver Pause darf der CLI-Neustart nicht ausführbar sein, obwohl der Ausführungsstatus ihn sonst erlauben würde (Issue 151).</summary>
    [Fact]
    public async Task StartCanCliNew_False_WhenWorkItemPaused()
    {
        var workItem = await CreateWorkItemWithExecutionStatusAsync(WorkItemStatus.Started, WorkItemExecutionStatus.Completed);
        var sut = await LoadSutAsync(workItem);
        sut.StartCanCliNew.Should().BeTrue("ohne Pause ist der CLI-Neustart bei beendeter Ausführung erlaubt");

        await _workItemService.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCanCliNew.Should().BeFalse("eine pausierte Aufgabe darf nicht neu gestartet werden");
        sut.RestartCliCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Bei aktiver Pause sind KannPromptVorlageSenden und KannPromptPlanen false — trotz laufender CLI, vorhandener Vorlagen und ausgefüllter Zeitfelder (Issue 151).</summary>
    [Fact]
    public async Task PromptGuards_False_WhenWorkItemPaused()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = await LoadSutAsync(workItem);

        // Vorbedingung: Ohne Pause sind die Guards bei laufender CLI + Vorlage + Zeitangabe erfüllt.
        SetIsCliRunning(sut, true);
        sut.ScheduledPromptTargetMinutes = 30;
        sut.SelectedPromptTemplate = template;
        sut.SendCanPromptTemplate.Should().BeTrue();
        sut.CanPromptPlan.Should().BeTrue();

        await _workItemService.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // LadenAsync setzt IsCliRunning aus dem KiService neu — für die Guard-Prüfung erneut
        // simulieren, ebenso die durch das Neuladen zurückgesetzten Eingaben.
        SetIsCliRunning(sut, true);
        sut.ScheduledPromptTargetMinutes = 30;
        sut.SelectedPromptTemplate = template;

        sut.SendCanPromptTemplate.Should().BeFalse("bei aktiver Pause darf keine Promptvorlage gesendet werden");
        sut.CanPromptPlan.Should().BeFalse("bei aktiver Pause darf kein Prompt geplant werden");
    }

    /// <summary>PauseEinstellenCommand ruft den Dialog auf, persistiert das Ergebnis via SetPauseAsync und meldet die Laufdaten-Änderung.</summary>
    [Fact]
    public async Task SetPauseCommand_SetsPause_OverDialogAndService()
    {
        var pausedUntil = DateTimeOffset.UtcNow.AddHours(2);
        _dialogServiceMock
            .Setup(d => d.ShowWorkItemPauseDialogAsync(It.IsAny<PauseWorkItemDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PauseWorkItemResult(pausedUntil, Lift: false));
        var notified = new List<Guid>();
        _runDataChangedNotifier.RunDataChanged += id => notified.Add(id);

        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = await LoadSutAsync(workItem);

        await ((AsyncRelayCommand)sut.SetPauseCommand).ExecuteAsync();

        var loaded = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.PausedUntilUtc.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(5));
        sut.IsPaused.Should().BeTrue();
        notified.Should().Contain(workItem.Id);
        _dialogServiceMock.Verify(
            d => d.ShowWorkItemPauseDialogAsync(It.IsAny<PauseWorkItemDialogViewModel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>PauseEinstellenCommand mit Aufheben-Ergebnis leert PausiertBisUtc.</summary>
    [Fact]
    public async Task SetPauseCommand_RaisesPauseOn_AtLiftResult()
    {
        _dialogServiceMock
            .Setup(d => d.ShowWorkItemPauseDialogAsync(It.IsAny<PauseWorkItemDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PauseWorkItemResult(null, Lift: true));

        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        await _workItemService.SetPauseAsync(workItem.Id, DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        var sut = await LoadSutAsync(workItem);
        sut.IsPaused.Should().BeTrue();

        await ((AsyncRelayCommand)sut.SetPauseCommand).ExecuteAsync();

        var loaded = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        loaded!.PausedUntilUtc.Should().BeNull();
        sut.IsPaused.Should().BeFalse();
        sut.PauseDisplayText.Should().BeNull();
    }

    /// <summary>PauseEinstellenCommand ändert nichts, wenn der Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task SetPauseCommand_ChangesNothing_AtCancel()
    {
        _dialogServiceMock
            .Setup(d => d.ShowWorkItemPauseDialogAsync(It.IsAny<PauseWorkItemDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PauseWorkItemResult?)null);

        var workItem = await CreateWorkItemAsync(WorkItemStatus.Started);
        var sut = await LoadSutAsync(workItem);

        await ((AsyncRelayCommand)sut.SetPauseCommand).ExecuteAsync();

        (await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken))!.PausedUntilUtc.Should().BeNull();
    }
}
