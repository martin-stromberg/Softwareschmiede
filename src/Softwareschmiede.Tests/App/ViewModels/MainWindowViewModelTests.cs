using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Converters;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für MainWindowViewModel.</summary>
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly TodoService _todoService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly Guid _projectId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>MainWindowViewModelTests.</summary>
    public MainWindowViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, _todoService);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();

        var projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        var recoveryService = new WorkItemRecoveryService(_db, _runningStatusSourceMock.Object, NullLogger<WorkItemRecoveryService>.Instance);
        var dashboardViewModel = new DashboardViewModel(projectService, _workItemService, recoveryService, NullLogger<DashboardViewModel>.Instance);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(DashboardViewModel)))
            .Returns(dashboardViewModel);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectListViewModel)))
            .Returns(() => CreateProjectListViewModel(projectService));
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel(projectService));
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => TaskDetailViewModelTestFactory.Create(_db, _workItemService));
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(OpenTodosDialogViewModel)))
            .Returns(() => new OpenTodosDialogViewModel(_todoService, NullLogger<OpenTodosDialogViewModel>.Instance));
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IPluginManager)))
            .Returns(_pluginManagerMock.Object);
        _dialogServiceMock
            .Setup(d => d.ShowOpenTodosDialogAsync(It.IsAny<OpenTodosDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private MainWindowViewModel CreateSut(
        IActiveWorkItemsService? workItemService = null,
        IDialogService? dialogService = null,
        IApplicationVersionProvider? versionProvider = null,
        WorkItemRunDataChangedNotifier? runDataChangedNotifier = null,
        WorkItemListModeService? workItemsListModeService = null)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        var aiService = TestAiExecutionServiceFactory.Create();
        var promptScheduleDispatchService = new PromptScheduleDispatchService(aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        return new MainWindowViewModel(
            darkModeService,
            _serviceProviderMock.Object,
            workItemService ?? _workItemService,
            _todoService,
            promptScheduleDispatchService,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            new MainWindowOptionalServices(
                DispatcherInvoke: action => action(),
                DialogService: dialogService,
                VersionProvider: versionProvider,
                RunDataChangedNotifier: runDataChangedNotifier,
                WorkItemListModeService: workItemsListModeService));
    }

    private ProjectListViewModel CreateProjectListViewModel(ProjectService projectService)
    {
        return new ProjectListViewModel(
            projectService,
            _serviceProviderMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectListViewModel>.Instance);
    }

    private ProjectDetailViewModel CreateProjectDetailViewModel(ProjectService projectService)
    {
        return new ProjectDetailViewModel(
            projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
    }

    /// <summary>RefreshActiveWorkItemsAsync befüllt die AktiveAufgabenListe-Collection.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldFillObservableCollection_WhenCalled()
    {
        // Arrange
        await _workItemService.CreateAsync(_projectId, "Gestartete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projectId, "Zu startende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id);
    }

    /// <summary>RefreshActiveWorkItemsAsync setzt die offene Todo-Anzahl je Aufgabe.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldSetOpenTodoCountPerTask()
    {
        // Arrange
        var withTodos = await _workItemService.CreateAsync(_projectId, "Aufgabe mit Todos", null, ct: TestContext.Current.CancellationToken);
        var withoutTodos = await _workItemService.CreateAsync(_projectId, "Aufgabe ohne Todos", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withTodos.Id, "feature/mit-todos", "/tmp/mit-todos", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withoutTodos.Id, "feature/ohne-todos", "/tmp/ohne-todos", ct: TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(withTodos.Id, "Offen 1", TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(withTodos.Id, "Offen 2", TestContext.Current.CancellationToken);
        var completed = await _todoService.CreateTodoAsync(withTodos.Id, "Erledigt", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(completed.Id, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Single(a => a.Id == withTodos.Id).OpenTodoCount.Should().Be(2);
        sut.ActiveWorkItemsList.Single(a => a.Id == withTodos.Id).OpenTodoLabelText.Should().Be("2 Todos");
        sut.ActiveWorkItemsList.Single(a => a.Id == withoutTodos.Id).OpenTodoCount.Should().Be(0);
        sut.ActiveWorkItemsList.Single(a => a.Id == withoutTodos.Id).OpenTodoLabelText.Should().Be("0 Todos");
    }

    /// <summary>RefreshActiveWorkItemsAsync befüllt LiveLaufStatus aus der Live-Laufzeitquelle (Issue 342).</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapLiveRunStatus()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Live-Status-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/live", "/tmp/live", ct: TestContext.Current.CancellationToken);
        _runningStatusSourceMock
            .Setup(s => s.GetLiveRunStatus(workItem.Id))
            .Returns(WorkItemRunStatus.WaitingForInput);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id);
        item.LiveRunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt PausedUntilUtc und das abgeleitete IsPaused auf das Panel-Item (Issue 151).</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapPausedUntilUtc_WhenTaskIsPaused()
    {
        // Arrange
        var pausedUntil = DateTimeOffset.UtcNow.AddHours(2);
        var workItem = await _workItemService.CreateAsync(_projectId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/pause", "/tmp/pause", ct: TestContext.Current.CancellationToken);
        await _workItemService.SetPauseAsync(workItem.Id, pausedUntil, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id);
        item.PausedUntilUtc.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(5));
        item.IsPaused.Should().BeTrue();
    }

    /// <summary>OffeneTodosAnzeigenCommand öffnet den Dialog für die Aufgabe des aktiven Listen-Items.</summary>
    [Fact]
    public async Task ShowOpenTodosCommand_ShouldOpenDialogForSelectedTask()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Dialog-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dialog", "/tmp/dialog", ct: TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        OpenTodosDialogViewModel? capturedViewModel = null;
        _dialogServiceMock
            .Setup(d => d.ShowOpenTodosDialogAsync(It.IsAny<OpenTodosDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Callback<OpenTodosDialogViewModel, CancellationToken>((vm, _) => capturedViewModel = vm)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(dialogService: _dialogServiceMock.Object);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Act
        var command = (AsyncRelayCommand)sut.ActiveWorkItemsList.Single().ShowOpenTodosCommand!;
        await command.ExecuteAsync();

        // Assert
        capturedViewModel.Should().NotBeNull();
        capturedViewModel!.WorkItemId.Should().Be(workItem.Id);
        capturedViewModel.WorkItemsTitle.Should().Be("Dialog-Aufgabe");
        capturedViewModel.Todos.Should().ContainSingle(t => t.Description == "Offenes Todo");
    }

    /// <summary>IsDashboardVisible ist true, wenn CurrentView ein DashboardViewModel ist.</summary>
    [Fact]
    public void IsDashboardVisible_ShouldReturnTrue_WhenCurrentViewIsDashboardViewModel()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert (Konstruktor navigiert bereits zum Dashboard)
        sut.IsDashboardVisible.Should().BeTrue();
    }

    /// <summary>IsDashboardVisible ist false, wenn CurrentView kein DashboardViewModel ist.</summary>
    [Fact]
    public async Task IsDashboardVisible_ShouldReturnFalse_WhenCurrentViewIsNotDashboard()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Detail-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);

        // Assert
        sut.IsDashboardVisible.Should().BeFalse();
    }

    /// <summary>NavigateZuAufgabeCommand erstellt eine neue TaskDetailViewModel-Instanz und setzt sie als CurrentView.</summary>
    [Fact]
    public async Task NavigateToWorkItemCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithWorkItemId()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe für Navigation", null, ct: TestContext.Current.CancellationToken);

        // Act
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).WorkItemId.Should().Be(workItem.Id);
    }

    /// <summary>NavigateZuAufgabeCommand setzt den Fenstertitel nach dem Laden auf den Aufgabentitel.</summary>
    [Fact]
    public async Task NavigateToWorkItemCommand_ShouldSetTitleToLoadedTaskTitle()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Fenstertitel-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Title != "Softwareschmiede – Fenstertitel-Aufgabe")
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.Title.Should().Be("Softwareschmiede – Fenstertitel-Aufgabe");
    }

    /// <summary>Eine aus der Projektansicht geöffnete Aufgabe setzt den Fenstertitel nach dem Laden auf den Aufgabentitel.</summary>
    [Fact]
    public async Task ProjectTaskNavigation_ShouldSetTitleToLoadedTaskTitle()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Projektlisten-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var project = await _db.Projects.FindAsync(new object?[] { _projectId }, TestContext.Current.CancellationToken);

        sut.NavigateToProjectListCommand.Execute(null);
        var projectListViewModel = (ProjectListViewModel)sut.CurrentView!;
        projectListViewModel.SelectedProject = project;
        var projectDetailViewModel = (ProjectDetailViewModel)projectListViewModel.DetailViewModel!;

        projectDetailViewModel.OpenWorkItemCommand.Execute(workItem.Id);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Title != "Softwareschmiede – Projektlisten-Aufgabe")
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.Title.Should().Be("Softwareschmiede – Projektlisten-Aufgabe");
    }

    /// <summary>Ein später Detail-Callback aus der Projektansicht darf den Fenstertitel nach Dashboard-Navigation nicht überschreiben.</summary>
    [Fact]
    public async Task NavigateToDashboardCommand_ShouldIgnoreLateProjectTaskTitleCallback()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Spaeter Projekttitel", null, ct: TestContext.Current.CancellationToken);
        var project = await _db.Projects.FindAsync(new object?[] { _projectId }, TestContext.Current.CancellationToken);

        sut.NavigateToProjectListCommand.Execute(null);
        var projectListViewModel = (ProjectListViewModel)sut.CurrentView!;
        projectListViewModel.SelectedProject = project;
        var projectDetailViewModel = (ProjectDetailViewModel)projectListViewModel.DetailViewModel!;
        projectDetailViewModel.OpenWorkItemCommand.Execute(workItem.Id);

        sut.NavigateToDashboardCommand.Execute(null);
        projectListViewModel.DetailTitleChangeAction?.Invoke("Spaeter Projekttitel");

        sut.Title.Should().Be("Softwareschmiede – Dashboard");
    }

    /// <summary>Beim Wechsel zurück zum Dashboard bleibt kein alter Aufgabentitel stehen.</summary>
    [Fact]
    public async Task NavigateToDashboardCommand_ShouldClearPreviousTaskTitle()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Alter Aufgabentitel", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Title != "Softwareschmiede – Alter Aufgabentitel")
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.NavigateToDashboardCommand.Execute(null);

        sut.Title.Should().Be("Softwareschmiede – Dashboard");
    }

    /// <summary>Ein alter Detail-Callback darf den Fenstertitel nach Navigation nicht überschreiben.</summary>
    [Fact]
    public async Task NavigateToDashboardCommand_ShouldIgnoreLateTaskTitleCallback()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Spaeter Aufgabentitel", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);
        var detailViewModel = (TaskDetailViewModel)sut.CurrentView!;

        sut.NavigateToDashboardCommand.Execute(null);
        detailViewModel.DetailTitleChangeAction?.Invoke("Spaeter Aufgabentitel");

        sut.Title.Should().Be("Softwareschmiede – Dashboard");
    }

    /// <summary>Der CurrentView-Setter löst RefreshActiveWorkItemsAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task CurrentView_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "FireAndForget-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/fire-and-forget", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act: CurrentView-Wechsel löst im Setter SafeFireAndForget(RefreshActiveWorkItemsAsync) aus
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);

        // Assert: die Aktualisierung läuft asynchron im Hintergrund und füllt die Liste
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.ActiveWorkItemsList.Any(a => a.Id == workItem.Id))
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.ActiveWorkItemsList.Should().Contain(a => a.Id == workItem.Id,
            "der CurrentView-Setter muss RefreshActiveWorkItemsAsync per SafeFireAndForget auslösen");
    }

    /// <summary>NavigateToDashboard teilt die AktiveAufgabenListe mit dem DashboardViewModel, statt eine eigene Abfrage im Dashboard auszulösen (einzige gemeinsame Datenquelle).</summary>
    [Fact]
    public void NavigateToDashboard_ShouldShareActiveWorkItemsListWithDashboardViewModel_WhenCalled()
    {
        // Arrange & Act (Konstruktor navigiert bereits zum Dashboard)
        var sut = CreateSut();

        // Assert
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;
        dashboardViewModel.ActiveWorkItemsList.Should().BeSameAs(sut.ActiveWorkItemsList);
    }

    /// <summary>RefreshActiveWorkItemsAsync markiert genau die aktuell angezeigte Aufgabe als aktiv.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMarkCurrentTaskAsActive_WhenTaskDetailIsCurrentView()
    {
        // Arrange
        var first = await _workItemService.CreateAsync(_projectId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var second = await _workItemService.CreateAsync(_projectId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(first.Id, "feature/erste", "/tmp/erste", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(second.Id, "feature/zweite", "/tmp/zweite", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(second.Id);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.IsActive);
        sut.ActiveWorkItemsList.Single(a => a.IsActive).Id.Should().Be(second.Id);
    }

    /// <summary>RefreshActiveWorkItemsAsync wechselt die aktive Markierung nach Navigation zu einer anderen Aufgabe.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMoveActiveMarker_WhenNavigatingToAnotherTask()
    {
        // Arrange
        var first = await _workItemService.CreateAsync(_projectId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var second = await _workItemService.CreateAsync(_projectId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(first.Id, "feature/erste", "/tmp/erste", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(second.Id, "feature/zweite", "/tmp/zweite", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(first.Id);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(second.Id);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.IsActive);
        sut.ActiveWorkItemsList.Single(a => a.IsActive).Id.Should().Be(second.Id);
        sut.ActiveWorkItemsList.Single(a => a.Id == first.Id).IsActive.Should().BeFalse();
    }

    /// <summary>RefreshActiveWorkItemsAsync behaelt die aktive Markierung auch nach erneutem Laden der Liste.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldKeepActiveMarker_WhenListIsRefreshed()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Refresh-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/refresh", "/tmp/refresh", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        ((RelayCommand<Guid>)sut.NavigateToWorkItemCommand).Execute(workItem.Id);

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.IsActive);
        sut.ActiveWorkItemsList.Single(a => a.IsActive).Id.Should().Be(workItem.Id);
    }

    /// <summary>RefreshActiveWorkItemsAsync markiert keine Aufgabe aktiv, wenn keine Aufgabendetailansicht angezeigt wird.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldNotMarkAnyTaskActive_WhenCurrentViewIsDashboard()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Dashboard-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dashboard", "/tmp/dashboard", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Should().NotContain(a => a.IsActive);
    }

    /// <summary>RefreshActiveWorkItemsAsync löst SCM- und KI-Plugin-Anzeigenamen pro Aufgabe auf.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldResolvePluginNamePerTask_WhenPrefixesAreKnown()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        var aiPlugin = new Mock<IAiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        aiPlugin.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projectId,
            RepositoryName = "repo",
            RepositoryUrl = "https://example.invalid/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        });
        var workItem = await _workItemService.CreateAsync(_projectId, "Plugin-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/plugin", "/tmp/plugin", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Should().ContainSingle().Subject;
        item.ScmPluginName.Should().Be("GitHub");
        item.AiPluginName.Should().Be("Codex");
    }

    /// <summary>RefreshActiveWorkItemsAsync fällt bei unbekannten Plugin-Prefixen auf den gespeicherten Prefix zurück.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldFallbackToStoredPrefix_WhenPluginIsUnknown()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Fallback-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/fallback", "/tmp/fallback", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Should().ContainSingle().Subject;
        item.AiPluginName.Should().Be("Unbekannt.KI");
    }

    /// <summary>RefreshActiveWorkItemsAsync nutzt bei fehlenden gespeicherten Plugin-Prefixen keinen globalen Default.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldNotUseGlobalDefaults_WhenPluginPrefixesAreMissing()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        var aiPlugin = new Mock<IAiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        aiPlugin.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var workItem = await _workItemService.CreateAsync(_projectId, "Ohne Prefix", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/ohne-prefix", "/tmp/ohne-prefix", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Should().ContainSingle().Subject;
        item.ScmPluginName.Should().BeNull();
        item.AiPluginName.Should().BeNull();
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt Kind, KindSymbol, KindLabel und KindGroupName je Auftragsart;
    /// Unspecified erhält weder Symbol noch Label — das Badge bleibt ausgeblendet (Issue 462).</summary>
    [Theory]
    [InlineData(WorkItemKind.Manual, "✋", "Manuell", "Manuell")]
    [InlineData(WorkItemKind.Instruction, "📣", "Anweisung", "Anweisung")]
    [InlineData(WorkItemKind.IssuePreparation, "🔍", "Issue-Aufbereitung", "Issue-Aufbereitung")]
    [InlineData(WorkItemKind.PullRequest, "🔀", "Pull Request", "Pull Request")]
    [InlineData(WorkItemKind.Alert, "⚠", "Alert", "Alert")]
    [InlineData(WorkItemKind.Unspecified, null, "", "Ohne Auftragsart")]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapKindFieldsPerKind(
        WorkItemKind kind, string? expectedSymbol, string expectedLabel, string expectedGroupName)
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, $"Aufgabe {kind}", null, ct: TestContext.Current.CancellationToken);
        workItem.Kind = kind;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, $"feature/{kind}", "/tmp/kind", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id);
        item.Kind.Should().Be(kind);
        item.KindSymbol.Should().Be(expectedSymbol);
        item.KindLabel.Should().Be(expectedLabel);
        item.KindGroupName.Should().Be(expectedGroupName);
    }

    /// <summary>NavigateToDashboard setzt die NavigateZuAufgabeAction des DashboardViewModel, sodass dessen NavigateZuAufgabeCommand zur Aufgabendetailansicht navigiert (Delegate-Muster statt versteckter Kopplung).</summary>
    [Fact]
    public async Task NavigateToDashboard_ShouldWireNavigateToWorkItemActionOnDashboardViewModel_WhenCalled()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Aufgabe für Dashboard-Navigation", null, ct: TestContext.Current.CancellationToken);
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;

        // Act
        ((RelayCommand<Guid>)dashboardViewModel.NavigateToWorkItemCommand).Execute(workItem.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).WorkItemId.Should().Be(workItem.Id);
    }

    /// <summary>Auslösen von RunningCountChanged am Mock aktualisiert die AktiveAufgabenListe.</summary>
    [Fact]
    public async Task RunningCountChanged_ShouldReloadActiveWorkItemsList_WhenRaised()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projectId, "Event-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/event", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act
        _runningStatusSourceMock.Raise(m => m.RunningCountChanged += null, 0, 1);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.ActiveWorkItemsList.Any(a => a.Id == workItem.Id))
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.ActiveWorkItemsList.Should().Contain(a => a.Id == workItem.Id,
            "RunningCountChanged muss RefreshActiveWorkItemsAsync auslösen");
    }

    /// <summary>LaufdatenChanged aktualisiert eine bereits sichtbare Seitenleisten-Kachel nach persistierten Laufdaten.</summary>
    [Fact]
    public async Task RundataChanged_ShouldReloadVisiblePanelItem_WhenRunDataWhatPersisted()
    {
        // Arrange
        var notifier = new WorkItemRunDataChangedNotifier();
        var converter = new AiExecutionStatusConverter();
        var workItem = await _workItemService.CreateAsync(_projectId, "Laufdaten-Event-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/laufdaten-event", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut(runDataChangedNotifier: notifier);
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);
        var initialItem = sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id).Subject;
        initialItem.ActiveRunId.Should().BeNull();
        converter.Convert(initialItem, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("✓ Bereit");

        // Act
        await _workItemService.SetActiveRunAsync(workItem.Id, "run-laufdaten-event", TestContext.Current.CancellationToken);
        notifier.NotifyRunDataChanged(workItem.Id);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline
               && sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id).ActiveRunId is null)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        var refreshedItem = sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id).Subject;
        refreshedItem.ActiveRunId.Should().Be("run-laufdaten-event");
        refreshedItem.LastHeartbeatUtc.Should().NotBeNull();
        refreshedItem.RunStatus.Should().Be(WorkItemRunStatus.Running);
        converter.Convert(refreshedItem, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("▶ Läuft");
    }

    /// <summary>Re-Entrancy-Schutz: Ein zweiter, echt gleichzeitiger Aufruf während eine Aktualisierung noch läuft, wird übersprungen, statt erneut auf die Datenquelle zuzugreifen.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldSkip_WhenAlreadyRunning()
    {
        // Arrange: kontrolliert verzögerte Datenquelle, damit ein Aufruf lange genug läuft, um echte Nebenläufigkeit zu erzwingen
        var workItem = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Project = new Softwareschmiede.Domain.Entities.Project
            {
                Id = _projectId,
                Name = "Testprojekt",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ProjectStatus.Active
            },
            Title = "Verzoegerte Aufgabe",
            Status = WorkItemStatus.Started,
            CreatedAt = DateTimeOffset.UtcNow,
            BranchName = "feature/verzoegert",
            LocalClonePath = "/tmp/klon"
        };
        var workItemService = new DelayingActiveWorkItemsService([workItem]);

        var sut = CreateSut(workItemService);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.ActiveWorkItemsList.All(a => a.Id != workItem.Id))
            await Task.Delay(25, TestContext.Current.CancellationToken);

        sut.ActiveWorkItemsList.Should().ContainSingle(a => a.Id == workItem.Id,
            "der vom Konstruktor gestartete Hintergrund-Refresh soll vor der Re-Entrancy-Pruefung abgeschlossen sein");

        workItemService.Enable();
        var firstCallTask = Task.Run(() => sut.RefreshActiveWorkItemsAsync(), TestContext.Current.CancellationToken);
        await workItemService.FirstDelayedQuery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act: der zweite Aufruf überlappt den noch laufenden, verzögerten ersten Aufruf
        try
        {
            await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Assert: der übersprungene Aufruf fragt die verzögerte Datenquelle kein zweites Mal ab
            workItemService.DelayedQuery.Should().Be(1,
                "ein zweiter, gleichzeitiger Aufruf muss uebersprungen werden, statt erneut auf die verzögerte Datenquelle zuzugreifen");
        }
        finally
        {
            workItemService.Resume();
            await firstCallTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Nach Dispose() löst ein erneutes RunningCountChanged kein Neuladen aus und wirft nicht.</summary>
    [Fact]
    public async Task Dispose_ShouldUnsubscribeFromRunningCountChanged()
    {
        // Arrange
        var sut = CreateSut();
        sut.Dispose();

        var workItem = await _workItemService.CreateAsync(_projectId, "Nach-Dispose-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dispose", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => _runningStatusSourceMock.Raise(m => m.RunningCountChanged += null, 0, 1);

        // Assert
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        sut.ActiveWorkItemsList.Should().NotContain(a => a.Id == workItem.Id,
            "nach Dispose() darf RunningCountChanged keine Aktualisierung mehr auslösen");
    }

    /// <summary>Nach Dispose() löst LaufdatenChanged kein Neuladen aus und wirft nicht.</summary>
    [Fact]
    public async Task Dispose_ShouldUnsubscribeFromRundataChanged()
    {
        // Arrange
        var workItem = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Project = new Softwareschmiede.Domain.Entities.Project
            {
                Id = _projectId,
                Name = "Testprojekt",
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ProjectStatus.Active
            },
            Title = "Laufdaten nach Dispose",
            Status = WorkItemStatus.Started,
            CreatedAt = DateTimeOffset.UtcNow,
            BranchName = "feature/laufdaten-dispose",
            LocalClonePath = "/tmp/klon"
        };
        var workItemService = new CountingActiveWorkItemsService([workItem]);
        var notifier = new WorkItemRunDataChangedNotifier();
        var sut = CreateSut(workItemService, runDataChangedNotifier: notifier);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && workItemService.Query == 0)
            await Task.Delay(25, TestContext.Current.CancellationToken);

        var queryBeforeDispose = workItemService.Query;
        sut.Dispose();

        // Act
        var act = () => notifier.NotifyRunDataChanged(workItem.Id);

        // Assert
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        workItemService.Query.Should().Be(queryBeforeDispose,
            "nach Dispose() darf LaufdatenChanged keine Aktualisierung mehr auslösen");
    }

    /// <summary>CurrentVersion wird bei erfolgreichem GetInstalledVersionAsync mit der formatierten Version gefüllt.</summary>
    [Fact]
    public async Task CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue()
    {
        // Arrange
        var versionProviderMock = new Mock<IApplicationVersionProvider>();
        versionProviderMock.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledVersionInfo("1.2.3", "v1.2.3", null, null));

        // Act
        var sut = CreateSut(versionProvider: versionProviderMock.Object);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.CurrentVersion is null)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.CurrentVersion.Should().Be("Version 1.2.3");
    }

    /// <summary>CurrentVersion fällt auf den Fallback-Text zurück, wenn der Provider null zurückgibt, ohne eine Exception auszulösen.</summary>
    [Fact]
    public async Task CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull()
    {
        // Arrange
        var versionProviderMock = new Mock<IApplicationVersionProvider>();
        versionProviderMock.Setup(p => p.GetInstalledVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstalledVersionInfo?)null);

        // Act
        var sut = CreateSut(versionProvider: versionProviderMock.Object);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.CurrentVersion is null)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.CurrentVersion.Should().Be("Version unbekannt");
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt den Repository-Namen als Untertitel und fällt ohne Repository auf den Projektnamen zurück.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapRepositoryName_WithRepoAndProjectFallback()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projectId,
            RepositoryName = "mein-repo",
            RepositoryUrl = "https://example.invalid/mein-repo.git",
            PluginType = "LocalDirectoryPlugin",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var withRepo = await _workItemService.CreateAsync(_projectId, "Repo-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        var withoutRepo = await _workItemService.CreateAsync(_projectId, "Freie Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withRepo.Id, "feature/repo", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withoutRepo.Id, "feature/frei", "/tmp/frei", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.ActiveWorkItemsList.Single(a => a.Id == withRepo.Id).RepositoryName.Should().Be("mein-repo");
        sut.ActiveWorkItemsList.Single(a => a.Id == withoutRepo.Id).RepositoryName.Should().Be("Testprojekt",
            "ohne Repository-Verknüpfung fällt der Untertitel auf den Projektnamen zurück");
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt Plugin-Symbole; nicht aufgelöste Symbole (Mock ohne Setup liefert null) fallen auf den Fallback-Glyph zurück.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapPluginSymbol_WithFallbackGlyphTolerance()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginSymbol).Returns("🐙");
        var aiPlugin = new Mock<IAiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        // PluginSymbol absichtlich nicht aufgesetzt: Moq liefert null statt des Interface-Defaults.
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projectId,
            RepositoryName = "repo",
            RepositoryUrl = "https://example.invalid/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projectId, "Symbol-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/symbol", "/tmp/symbol", ct: TestContext.Current.CancellationToken);
        var unknown = await _workItemService.CreateAsync(_projectId, "Unbekannte KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(unknown.Id, unknown.Title, unknown.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(unknown.Id, "feature/unbekannt", "/tmp/unbekannt", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id);
        item.ScmPluginSymbol.Should().Be("🐙");
        item.AiPluginSymbol.Should().Be(IPlugin.FallbackSymbol,
            "PluginSymbol=null (Mock ohne Setup) muss auf den Fallback-Glyph gemappt werden");
        var unknownItem = sut.ActiveWorkItemsList.Single(a => a.Id == unknown.Id);
        unknownItem.AiPluginSymbol.Should().Be(IPlugin.FallbackSymbol,
            "ein nicht auflösbarer Prefix liefert ebenfalls den Fallback-Glyph");
        unknownItem.ScmPluginSymbol.Should().BeNull("ohne Repository kein SCM-Symbol");
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt KiGruppenName auf PluginName, rohen Prefix bzw. "Nicht ausgeführt".</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapAiGroupsName()
    {
        // Arrange
        var aiPlugin = new Mock<IAiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var withAi = await _workItemService.CreateAsync(_projectId, "KI-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(withAi.Id, withAi.Title, withAi.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withAi.Id, "feature/ki", "/tmp/ki", ct: TestContext.Current.CancellationToken);
        var unknown = await _workItemService.CreateAsync(_projectId, "Unbekannte KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(unknown.Id, unknown.Title, unknown.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(unknown.Id, "feature/unbekannt", "/tmp/unbekannt", ct: TestContext.Current.CancellationToken);
        var withoutAi = await _workItemService.CreateAsync(_projectId, "Ohne KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withoutAi.Id, "feature/ohne", "/tmp/ohne", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var resolved = sut.ActiveWorkItemsList.Single(a => a.Id == withAi.Id);
        resolved.AiGroupName.Should().Be("Codex");
        resolved.AiGroupIsFallback.Should().BeFalse();
        var rawPrefix = sut.ActiveWorkItemsList.Single(a => a.Id == unknown.Id);
        rawPrefix.AiGroupName.Should().Be("Unbekannt.KI",
            "nicht auflösbare Prefixe bilden eine eigene Gruppe mit dem Prefix als Namen");
        rawPrefix.AiGroupIsFallback.Should().BeFalse();
        var fallback = sut.ActiveWorkItemsList.Single(a => a.Id == withoutAi.Id);
        fallback.AiGroupName.Should().Be(WorkItemListLayout.NotExecuted);
        fallback.AiGroupIsFallback.Should().BeTrue();
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt RepositoryGruppenName auf den Repository-Namen bzw. "Ohne Repository".</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapRepositoryGroupsName()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projectId,
            RepositoryName = "gruppen-repo",
            RepositoryUrl = "https://example.invalid/gruppen-repo.git",
            PluginType = "LocalDirectoryPlugin",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var withRepo = await _workItemService.CreateAsync(_projectId, "Repo-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        var withoutRepo = await _workItemService.CreateAsync(_projectId, "Freie Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withRepo.Id, "feature/repo", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(withoutRepo.Id, "feature/frei", "/tmp/frei", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var withGroup = sut.ActiveWorkItemsList.Single(a => a.Id == withRepo.Id);
        withGroup.RepositoryGroupName.Should().Be("gruppen-repo");
        withGroup.RepositoryGroupIsFallback.Should().BeFalse();
        var fallback = sut.ActiveWorkItemsList.Single(a => a.Id == withoutRepo.Id);
        fallback.RepositoryGroupName.Should().Be(WorkItemListLayout.WithoutRepository);
        fallback.RepositoryGroupIsFallback.Should().BeTrue();
    }

    /// <summary>RefreshActiveWorkItemsAsync mappt SortierZeitpunkt auf LastCliStartUtc ?? ErstellungsDatum.</summary>
    [Fact]
    public async Task RefreshActiveWorkItemsAsync_ShouldMapSortTimestamp()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projectId, "Sortier-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/sort", "/tmp/sort", ct: TestContext.Current.CancellationToken);
        // StartAsync setzt LastCliStartUtc bewusst auf null — der Zeitstempel entsteht erst
        // beim echten CLI-Prozessstart (CliProcessManager → AktivenLaufSetzenAsync).
        await _workItemService.SetActiveRunAsync(workItem.Id, Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.RefreshActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        var read = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var item = sut.ActiveWorkItemsList.Single(a => a.Id == workItem.Id);
        read!.LastCliStartUtc.Should().NotBeNull(
            "die gestartete Aufgabe muss einen CLI-Startzeitpunkt haben, damit der Test den ??-Zweig belegt");
        item.SortTimestamp.Should().BeCloseTo(read.LastCliStartUtc!.Value, TimeSpan.FromMilliseconds(50),
            "SortierZeitpunkt muss auf LetzterCliStartUtc ?? ErstellungsDatum mappen");
        item.SortTimestamp.Should().NotBe(read.CreatedAt);
    }

    /// <summary>AufgabenListenModus startet mit dem Default Datum.</summary>
    [Fact]
    public void WorkItemsListsMode_ShouldDefaultToDate()
    {
        var sut = CreateSut();

        sut.WorkItemListMode.Should().Be(WorkItemListMode.Date);
    }

    /// <summary>AufgabenListenModus ist setzbar und löst PropertyChanged aus.</summary>
    [Fact]
    public void WorkItemsListsMode_ShouldBeSettableAndRaisePropertyChanged()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.WorkItemListMode = WorkItemListMode.Repository;

        sut.WorkItemListMode.Should().Be(WorkItemListMode.Repository);
        changed.Should().Contain(nameof(MainWindowViewModel.WorkItemListMode));
    }

    /// <summary>Ein persistierter Modus wird über GespeichertenAufgabenListenModusLadenAsync wiederhergestellt.</summary>
    [Fact]
    public async Task WorkItemsListsMode_ShouldRestorePersistedMode()
    {
        // Arrange
        var (modeService, provider) = WorkItemsListsModeServiceTestFactory.CreateModeService();
        using (provider)
        {
            using (var scope = provider.CreateScope())
            {
                var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
                await settings.SetSettingAsync(AppSettingService.ActiveWorkItemsListModeKey, "Repository", TestContext.Current.CancellationToken);
            }

            var sut = CreateSut(workItemsListModeService: modeService);

            // Act
            await sut.LoadSavedWorkItemsListModeAsync(TestContext.Current.CancellationToken);

            // Assert
            sut.WorkItemListMode.Should().Be(WorkItemListMode.Repository);
        }
    }

    /// <summary>Eine Modusänderung wird über den Service persistiert; die Wiederherstellung schreibt nicht zurück.</summary>
    [Fact]
    public async Task WorkItemsListsMode_ShouldPersistModeOnChange_WithoutWriteBackOnRestore()
    {
        // Arrange
        var (modeService, provider) = WorkItemsListsModeServiceTestFactory.CreateModeService();
        using (provider)
        {
            var sut = CreateSut(workItemsListModeService: modeService);

            // Act: Benutzerumschaltung persistiert den neuen Modus.
            sut.WorkItemListMode = WorkItemListMode.Ai;

            // Assert (Polling, da das Speichern fire-and-forget läuft)
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            string? value = null;
            while (DateTime.UtcNow < deadline && value != "Ki")
            {
                using var scope = provider.CreateScope();
                value = await scope.ServiceProvider.GetRequiredService<AppSettingService>()
                    .GetSettingAsync(AppSettingService.ActiveWorkItemsListModeKey, TestContext.Current.CancellationToken);
                if (value != "Ki")
                    await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            value.Should().Be("Ki");

            // Act & Assert: die Wiederherstellung würde den gesetzten Benutzer-Modus nicht
            // überschreiben (Race-Guard) und keinen zweiten Schreibzugriff auslösen.
            await sut.LoadSavedWorkItemsListModeAsync(TestContext.Current.CancellationToken);
            sut.WorkItemListMode.Should().Be(WorkItemListMode.Ai);
        }
    }

    /// <summary>Test-Datenquelle, die aktive Aufgaben kontrolliert blockierend zurueckgibt.</summary>
    private sealed class DelayingActiveWorkItemsService : IActiveWorkItemsService
    {
        private readonly ManualResetEventSlim _resume = new();
        private readonly TaskCompletionSource _firstDelayedQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Softwareschmiede.Domain.Entities.WorkItem> _workItems;
        private int _active;
        private int _delayedQuery;

        /// <summary>Initialisiert eine neue Test-Datenquelle.</summary>
        /// <param name="workItems">Die zurueckzugebenden Aufgaben.</param>
        public DelayingActiveWorkItemsService(List<Softwareschmiede.Domain.Entities.WorkItem> workItems)
        {
            _workItems = workItems;
        }

        /// <summary>Task, der abgeschlossen wird, sobald der erste künstlich verzögerte Abruf erreicht wurde.</summary>
        public Task FirstDelayedQuery => _firstDelayedQuery.Task;

        /// <summary>Anzahl der Abrufe, die nach Aktivierung der künstlichen Verzögerung erreicht wurden.</summary>
        public int DelayedQuery => Volatile.Read(ref _delayedQuery);

        /// <summary>Aktiviert die kuenstliche Verzoegerung fuer nachfolgende Abrufe.</summary>
        public void Enable() => Volatile.Write(ref _active, 1);

        /// <summary>Gibt blockierte Abrufe wieder frei.</summary>
        public void Resume() => _resume.Set();

        /// <inheritdoc />
        public Task<List<Softwareschmiede.Domain.Entities.WorkItem>> GetActiveWorkItemsAsync(CancellationToken ct = default)
        {
            if (Volatile.Read(ref _active) == 1)
            {
                Interlocked.Increment(ref _delayedQuery);
                _firstDelayedQuery.TrySetResult();
                _resume.Wait(ct);
            }

            return Task.FromResult(_workItems);
        }
    }

    /// <summary>Test-Datenquelle, die Abrufe zaehlt.</summary>
    private sealed class CountingActiveWorkItemsService : IActiveWorkItemsService
    {
        private readonly List<Softwareschmiede.Domain.Entities.WorkItem> _workItems;
        private int _query;

        /// <summary>Initialisiert eine neue zaehlende Test-Datenquelle.</summary>
        /// <param name="workItems">Die zurueckzugebenden Aufgaben.</param>
        public CountingActiveWorkItemsService(List<Softwareschmiede.Domain.Entities.WorkItem> workItems)
        {
            _workItems = workItems;
        }

        /// <summary>Anzahl der Abrufe.</summary>
        public int Query => Volatile.Read(ref _query);

        /// <inheritdoc />
        public Task<List<Softwareschmiede.Domain.Entities.WorkItem>> GetActiveWorkItemsAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _query);
            return Task.FromResult(_workItems);
        }
    }
}
