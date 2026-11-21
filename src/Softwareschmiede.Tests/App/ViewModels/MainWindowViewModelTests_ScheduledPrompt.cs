using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für MainWindowViewModel im Zusammenhang mit zeitgesteuerten Prompts: Die Seitenleisten-Liste
/// muss den "Prompt in Wartestellung"-Status eines aktiven PromptScheduleDispatchService-Eintrags übernehmen.</summary>
public sealed class MainWindowViewModelTests_ScheduledPrompt : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly TodoService _todoService;
    private readonly WorkItemService _workItemService;
    private readonly AiExecutionService _aiService;
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock;
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>MainWindowViewModelTests_ZeitgesteuerterPrompt.</summary>
    public MainWindowViewModelTests_ScheduledPrompt()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, _todoService);
        _aiService = TestAiExecutionServiceFactory.Create();
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IPluginManager))).Returns(pluginManagerMock.Object);

        var projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        var recoveryService = new WorkItemRecoveryService(_db, _runningStatusSourceMock.Object, NullLogger<WorkItemRecoveryService>.Instance);
        var dashboardViewModel = new DashboardViewModel(projectService, _workItemService, recoveryService, NullLogger<DashboardViewModel>.Instance);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(DashboardViewModel))).Returns(dashboardViewModel);

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
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

    private MainWindowViewModel CreateSut()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        return new MainWindowViewModel(
            darkModeService,
            _serviceProviderMock.Object,
            _workItemService,
            _todoService,
            _promptScheduleDispatchService,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            new MainWindowOptionalServices(DispatcherInvoke: action => action()));
    }

    /// <summary>Ist für eine Aufgabe ein zeitgesteuerter Prompt beim PromptScheduleDispatchService geplant, muss das
    /// entsprechende AktiveAufgabePanelItem in der Seitenleisten-Liste HasScheduledPrompt=true tragen.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_WithPlannedPrompt_SetsHasScheduledPrompt()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe mit geplantem Prompt", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        await _promptScheduleDispatchService.SchedulePromptAsync(workItem.Id, "Testprompt", DateTimeOffset.UtcNow.AddMinutes(5));

        var sut = CreateSut();
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id && a.HasScheduledPrompt);
    }

    /// <summary>Ist für eine Aufgabe kein Prompt geplant, muss HasScheduledPrompt false bleiben.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_WithoutPlannedPrompt_HasScheduledPromptRemainsFalse()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne geplanten Prompt", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id && !a.HasScheduledPrompt);
    }
}
