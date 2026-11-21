using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für den zeitgesteuerten Prompt-Versand von TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests_ScheduledPrompt : IDisposable
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
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_ZeitgesteuerterPrompt.</summary>
    public TaskDetailViewModelTests_ScheduledPrompt()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _aiService = TestAiExecutionServiceFactory.Create(scopeFactoryMock.Object);
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, _timeProvider, NullLogger<PromptScheduleDispatchService>.Instance);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
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

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();

        var workingDirectoryOpenService = TaskDetailViewModelTestFactory.CreateWorkingDirectoryOpenService();
        var serviceProviderMock = new Mock<IServiceProvider>();

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
            serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            _timeProvider,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            workingDirectoryOpenService);
    }

    private async Task<WorkItem> CreateWorkItem(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    private static void SetIsCliRunning(TaskDetailViewModel sut, bool value)
        => typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, value);

    /// <summary>ScheduledPromptTargetHours und ScheduledPromptTargetMinutes feuern PropertyChanged und speichern die gesetzten Werte.</summary>
    [Fact]
    public async Task ScheduledPromptTargetHours_Binding_SetsProperty()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? string.Empty);

        sut.ScheduledPromptTargetHours = 14;
        sut.ScheduledPromptTargetMinutes = 30;

        sut.ScheduledPromptTargetHours.Should().Be(14);
        sut.ScheduledPromptTargetMinutes.Should().Be(30);
        changedProperties.Should().Contain(nameof(TaskDetailViewModel.ScheduledPromptTargetHours));
        changedProperties.Should().Contain(nameof(TaskDetailViewModel.ScheduledPromptTargetMinutes));
    }

    /// <summary>Sind beide Zeitfelder leer, ist CanPromptPlan false und der Sofortversand über die ComboBox bleibt unverändert möglich.</summary>
    [Fact]
    public async Task SchedulePrompt_EmptyField_NoScheduling()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetIsCliRunning(sut, true);

        sut.SelectedPromptTemplate = template;

        sut.CanPromptPlan.Should().BeFalse("beide Zeitfelder sind leer");
        sut.SchedulePromptCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Eine gültige Zeit mit gewählter Vorlage ruft SchedulePromptAsync des Service auf und setzt den Wartestellung-Status.</summary>
    [Fact]
    public async Task SchedulePrompt_ValidTime_CallsServiceOn()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetIsCliRunning(sut, true);

        // Die Produktivlogik liest die aktuelle Zeit über den injizierten TimeProvider, hier den
        // FakeTimeProvider dieser Testklasse mit fest verdrahteter, nie voranschreitender Uhrzeit
        // (siehe _timeProvider-Feld). Damit ist die Zielzeitberechnung unabhängig von der realen
        // Wanduhrzeit deterministisch, insbesondere ohne die vormals nötige Mitternachts-Sonderbehandlung.
        var targettime = _timeProvider.GetLocalNow().AddMinutes(2);
        sut.ScheduledPromptTargetHours = targettime.Hour;
        sut.ScheduledPromptTargetMinutes = targettime.Minute;
        sut.SelectedPromptTemplate = template;

        await ((AsyncRelayCommand)sut.SchedulePromptCommand).ExecuteAsync();

        sut.ScheduledPromptStatus.Should().Be("Prompt in Wartestellung");
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull(
            "SchedulePromptAsync des Service muss für die Aufgabe einen Eintrag angelegt haben");
        sut.ScheduledPromptTargetHours.Should().BeNull("die Zeitfelder werden nach dem Planen geleert");
        sut.ScheduledPromptTargetMinutes.Should().BeNull("die Zeitfelder werden nach dem Planen geleert");
    }

    /// <summary>Eine ungültige Stunde (z. B. 25) setzt eine Fehlermeldung und plant keinen Versand.</summary>
    [Fact]
    public async Task SchedulePrompt_InvalidHour_SetsErrorMessage()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetIsCliRunning(sut, true);

        sut.ScheduledPromptTargetHours = 25;
        sut.ScheduledPromptTargetMinutes = 0;
        sut.SelectedPromptTemplate = template;

        await ((AsyncRelayCommand)sut.SchedulePromptCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Be("Ungültige Stunde (0–23)");
        sut.ScheduledPromptStatus.Should().BeNull();
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().BeNull();
    }

    /// <summary>Dispose lässt einen für die Aufgabe geplanten Prompt bestehen — der persistierte Plan überlebt das Verlassen der Detailansicht.</summary>
    [Fact]
    public async Task Dispose_LetsPlannedPlanConsist()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull();

        sut.Dispose();

        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull(
            "Dispose darf den geplanten Prompt für die Aufgabe nicht mehr stornieren");
    }

    /// <summary>Stoppt der CLI-Prozess (OnCliProcessStatusChanged mit Status ungleich Gestartet), bleibt der geplante Prompt bestehen und wartet auf die nächste Session.</summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_CliStopped_KeepsPlannedPrompt()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull();

        var method = typeof(TaskDetailViewModel).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(sut, new object[] { workItem.Id, CliProcessStatus.Stopped });

        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().NotBeNull(
            "ein CLI-Stopp darf den persistierten Plan nicht mehr stornieren");
    }

    /// <summary>Beim (Wieder-)Öffnen der Detailansicht wird die Statusanzeige aus dem Service-Zustand befüllt.</summary>
    [Fact]
    public async Task LoadAsync_WithPlannedPrompt_ShowsQueueStatus()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var targettime = _timeProvider.GetUtcNow().AddMinutes(5);
        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", targettime);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScheduledPromptStatus.Should().Be("Prompt in Wartestellung");
        sut.ScheduledPromptTimeDisplay.Should().Be(targettime.ToLocalTime().ToString("HH:mm"));
    }

    /// <summary>Ein Plan im Zustand „wartet auf Session" (Zielzeit verstrichen ohne CLI-Session) zeigt einen verständlichen Hinweis statt einer vergangenen Uhrzeit.</summary>
    [Fact]
    public async Task LoadAsync_WaitsWithPlanTheOnSession_ShowsCliStartNotice()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        // Vergangene Zielzeit + keine CLI-Session → Sofortversand schlägt fehl → Plan wartet auf Session.
        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(-1));
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id)!.WaitingForSession.Should().BeTrue();

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ScheduledPromptStatus.Should().Be("Prompt wartet auf CLI-Start");
        sut.ScheduledPromptTimeDisplay.Should().BeNull(
            "eine vergangene Uhrzeit würde den noch gültigen Plan als verfallen oder defekt wirken lassen");
    }

    /// <summary>Das ScheduledPromptChanged-Event des Service synchronisiert die Anzeige: externes Storno leert sie, ein neuer Plan setzt sie.</summary>
    [Fact]
    public async Task ScheduledPromptChanged_UpdatesDisplay()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        sut.ScheduledPromptStatus.Should().Be("Prompt in Wartestellung",
            "das Planen muss die Anzeige über ScheduledPromptChanged setzen");

        await _promptScheduleDispatchService.CancelScheduledPromptAsync(workItem.Id);
        sut.ScheduledPromptStatus.Should().BeNull(
            "ein externes Storno muss die Anzeige über ScheduledPromptChanged leeren");
        sut.ScheduledPromptTimeDisplay.Should().BeNull();
    }

    /// <summary>Das CancelScheduledPromptCommand ruft CancelScheduledPromptAsync; danach sind Anzeige und Plan entfernt.</summary>
    [Fact]
    public async Task CancelScheduledPromptCommand_RemovesPlanAndDisplay()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        sut.ScheduledPromptStatus.Should().Be("Prompt in Wartestellung");

        await ((AsyncRelayCommand)sut.CancelScheduledPromptCommand).ExecuteAsync();

        sut.ScheduledPromptStatus.Should().BeNull();
        sut.ScheduledPromptTimeDisplay.Should().BeNull();
        _promptScheduleDispatchService.GetScheduledPromptStatus(workItem.Id).Should().BeNull();
    }

    /// <summary>CanPromptPlan hängt auch von IsCliRunning ab; ein Wechsel des CLI-Laufstatus muss daher PropertyChanged für CanPromptPlan auslösen.</summary>
    [Fact]
    public async Task IsCliRunning_Changes_TriggersCanPromptPlanPropertyChangedOut()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? string.Empty);

        var method = typeof(TaskDetailViewModel).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(sut, new object[] { workItem.Id, CliProcessStatus.Started });

        changedProperties.Should().Contain(nameof(TaskDetailViewModel.CanPromptPlan));
    }

    /// <summary>CanPromptPlan hängt auch von SelectedPromptVorlage ab; eine geänderte Vorlagenauswahl muss daher PropertyChanged für CanPromptPlan auslösen.</summary>
    [Fact]
    public async Task SelectedPromptTemplate_Changes_TriggersCanPromptPlanPropertyChangedOut()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        SetIsCliRunning(sut, true);

        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? string.Empty);

        sut.SelectedPromptTemplate = template;

        changedProperties.Should().Contain(nameof(TaskDetailViewModel.CanPromptPlan));
    }
}
