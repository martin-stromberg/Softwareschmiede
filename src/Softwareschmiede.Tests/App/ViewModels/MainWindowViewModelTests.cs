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
    private readonly Guid _projektId = new Guid("22222222-2222-2222-2222-222222222222");

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
            Id = _projektId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();

        var projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        var recoveryService = new WorkItemRecoveryService(_db, _runningStatusSourceMock.Object, NullLogger<WorkItemRecoveryService>.Instance);
        var dashboardViewModel = new DashboardViewModel(projektService, _workItemService, recoveryService, NullLogger<DashboardViewModel>.Instance);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(DashboardViewModel)))
            .Returns(dashboardViewModel);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectListViewModel)))
            .Returns(() => CreateProjectListViewModel(projektService));
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel(projektService));
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
        AufgabenListenModusService? aufgabenListenModusService = null)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        var promptScheduleDispatchService = new PromptScheduleDispatchService(kiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        return new MainWindowViewModel(
            darkModeService,
            _serviceProviderMock.Object,
            workItemService ?? _workItemService,
            _todoService,
            promptScheduleDispatchService,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            new MainWindowOptionaleDienste(
                DispatcherInvoke: action => action(),
                DialogService: dialogService,
                VersionProvider: versionProvider,
                LaufdatenChangedNotifier: runDataChangedNotifier,
                AufgabenListenModusService: aufgabenListenModusService));
    }

    private ProjectListViewModel CreateProjectListViewModel(ProjectService projektService)
    {
        return new ProjectListViewModel(
            projektService,
            _serviceProviderMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectListViewModel>.Instance);
    }

    private ProjectDetailViewModel CreateProjectDetailViewModel(ProjectService projektService)
    {
        return new ProjectDetailViewModel(
            projektService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync befüllt die AktiveAufgabenListe-Collection.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled()
    {
        // Arrange
        await _workItemService.CreateAsync(_projektId, "Gestartete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projektId, "Zu startende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == workItem.Id);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync setzt die offene Todo-Anzahl je Aufgabe.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldSetOpenTodoCountPerTask()
    {
        // Arrange
        var mitTodos = await _workItemService.CreateAsync(_projektId, "Aufgabe mit Todos", null, ct: TestContext.Current.CancellationToken);
        var ohneTodos = await _workItemService.CreateAsync(_projektId, "Aufgabe ohne Todos", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(mitTodos.Id, "feature/mit-todos", "/tmp/mit-todos", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(ohneTodos.Id, "feature/ohne-todos", "/tmp/ohne-todos", ct: TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(mitTodos.Id, "Offen 1", TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(mitTodos.Id, "Offen 2", TestContext.Current.CancellationToken);
        var erledigt = await _todoService.CreateTodoAsync(mitTodos.Id, "Erledigt", TestContext.Current.CancellationToken);
        await _todoService.MarkTodoAsCompletedAsync(erledigt.Id, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Single(a => a.Id == mitTodos.Id).OffeneTodoCount.Should().Be(2);
        sut.AktiveAufgabenListe.Single(a => a.Id == mitTodos.Id).OffeneTodoLabelText.Should().Be("2 Todos");
        sut.AktiveAufgabenListe.Single(a => a.Id == ohneTodos.Id).OffeneTodoCount.Should().Be(0);
        sut.AktiveAufgabenListe.Single(a => a.Id == ohneTodos.Id).OffeneTodoLabelText.Should().Be("0 Todos");
    }

    /// <summary>AktiveAufgabenAktualisierenAsync befüllt LiveLaufStatus aus der Live-Laufzeitquelle (Issue 342).</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapLiveLaufStatus()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Live-Status-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/live", "/tmp/live", ct: TestContext.Current.CancellationToken);
        _runningStatusSourceMock
            .Setup(s => s.GetLiveRunStatus(workItem.Id))
            .Returns(WorkItemRunStatus.WaitingForInput);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Single(a => a.Id == workItem.Id);
        item.LiveRunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync mappt PausiertBisUtc und das abgeleitete IstPausiert auf das Panel-Item (Issue 151).</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapPausiertBisUtc_WhenTaskIsPaused()
    {
        // Arrange
        var pausedUntil = DateTimeOffset.UtcNow.AddHours(2);
        var workItem = await _workItemService.CreateAsync(_projektId, "Pausierte Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/pause", "/tmp/pause", ct: TestContext.Current.CancellationToken);
        await _workItemService.SetPauseAsync(workItem.Id, pausedUntil, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Single(a => a.Id == workItem.Id);
        item.PausedUntilUtc.Should().BeCloseTo(pausedUntil, TimeSpan.FromSeconds(5));
        item.IsPaused.Should().BeTrue();
    }

    /// <summary>OffeneTodosAnzeigenCommand öffnet den Dialog für die Aufgabe des aktiven Listen-Items.</summary>
    [Fact]
    public async Task OffeneTodosAnzeigenCommand_ShouldOpenDialogForSelectedTask()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Dialog-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dialog", "/tmp/dialog", ct: TestContext.Current.CancellationToken);
        await _todoService.CreateTodoAsync(workItem.Id, "Offenes Todo", TestContext.Current.CancellationToken);
        OpenTodosDialogViewModel? capturedViewModel = null;
        _dialogServiceMock
            .Setup(d => d.ShowOpenTodosDialogAsync(It.IsAny<OpenTodosDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Callback<OpenTodosDialogViewModel, CancellationToken>((vm, _) => capturedViewModel = vm)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(dialogService: _dialogServiceMock.Object);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Act
        var command = (AsyncRelayCommand)sut.AktiveAufgabenListe.Single().OffeneTodosAnzeigenCommand!;
        await command.ExecuteAsync();

        // Assert
        capturedViewModel.Should().NotBeNull();
        capturedViewModel!.WorkItemId.Should().Be(workItem.Id);
        capturedViewModel.AufgabenTitel.Should().Be("Dialog-Aufgabe");
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
        var workItem = await _workItemService.CreateAsync(_projektId, "Detail-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);

        // Assert
        sut.IsDashboardVisible.Should().BeFalse();
    }

    /// <summary>NavigateZuAufgabeCommand erstellt eine neue TaskDetailViewModel-Instanz und setzt sie als CurrentView.</summary>
    [Fact]
    public async Task NavigateZuAufgabeCommand_ShouldCreateTaskDetailViewModelAndSetCurrentView_WhenExecutedWithAufgabeId()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "Aufgabe für Navigation", null, ct: TestContext.Current.CancellationToken);

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).WorkItemId.Should().Be(workItem.Id);
    }

    /// <summary>NavigateZuAufgabeCommand setzt den Fenstertitel nach dem Laden auf den Aufgabentitel.</summary>
    [Fact]
    public async Task NavigateZuAufgabeCommand_ShouldSetTitleToLoadedTaskTitle()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "Fenstertitel-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);

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
        var workItem = await _workItemService.CreateAsync(_projektId, "Projektlisten-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var project = await _db.Projects.FindAsync(new object?[] { _projektId }, TestContext.Current.CancellationToken);

        sut.NavigateToProjectListCommand.Execute(null);
        var projectListViewModel = (ProjectListViewModel)sut.CurrentView!;
        projectListViewModel.SelectedProjekt = project;
        var projectDetailViewModel = (ProjectDetailViewModel)projectListViewModel.DetailViewModel!;

        projectDetailViewModel.AufgabeOeffnenCommand.Execute(workItem.Id);

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
        var workItem = await _workItemService.CreateAsync(_projektId, "Spaeter Projekttitel", null, ct: TestContext.Current.CancellationToken);
        var project = await _db.Projects.FindAsync(new object?[] { _projektId }, TestContext.Current.CancellationToken);

        sut.NavigateToProjectListCommand.Execute(null);
        var projectListViewModel = (ProjectListViewModel)sut.CurrentView!;
        projectListViewModel.SelectedProjekt = project;
        var projectDetailViewModel = (ProjectDetailViewModel)projectListViewModel.DetailViewModel!;
        projectDetailViewModel.AufgabeOeffnenCommand.Execute(workItem.Id);

        sut.NavigateToDashboardCommand.Execute(null);
        projectListViewModel.DetailTitelAenderungAction?.Invoke("Spaeter Projekttitel");

        sut.Title.Should().Be("Softwareschmiede – Dashboard");
    }

    /// <summary>Beim Wechsel zurück zum Dashboard bleibt kein alter Aufgabentitel stehen.</summary>
    [Fact]
    public async Task NavigateToDashboardCommand_ShouldClearPreviousTaskTitle()
    {
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "Alter Aufgabentitel", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);
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
        var workItem = await _workItemService.CreateAsync(_projektId, "Spaeter Aufgabentitel", null, ct: TestContext.Current.CancellationToken);

        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);
        var detailViewModel = (TaskDetailViewModel)sut.CurrentView!;

        sut.NavigateToDashboardCommand.Execute(null);
        detailViewModel.DetailTitelAenderungAction?.Invoke("Spaeter Aufgabentitel");

        sut.Title.Should().Be("Softwareschmiede – Dashboard");
    }

    /// <summary>Der CurrentView-Setter löst AktiveAufgabenAktualisierenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task CurrentView_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "FireAndForget-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/fire-and-forget", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act: CurrentView-Wechsel löst im Setter SafeFireAndForget(AktiveAufgabenAktualisierenAsync) aus
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);

        // Assert: die Aktualisierung läuft asynchron im Hintergrund und füllt die Liste
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.AktiveAufgabenListe.Any(a => a.Id == workItem.Id))
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.AktiveAufgabenListe.Should().Contain(a => a.Id == workItem.Id,
            "der CurrentView-Setter muss AktiveAufgabenAktualisierenAsync per SafeFireAndForget auslösen");
    }

    /// <summary>NavigateToDashboard teilt die AktiveAufgabenListe mit dem DashboardViewModel, statt eine eigene Abfrage im Dashboard auszulösen (einzige gemeinsame Datenquelle).</summary>
    [Fact]
    public void NavigateToDashboard_ShouldShareAktiveAufgabenListeWithDashboardViewModel_WhenCalled()
    {
        // Arrange & Act (Konstruktor navigiert bereits zum Dashboard)
        var sut = CreateSut();

        // Assert
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;
        dashboardViewModel.AktiveAufgabenListe.Should().BeSameAs(sut.AktiveAufgabenListe);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync markiert genau die aktuell angezeigte Aufgabe als aktiv.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMarkCurrentTaskAsActive_WhenTaskDetailIsCurrentView()
    {
        // Arrange
        var erste = await _workItemService.CreateAsync(_projektId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var zweite = await _workItemService.CreateAsync(_projektId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(erste.Id, "feature/erste", "/tmp/erste", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(zweite.Id, "feature/zweite", "/tmp/zweite", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(zweite.Id);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.IsAktiv);
        sut.AktiveAufgabenListe.Single(a => a.IsAktiv).Id.Should().Be(zweite.Id);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync wechselt die aktive Markierung nach Navigation zu einer anderen Aufgabe.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMoveActiveMarker_WhenNavigatingToAnotherTask()
    {
        // Arrange
        var erste = await _workItemService.CreateAsync(_projektId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var zweite = await _workItemService.CreateAsync(_projektId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(erste.Id, "feature/erste", "/tmp/erste", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(zweite.Id, "feature/zweite", "/tmp/zweite", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(erste.Id);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(zweite.Id);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.IsAktiv);
        sut.AktiveAufgabenListe.Single(a => a.IsAktiv).Id.Should().Be(zweite.Id);
        sut.AktiveAufgabenListe.Single(a => a.Id == erste.Id).IsAktiv.Should().BeFalse();
    }

    /// <summary>AktiveAufgabenAktualisierenAsync behaelt die aktive Markierung auch nach erneutem Laden der Liste.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldKeepActiveMarker_WhenListIsRefreshed()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Refresh-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/refresh", "/tmp/refresh", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        ((RelayCommand<Guid>)sut.NavigateZuAufgabeCommand).Execute(workItem.Id);

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.IsAktiv);
        sut.AktiveAufgabenListe.Single(a => a.IsAktiv).Id.Should().Be(workItem.Id);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync markiert keine Aufgabe aktiv, wenn keine Aufgabendetailansicht angezeigt wird.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldNotMarkAnyTaskActive_WhenCurrentViewIsDashboard()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Dashboard-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dashboard", "/tmp/dashboard", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Should().NotContain(a => a.IsAktiv);
    }

    /// <summary>AktiveAufgabenAktualisierenAsync löst SCM- und KI-Plugin-Anzeigenamen pro Aufgabe auf.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldResolvePluginNamesPerTask_WhenPrefixesAreKnown()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        var aiPlugin = new Mock<IKiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        aiPlugin.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projektId,
            RepositoryName = "repo",
            RepositoryUrl = "https://example.invalid/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        });
        var workItem = await _workItemService.CreateAsync(_projektId, "Plugin-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/plugin", "/tmp/plugin", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Should().ContainSingle().Subject;
        item.ScmPluginName.Should().Be("GitHub");
        item.KiPluginName.Should().Be("Codex");
    }

    /// <summary>AktiveAufgabenAktualisierenAsync fällt bei unbekannten Plugin-Prefixen auf den gespeicherten Prefix zurück.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldFallbackToStoredPrefix_WhenPluginIsUnknown()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Fallback-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/fallback", "/tmp/fallback", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Should().ContainSingle().Subject;
        item.KiPluginName.Should().Be("Unbekannt.KI");
    }

    /// <summary>AktiveAufgabenAktualisierenAsync nutzt bei fehlenden gespeicherten Plugin-Prefixen keinen globalen Default.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldNotUseGlobalDefaults_WhenPluginPrefixesAreMissing()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        var aiPlugin = new Mock<IKiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        aiPlugin.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var workItem = await _workItemService.CreateAsync(_projektId, "Ohne Prefix", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/ohne-prefix", "/tmp/ohne-prefix", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Should().ContainSingle().Subject;
        item.ScmPluginName.Should().BeNull();
        item.KiPluginName.Should().BeNull();
    }

    /// <summary>NavigateToDashboard setzt die NavigateZuAufgabeAction des DashboardViewModel, sodass dessen NavigateZuAufgabeCommand zur Aufgabendetailansicht navigiert (Delegate-Muster statt versteckter Kopplung).</summary>
    [Fact]
    public async Task NavigateToDashboard_ShouldWireNavigateZuAufgabeActionOnDashboardViewModel_WhenCalled()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "Aufgabe für Dashboard-Navigation", null, ct: TestContext.Current.CancellationToken);
        var dashboardViewModel = (DashboardViewModel)sut.CurrentView!;

        // Act
        ((RelayCommand<Guid>)dashboardViewModel.NavigateZuAufgabeCommand).Execute(workItem.Id);

        // Assert
        sut.CurrentView.Should().BeOfType<TaskDetailViewModel>();
        ((TaskDetailViewModel)sut.CurrentView!).WorkItemId.Should().Be(workItem.Id);
    }

    /// <summary>Auslösen von RunningCountChanged am Mock aktualisiert die AktiveAufgabenListe.</summary>
    [Fact]
    public async Task RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised()
    {
        // Arrange
        var sut = CreateSut();
        var workItem = await _workItemService.CreateAsync(_projektId, "Event-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/event", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act
        _runningStatusSourceMock.Raise(m => m.RunningCountChanged += null, 0, 1);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.AktiveAufgabenListe.Any(a => a.Id == workItem.Id))
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.AktiveAufgabenListe.Should().Contain(a => a.Id == workItem.Id,
            "RunningCountChanged muss AktiveAufgabenAktualisierenAsync auslösen");
    }

    /// <summary>LaufdatenChanged aktualisiert eine bereits sichtbare Seitenleisten-Kachel nach persistierten Laufdaten.</summary>
    [Fact]
    public async Task LaufdatenChanged_ShouldReloadVisiblePanelItem_WhenRunDataWasPersisted()
    {
        // Arrange
        var notifier = new WorkItemRunDataChangedNotifier();
        var converter = new KiAusfuehrungsStatusConverter();
        var workItem = await _workItemService.CreateAsync(_projektId, "Laufdaten-Event-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/laufdaten-event", "/tmp/klon", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut(runDataChangedNotifier: notifier);
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);
        var initialItem = sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == workItem.Id).Subject;
        initialItem.ActiveRunId.Should().BeNull();
        converter.Convert(initialItem, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("✓ Bereit");

        // Act
        await _workItemService.SetActiveRunAsync(workItem.Id, "run-laufdaten-event", TestContext.Current.CancellationToken);
        notifier.NotifyRunDataChanged(workItem.Id);

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline
               && sut.AktiveAufgabenListe.Single(a => a.Id == workItem.Id).ActiveRunId is null)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        var refreshedItem = sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == workItem.Id).Subject;
        refreshedItem.ActiveRunId.Should().Be("run-laufdaten-event");
        refreshedItem.LastHeartbeatUtc.Should().NotBeNull();
        refreshedItem.RunStatus.Should().Be(WorkItemRunStatus.Running);
        converter.Convert(refreshedItem, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("▶ Läuft");
    }

    /// <summary>Re-Entrancy-Schutz: Ein zweiter, echt gleichzeitiger Aufruf während eine Aktualisierung noch läuft, wird übersprungen, statt erneut auf die Datenquelle zuzugreifen.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning()
    {
        // Arrange: kontrolliert verzögerte Datenquelle, damit ein Aufruf lange genug läuft, um echte Nebenläufigkeit zu erzwingen
        var workItem = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            Project = new Softwareschmiede.Domain.Entities.Project
            {
                Id = _projektId,
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
        var workItemService = new VerzoegernderAktiveAufgabenService([workItem]);

        var sut = CreateSut(workItemService);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.AktiveAufgabenListe.All(a => a.Id != workItem.Id))
            await Task.Delay(25, TestContext.Current.CancellationToken);

        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == workItem.Id,
            "der vom Konstruktor gestartete Hintergrund-Refresh soll vor der Re-Entrancy-Pruefung abgeschlossen sein");

        workItemService.Enable();
        var ersterAufrufTask = Task.Run(() => sut.AktiveAufgabenAktualisierenAsync(), TestContext.Current.CancellationToken);
        await workItemService.ErsterVerzoegerterAbruf.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act: der zweite Aufruf überlappt den noch laufenden, verzögerten ersten Aufruf
        try
        {
            await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Assert: der übersprungene Aufruf fragt die verzögerte Datenquelle kein zweites Mal ab
            workItemService.VerzoegerteAbrufe.Should().Be(1,
                "ein zweiter, gleichzeitiger Aufruf muss uebersprungen werden, statt erneut auf die verzögerte Datenquelle zuzugreifen");
        }
        finally
        {
            workItemService.Resume();
            await ersterAufrufTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Nach Dispose() löst ein erneutes RunningCountChanged kein Neuladen aus und wirft nicht.</summary>
    [Fact]
    public async Task Dispose_ShouldUnsubscribeFromRunningCountChanged()
    {
        // Arrange
        var sut = CreateSut();
        sut.Dispose();

        var workItem = await _workItemService.CreateAsync(_projektId, "Nach-Dispose-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/dispose", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => _runningStatusSourceMock.Raise(m => m.RunningCountChanged += null, 0, 1);

        // Assert
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        sut.AktiveAufgabenListe.Should().NotContain(a => a.Id == workItem.Id,
            "nach Dispose() darf RunningCountChanged keine Aktualisierung mehr auslösen");
    }

    /// <summary>Nach Dispose() löst LaufdatenChanged kein Neuladen aus und wirft nicht.</summary>
    [Fact]
    public async Task Dispose_ShouldUnsubscribeFromLaufdatenChanged()
    {
        // Arrange
        var workItem = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            Project = new Softwareschmiede.Domain.Entities.Project
            {
                Id = _projektId,
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
        var workItemService = new ZaehlenderAktiveAufgabenService([workItem]);
        var notifier = new WorkItemRunDataChangedNotifier();
        var sut = CreateSut(workItemService, runDataChangedNotifier: notifier);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && workItemService.Abrufe == 0)
            await Task.Delay(25, TestContext.Current.CancellationToken);

        var abrufeVorDispose = workItemService.Abrufe;
        sut.Dispose();

        // Act
        var act = () => notifier.NotifyRunDataChanged(workItem.Id);

        // Assert
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        workItemService.Abrufe.Should().Be(abrufeVorDispose,
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

    /// <summary>AktiveAufgabenAktualisierenAsync mappt den Repository-Namen als Untertitel und fällt ohne Repository auf den Projektnamen zurück.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapRepositoryName_WithRepoAndProjectFallback()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projektId,
            RepositoryName = "mein-repo",
            RepositoryUrl = "https://example.invalid/mein-repo.git",
            PluginType = "LocalDirectoryPlugin",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var mitRepo = await _workItemService.CreateAsync(_projektId, "Repo-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        var ohneRepo = await _workItemService.CreateAsync(_projektId, "Freie Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(mitRepo.Id, "feature/repo", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(ohneRepo.Id, "feature/frei", "/tmp/frei", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        sut.AktiveAufgabenListe.Single(a => a.Id == mitRepo.Id).RepositoryName.Should().Be("mein-repo");
        sut.AktiveAufgabenListe.Single(a => a.Id == ohneRepo.Id).RepositoryName.Should().Be("Testprojekt",
            "ohne Repository-Verknüpfung fällt der Untertitel auf den Projektnamen zurück");
    }

    /// <summary>AktiveAufgabenAktualisierenAsync mappt Plugin-Symbole; nicht aufgelöste Symbole (Mock ohne Setup liefert null) fallen auf den Fallback-Glyph zurück.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapPluginSymbols_WithFallbackGlyphTolerance()
    {
        // Arrange
        var scmPlugin = new Mock<IGitPlugin>();
        scmPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        scmPlugin.SetupGet(p => p.PluginName).Returns("GitHub");
        scmPlugin.SetupGet(p => p.PluginSymbol).Returns("🐙");
        var aiPlugin = new Mock<IKiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        // PluginSymbol absichtlich nicht aufgesetzt: Moq liefert null statt des Interface-Defaults.
        _pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([scmPlugin.Object]);
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projektId,
            RepositoryName = "repo",
            RepositoryUrl = "https://example.invalid/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projektId, "Symbol-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/symbol", "/tmp/symbol", ct: TestContext.Current.CancellationToken);
        var unbekannt = await _workItemService.CreateAsync(_projektId, "Unbekannte KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(unbekannt.Id, unbekannt.Title, unbekannt.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(unbekannt.Id, "feature/unbekannt", "/tmp/unbekannt", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var item = sut.AktiveAufgabenListe.Single(a => a.Id == workItem.Id);
        item.ScmPluginSymbol.Should().Be("🐙");
        item.KiPluginSymbol.Should().Be(IPlugin.FallbackSymbol,
            "PluginSymbol=null (Mock ohne Setup) muss auf den Fallback-Glyph gemappt werden");
        var unbekanntesItem = sut.AktiveAufgabenListe.Single(a => a.Id == unbekannt.Id);
        unbekanntesItem.KiPluginSymbol.Should().Be(IPlugin.FallbackSymbol,
            "ein nicht auflösbarer Prefix liefert ebenfalls den Fallback-Glyph");
        unbekanntesItem.ScmPluginSymbol.Should().BeNull("ohne Repository kein SCM-Symbol");
    }

    /// <summary>AktiveAufgabenAktualisierenAsync mappt KiGruppenName auf PluginName, rohen Prefix bzw. "Nicht ausgeführt".</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapKiGruppenName()
    {
        // Arrange
        var aiPlugin = new Mock<IKiPlugin>();
        aiPlugin.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.Codex");
        aiPlugin.SetupGet(p => p.PluginName).Returns("Codex");
        _pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([aiPlugin.Object]);

        var mitKi = await _workItemService.CreateAsync(_projektId, "KI-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(mitKi.Id, mitKi.Title, mitKi.RequirementDescription, "Softwareschmiede.Codex", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(mitKi.Id, "feature/ki", "/tmp/ki", ct: TestContext.Current.CancellationToken);
        var unbekannt = await _workItemService.CreateAsync(_projektId, "Unbekannte KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(unbekannt.Id, unbekannt.Title, unbekannt.RequirementDescription, "Unbekannt.KI", TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(unbekannt.Id, "feature/unbekannt", "/tmp/unbekannt", ct: TestContext.Current.CancellationToken);
        var ohneKi = await _workItemService.CreateAsync(_projektId, "Ohne KI", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(ohneKi.Id, "feature/ohne", "/tmp/ohne", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var aufgeloest = sut.AktiveAufgabenListe.Single(a => a.Id == mitKi.Id);
        aufgeloest.KiGruppenName.Should().Be("Codex");
        aufgeloest.KiGruppeIstFallback.Should().BeFalse();
        var roherPrefix = sut.AktiveAufgabenListe.Single(a => a.Id == unbekannt.Id);
        roherPrefix.KiGruppenName.Should().Be("Unbekannt.KI",
            "nicht auflösbare Prefixe bilden eine eigene Gruppe mit dem Prefix als Namen");
        roherPrefix.KiGruppeIstFallback.Should().BeFalse();
        var fallback = sut.AktiveAufgabenListe.Single(a => a.Id == ohneKi.Id);
        fallback.KiGruppenName.Should().Be(AufgabenListenAnsicht.NichtAusgefuehrt);
        fallback.KiGruppeIstFallback.Should().BeTrue();
    }

    /// <summary>AktiveAufgabenAktualisierenAsync mappt RepositoryGruppenName auf den Repository-Namen bzw. "Ohne Repository".</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapRepositoryGruppenName()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new Softwareschmiede.Domain.Entities.GitRepository
        {
            Id = repositoryId,
            ProjectId = _projektId,
            RepositoryName = "gruppen-repo",
            RepositoryUrl = "https://example.invalid/gruppen-repo.git",
            PluginType = "LocalDirectoryPlugin",
            Active = true
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var mitRepo = await _workItemService.CreateAsync(_projektId, "Repo-Aufgabe", null, repositoryId, TestContext.Current.CancellationToken);
        var ohneRepo = await _workItemService.CreateAsync(_projektId, "Freie Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(mitRepo.Id, "feature/repo", "/tmp/repo", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(ohneRepo.Id, "feature/frei", "/tmp/frei", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var mitGruppe = sut.AktiveAufgabenListe.Single(a => a.Id == mitRepo.Id);
        mitGruppe.RepositoryGruppenName.Should().Be("gruppen-repo");
        mitGruppe.RepositoryGruppeIstFallback.Should().BeFalse();
        var fallback = sut.AktiveAufgabenListe.Single(a => a.Id == ohneRepo.Id);
        fallback.RepositoryGruppenName.Should().Be(AufgabenListenAnsicht.OhneRepository);
        fallback.RepositoryGruppeIstFallback.Should().BeTrue();
    }

    /// <summary>AktiveAufgabenAktualisierenAsync mappt SortierZeitpunkt auf LetzterCliStartUtc ?? ErstellungsDatum.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_ShouldMapSortierZeitpunkt()
    {
        // Arrange
        var workItem = await _workItemService.CreateAsync(_projektId, "Sortier-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/sort", "/tmp/sort", ct: TestContext.Current.CancellationToken);
        // StartenAsync setzt LetzterCliStartUtc bewusst auf null — der Zeitstempel entsteht erst
        // beim echten CLI-Prozessstart (CliProcessManager → AktivenLaufSetzenAsync).
        await _workItemService.SetActiveRunAsync(workItem.Id, Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        await sut.AktiveAufgabenAktualisierenAsync(TestContext.Current.CancellationToken);

        // Assert
        var gelesen = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var item = sut.AktiveAufgabenListe.Single(a => a.Id == workItem.Id);
        gelesen!.LastCliStartUtc.Should().NotBeNull(
            "die gestartete Aufgabe muss einen CLI-Startzeitpunkt haben, damit der Test den ??-Zweig belegt");
        item.SortierZeitpunkt.Should().BeCloseTo(gelesen.LastCliStartUtc!.Value, TimeSpan.FromMilliseconds(50),
            "SortierZeitpunkt muss auf LetzterCliStartUtc ?? ErstellungsDatum mappen");
        item.SortierZeitpunkt.Should().NotBe(gelesen.CreatedAt);
    }

    /// <summary>AufgabenListenModus startet mit dem Default Datum.</summary>
    [Fact]
    public void AufgabenListenModus_ShouldDefaultToDatum()
    {
        var sut = CreateSut();

        sut.AufgabenListenModus.Should().Be(AufgabenListenModus.Datum);
    }

    /// <summary>AufgabenListenModus ist setzbar und löst PropertyChanged aus.</summary>
    [Fact]
    public void AufgabenListenModus_ShouldBeSettableAndRaisePropertyChanged()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.AufgabenListenModus = AufgabenListenModus.Repository;

        sut.AufgabenListenModus.Should().Be(AufgabenListenModus.Repository);
        changed.Should().Contain(nameof(MainWindowViewModel.AufgabenListenModus));
    }

    /// <summary>Ein persistierter Modus wird über GespeichertenAufgabenListenModusLadenAsync wiederhergestellt.</summary>
    [Fact]
    public async Task AufgabenListenModus_ShouldRestorePersistedModus()
    {
        // Arrange
        var (modusService, provider) = AufgabenListenModusServiceTestFactory.ErstelleModusService();
        using (provider)
        {
            using (var scope = provider.CreateScope())
            {
                var settings = scope.ServiceProvider.GetRequiredService<AppSettingService>();
                await settings.SetSettingAsync(AppSettingService.ActiveWorkItemsListModeKey, "Repository", TestContext.Current.CancellationToken);
            }

            var sut = CreateSut(aufgabenListenModusService: modusService);

            // Act
            await sut.GespeichertenAufgabenListenModusLadenAsync(TestContext.Current.CancellationToken);

            // Assert
            sut.AufgabenListenModus.Should().Be(AufgabenListenModus.Repository);
        }
    }

    /// <summary>Eine Modusänderung wird über den Service persistiert; die Wiederherstellung schreibt nicht zurück.</summary>
    [Fact]
    public async Task AufgabenListenModus_ShouldPersistModusOnChange_WithoutWriteBackOnRestore()
    {
        // Arrange
        var (modusService, provider) = AufgabenListenModusServiceTestFactory.ErstelleModusService();
        using (provider)
        {
            var sut = CreateSut(aufgabenListenModusService: modusService);

            // Act: Benutzerumschaltung persistiert den neuen Modus.
            sut.AufgabenListenModus = AufgabenListenModus.Ki;

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
            await sut.GespeichertenAufgabenListenModusLadenAsync(TestContext.Current.CancellationToken);
            sut.AufgabenListenModus.Should().Be(AufgabenListenModus.Ki);
        }
    }

    /// <summary>Test-Datenquelle, die aktive Aufgaben kontrolliert blockierend zurueckgibt.</summary>
    private sealed class VerzoegernderAktiveAufgabenService : IActiveWorkItemsService
    {
        private readonly ManualResetEventSlim _fortsetzen = new();
        private readonly TaskCompletionSource _ersterVerzoegerterAbruf = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Softwareschmiede.Domain.Entities.WorkItem> _aufgaben;
        private int _aktiv;
        private int _verzoegerteAbrufe;

        /// <summary>Initialisiert eine neue Test-Datenquelle.</summary>
        /// <param name="workItems">Die zurueckzugebenden Aufgaben.</param>
        public VerzoegernderAktiveAufgabenService(List<Softwareschmiede.Domain.Entities.WorkItem> workItems)
        {
            _aufgaben = workItems;
        }

        /// <summary>Task, der abgeschlossen wird, sobald der erste künstlich verzögerte Abruf erreicht wurde.</summary>
        public Task ErsterVerzoegerterAbruf => _ersterVerzoegerterAbruf.Task;

        /// <summary>Anzahl der Abrufe, die nach Aktivierung der künstlichen Verzögerung erreicht wurden.</summary>
        public int VerzoegerteAbrufe => Volatile.Read(ref _verzoegerteAbrufe);

        /// <summary>Aktiviert die kuenstliche Verzoegerung fuer nachfolgende Abrufe.</summary>
        public void Enable() => Volatile.Write(ref _aktiv, 1);

        /// <summary>Gibt blockierte Abrufe wieder frei.</summary>
        public void Resume() => _fortsetzen.Set();

        /// <inheritdoc />
        public Task<List<Softwareschmiede.Domain.Entities.WorkItem>> GetActiveWorkItemsAsync(CancellationToken ct = default)
        {
            if (Volatile.Read(ref _aktiv) == 1)
            {
                Interlocked.Increment(ref _verzoegerteAbrufe);
                _ersterVerzoegerterAbruf.TrySetResult();
                _fortsetzen.Wait(ct);
            }

            return Task.FromResult(_aufgaben);
        }
    }

    /// <summary>Test-Datenquelle, die Abrufe zaehlt.</summary>
    private sealed class ZaehlenderAktiveAufgabenService : IActiveWorkItemsService
    {
        private readonly List<Softwareschmiede.Domain.Entities.WorkItem> _aufgaben;
        private int _abrufe;

        /// <summary>Initialisiert eine neue zaehlende Test-Datenquelle.</summary>
        /// <param name="workItems">Die zurueckzugebenden Aufgaben.</param>
        public ZaehlenderAktiveAufgabenService(List<Softwareschmiede.Domain.Entities.WorkItem> workItems)
        {
            _aufgaben = workItems;
        }

        /// <summary>Anzahl der Abrufe.</summary>
        public int Abrufe => Volatile.Read(ref _abrufe);

        /// <inheritdoc />
        public Task<List<Softwareschmiede.Domain.Entities.WorkItem>> GetActiveWorkItemsAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _abrufe);
            return Task.FromResult(_aufgaben);
        }
    }
}
