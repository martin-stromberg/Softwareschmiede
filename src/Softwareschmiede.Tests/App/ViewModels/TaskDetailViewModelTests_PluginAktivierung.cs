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

/// <summary>Unit-Tests für das Single-Plugin-Verhalten von TaskDetailViewModel (Issue #174).</summary>
public sealed class TaskDetailViewModelTests_PluginAktivierung : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly TodoService _todoService;
    private readonly AiExecutionService _aiService;
    private readonly PromptTemplateService _promptVorlagenService;
    private readonly PromptTemplatePlaceholderService _promptVorlagenPlatzhalterService = new();
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_PluginAktivierung.</summary>
    public TaskDetailViewModelTests_PluginAktivierung()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aiService = TestKiAusfuehrungsServiceFactory.Create();
        _promptVorlagenService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
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

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _aiService.Dispose();
        _db.Dispose();
    }

    private TaskDetailViewModel CreateSut(params IAiPlugin[] kiPlugins)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns(kiPlugins);

        var appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IWorkingDirectoryResolver>();
        var entwicklungsprozessService = new DevelopmentProcessService(
            _workItemService,
            _logService,
            gitPluginMock.Object,
            pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _aiService),
            NullLogger<DevelopmentProcessService>.Instance);

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();
        var arbeitsverzeichnisOeffnenService = TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService();
        var serviceProviderMock = new Mock<IServiceProvider>();

        return new TaskDetailViewModel(
            _workItemService,
            _logService,
            _aiService,
            entwicklungsprozessService,
            pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptScheduleDispatchService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService);
    }

    private async Task<WorkItem> ErstelleAufgabe()
        => await _workItemService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");

    private static Mock<IAiPlugin> CreateKiPluginMock(string name, string prefix)
    {
        var mock = new Mock<IAiPlugin>();
        mock.SetupGet(p => p.PluginName).Returns(name);
        mock.SetupGet(p => p.PluginPrefix).Returns(prefix);
        mock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        mock.SetupTerminalSpec();
        return mock;
    }

    /// <summary>Bei genau einem aktiven KI-Plugin wird der Selector versteckt (ZeigeKiPluginAuswahl == false).</summary>
    [Fact]
    public async Task LadeVerfuegbarePlugins_VerstecktSelector_BeiEinemAktivenPlugin()
    {
        var einzigesPlugin = CreateKiPluginMock("Einziges KI", "Softwareschmiede.EinzigesKi").Object;
        var workItem = await ErstelleAufgabe();
        var sut = CreateSut(einzigesPlugin);
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ZeigeKiPluginAuswahl.Should().BeFalse();
        sut.SelectedKiPluginPrefix.Should().Be("Softwareschmiede.EinzigesKi");
    }

    /// <summary>Bei mehr als einem aktiven KI-Plugin bleibt der Selector sichtbar (ZeigeKiPluginAuswahl == true).</summary>
    [Fact]
    public async Task LadeVerfuegbarePlugins_ZeigtSelector_BeiMehrerenAktivenPlugins()
    {
        var erstesPlugin = CreateKiPluginMock("Erstes KI", "Softwareschmiede.ErstesKi").Object;
        var zweitesPlugin = CreateKiPluginMock("Zweites KI", "Softwareschmiede.ZweitesKi").Object;
        var workItem = await ErstelleAufgabe();
        var sut = CreateSut(erstesPlugin, zweitesPlugin);
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ZeigeKiPluginAuswahl.Should().BeTrue();
    }
}
