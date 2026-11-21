using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für ProjectDetailViewModel.</summary>
public sealed class ProjectDetailViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests.</summary>
    public ProjectDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectDetailViewModel CreateSut(
        Action? backAction = null,
        Func<Task>? projectAddedCallback = null,
        Action<TaskDetailViewModel>? navigateToTaskViewCallback = null,
        Action? navigateBackToProjectCallback = null)
    {
        var vm = new ProjectDetailViewModel(
            _projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
        vm.BackAction = backAction;
        vm.RefreshProjectListCallback = projectAddedCallback;
        vm.NavigateToTaskViewCallback = navigateToTaskViewCallback;
        vm.NavigateBackToProjectCallback = navigateBackToProjectCallback;
        return vm;
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _workItemService);

    /// <summary>ProjektSpeichernAsync ruft CreateAsync auf und navigiert zurück, wenn die ID leer ist.</summary>
    [Fact]
    public async Task SaveProjectAsync_CreatesNewProject_WhenIdEmpty()
    {
        // Arrange
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.ProjectName = "Neues Projekt";
        sut.ProjectDescription = "Eine Beschreibung";

        // Act
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        // Assert: BackAction wird nach Erstellung aufgerufen
        backInvoked.Should().BeTrue();
        // Projekt ist in der DB vorhanden
        var projects = await _projectService.GetAllAsync(TestContext.Current.CancellationToken);
        projects.Should().Contain(p => p.Name == "Neues Projekt");
    }

    /// <summary>ProjektSpeichernAsync ruft UpdateAsync auf, wenn eine ID gesetzt ist.</summary>
    [Fact]
    public async Task SaveProjectAsync_UpdatesExistingProject_WhenIdExisting()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Alter Name", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ProjectName = "Neuer Name";
        sut.ProjectDescription = "Neue Beschreibung";

        // Act
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        // Assert
        var updates = await _projectService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
        updates!.Name.Should().Be("Neuer Name");
        updates.Description.Should().Be("Neue Beschreibung");
    }

    /// <summary>ProjektDeleteAsync ruft DeleteAsync und BackAction auf.</summary>
    [Fact]
    public async Task DeleteProjectAsync_Success_CallsDeleteAsyncAndBackActionOn()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Zu löschendes Projekt", null, TestContext.Current.CancellationToken);
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.ProjectId = project.Id;
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        // Assert
        backInvoked.Should().BeTrue();
        var deleted = await _projectService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    /// <summary>ProjektDeleteAsync ruft weder DeleteAsync noch BackAction auf, wenn Benutzer abbricht.</summary>
    [Fact]
    public async Task DeleteProjectAsync_Aborted_CallsDeleteAsyncNotOn()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Bestehendes Projekt", null, TestContext.Current.CancellationToken);
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.ProjectId = project.Id;
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        // Assert
        backInvoked.Should().BeFalse();
        var stillExisting = await _projectService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
        stillExisting.Should().NotBeNull();
    }

    /// <summary>SpeichernCommand ist nicht ausführbar, wenn der Projektname leer ist.</summary>
    [Fact]
    public void SaveProjectAsync_ValidationError_CanExecuteFalse_WhenNameEmpty()
    {
        // Arrange
        var sut = CreateSut();
        sut.ProjectName = string.Empty;

        // Act & Assert
        sut.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>SpeichernCommand ist nicht ausführbar, wenn der Projektname nur Leerzeichen enthält.</summary>
    [Fact]
    public void SaveProjectAsync_ValidationError_CanExecuteFalse_WhenNameOnlyWhitespace()
    {
        // Arrange
        var sut = CreateSut();
        sut.ProjectName = "   ";

        // Act & Assert
        sut.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>ProjektSpeichernAsync ruft ProjektHinzugefuegtCallback auf, nachdem ein neues Projekt erstellt wurde.</summary>
    [Fact]
    public async Task SaveProjectAsync_Success_CallsProjectAddedCallbackOn()
    {
        // Arrange
        var callbackInvoked = false;
        var sut = CreateSut(projectAddedCallback: () => { callbackInvoked = true; return Task.CompletedTask; });
        sut.ProjectName = "Callback-Test-Projekt";

        // Act
        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    /// <summary>Der ProjektId-Setter löst LadenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task ProjectId_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var project = await _projectService.CreateAsync("FireAndForget-Projekt", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act: ProjektId-Setter löst SafeFireAndForget(LadenAsync) aus, ohne explizites Awaiten von LoadCommand
        sut.ProjectId = project.Id;

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Project == null)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.Project.Should().NotBeNull("der ProjektId-Setter muss LadenAsync per SafeFireAndForget auslösen");
        sut.Project!.Name.Should().Be("FireAndForget-Projekt");
    }

    /// <summary>RepositoryZuweisenAsync ruft AddRepositoryAsync auf und aktualisiert SelectedRepository, wenn der Dialog bestätigt wird.</summary>
    [Fact]
    public async Task AssignRepositoryAsync_Success_CallsAddRepositoryAsyncOnAndUpdatesViewModel()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Repository-Test-Projekt", null, TestContext.Current.CancellationToken);

        var testRepo = new AvailableRepository("test-repo", DateTime.UtcNow, "test/repo", "https://github.com/test/repo");

        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        pluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");

        var repositoryAssignVm = new RepositoryAssignViewModel(NullLogger<RepositoryAssignViewModel>.Instance);
        repositoryAssignVm.SelectedRepository = testRepo;
        repositoryAssignVm.SelectedScmPlugin = pluginMock.Object;

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(RepositoryAssignViewModel)))
            .Returns(repositoryAssignVm);

        _dialogServiceMock
            .Setup(d => d.RepositoryAssignDialog(It.IsAny<RepositoryAssignViewModel>()))
            .Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.AssignRepositoryCommand).ExecuteAsync();

        // Assert: Service hat AddRepositoryAsync aufgerufen
        var detail = await _projectService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        detail!.Repositories.Should().HaveCount(1);
        detail.Repositories[0].RepositoryUrl.Should().Be("https://github.com/test/repo");

        // Assert: ViewModel hat SelectedRepository aktualisiert
        sut.SelectedRepository.Should().NotBeNull();
    }

    /// <summary>RepositoryZuweisenAsync persistiert das im Dialog ausgewählte Arbeitsverzeichnis.</summary>
    [Fact]
    public async Task AssignRepositoryAsync_Success_PersistedSelectedWorkingDirectory()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Repository-Arbeitsverzeichnis-Projekt", null, TestContext.Current.CancellationToken);

        var testRepo = new AvailableRepository("test-repo", DateTime.UtcNow, "test/repo", "https://github.com/test/repo-workdir");

        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        pluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");

        var repositoryAssignVm = new RepositoryAssignViewModel(NullLogger<RepositoryAssignViewModel>.Instance);
        repositoryAssignVm.SelectedRepository = testRepo;
        repositoryAssignVm.SelectedScmPlugin = pluginMock.Object;
        repositoryAssignVm.SelectedWorkingDirectory = "backend";

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(RepositoryAssignViewModel)))
            .Returns(repositoryAssignVm);

        _dialogServiceMock
            .Setup(d => d.RepositoryAssignDialog(It.IsAny<RepositoryAssignViewModel>()))
            .Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.AssignRepositoryCommand).ExecuteAsync();

        // Assert
        var detail = await _projectService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        var repository = detail!.Repositories.Single();
        var startConfiguration = await _projectService.GetRepositoryStartConfigurationAsync(repository.Id, TestContext.Current.CancellationToken);

        startConfiguration.Should().NotBeNull();
        startConfiguration!.WorkingDirectoryRelativePath.Should().Be("backend");
    }

    /// <summary>RepositoryOeffnenCommand hat CanExecute false, wenn kein Repository geladen ist.</summary>
    [Fact]
    public void OpenRepositoryCommand_CanExecuteFalse_WithoutRepository()
    {
        // Arrange
        var sut = CreateSut();

        // Assert
        sut.OpenRepositoryCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>RepositoryOeffnenCommand hat CanExecute true, nachdem ein Projekt mit Repository geladen wurde.</summary>
    [Fact]
    public async Task OpenRepositoryCommand_CanExecuteTrue_WhenRepositoryLoaded()
    {
        // Arrange
        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            PluginType = "GitHub",
            RepositoryUrl = "https://github.com/test/repo",
            RepositoryName = "test-repo"
        };

        var project = await _projectService.CreateAsync("URL-Test-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, repo.PluginType, repo.RepositoryUrl, repo.RepositoryName, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert: Nach dem Laden ist CanExecute true
        sut.OpenRepositoryCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>OpenWorkItemCommand ruft NavigateToTaskViewCallback auf, wenn eine Aufgabe ausgewählt wird.</summary>
    [Fact]
    public async Task OpenWorkItem_CallsNavigateToTaskViewCallback_WhenWorkItemSelected()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Navigations-Test-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigatesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigatesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.OpenWorkItemCommand.Execute(workItem.Id);

        // Assert
        navigatesViewModel.Should().NotBeNull();
    }

    /// <summary>Beim Öffnen einer Aufgabe werden BackAction und RefreshWorkItemListCallback auf dem neuen TaskDetailViewModel gesetzt.</summary>
    [Fact]
    public async Task NavigateToTaskViewCallback_SetsBackActionAndCallbackOnTaskVM()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Navigations-Test-Projekt-2", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigatesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigatesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.OpenWorkItemCommand.Execute(workItem.Id);

        // Assert
        navigatesViewModel.Should().NotBeNull();
        navigatesViewModel!.BackAction.Should().NotBeNull();
        navigatesViewModel.RefreshWorkItemListCallback.Should().NotBeNull();
    }

    /// <summary>CreateWorkItemCommand erstellt eine Aufgabe mit Status Neu und navigiert zur TaskDetailView.</summary>
    [Fact]
    public async Task CreateWorkItem_CreatesTaskWithStatusNewAndNavigates()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Erstellungs-Test-Projekt", null, TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigatesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigatesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.CreateWorkItemCommand).ExecuteAsync();

        // Assert
        navigatesViewModel.Should().NotBeNull();
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().ContainSingle(a => a.Status == WorkItemStatus.New);
    }

    /// <summary>CreateWorkItemCommand uebernimmt das im Projekt ausgewaehlte Repository.</summary>
    [Fact]
    public async Task CreateWorkItem_AssignsSelectedRepositoryToNewTask()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Erstellungs-Test-Projekt mit Repository", null, TestContext.Current.CancellationToken);
        var repository = await _projectService.AddRepositoryAsync(project.Id, "Softwareschmiede.GitHub", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.CreateWorkItemCommand).ExecuteAsync();

        // Assert
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().ContainSingle(a => a.GitRepositoryId == repository.Id);
    }

    /// <summary>ReloadAufgabenListAsync (über RefreshWorkItemListCallback) aktualisiert nur das geänderte Element in der Aufgabenliste.</summary>
    [Fact]
    public async Task ReloadWorkItemsList_UpdatesSingleItemAsync_WhenCallbackInvoked()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Reload-Test-Projekt", null, TestContext.Current.CancellationToken);
        var aufgabe1 = await _workItemService.CreateAsync(project.Id, "Aufgabe 1", "Beschreibung 1", ct: TestContext.Current.CancellationToken);
        var aufgabe2 = await _workItemService.CreateAsync(project.Id, "Aufgabe 2", "Beschreibung 2", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigatesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigatesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.OpenWorkItemCommand.Execute(aufgabe1.Id);

        // Aufgabe 1 wird extern geändert (z. B. durch Speichern in TaskDetailView)
        await _workItemService.UpdateAsync(aufgabe1.Id, "Aufgabe 1 - Geändert", "Beschreibung 1", ct: TestContext.Current.CancellationToken);

        // Act: Callback ausführen wie es TaskDetailViewModel nach dem Speichern tut
        await navigatesViewModel!.RefreshWorkItemListCallback!.Invoke();

        // Assert: nur Aufgabe 1 wurde aktualisiert, Aufgabe 2 unverändert, keine Duplikate
        sut.WorkItems.Should().HaveCount(2);
        sut.WorkItems.Should().ContainSingle(a => a.Id == aufgabe1.Id && a.Title == "Aufgabe 1 - Geändert");
        sut.WorkItems.Should().ContainSingle(a => a.Id == aufgabe2.Id && a.Title == "Aufgabe 2");
    }

    /// <summary>LadenAsync trennt nicht beendete und beendete Aufgaben nach Status.</summary>
    [Fact]
    public async Task LoadAsync_DisconnectsNotFinishedAndFinishedWorkItems()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Status-Trennung-Projekt", null, TestContext.Current.CancellationToken);
        var newItem = await _workItemService.CreateAsync(project.Id, "Neu", null, ct: TestContext.Current.CancellationToken);
        var started = await _workItemService.CreateAsync(project.Id, "Gestartet", null, ct: TestContext.Current.CancellationToken);
        var waiting = await _workItemService.CreateAsync(project.Id, "Wartend", null, ct: TestContext.Current.CancellationToken);
        var completed = await _workItemService.CreateAsync(project.Id, "Beendet", null, ct: TestContext.Current.CancellationToken);
        var archived = await _workItemService.CreateAsync(project.Id, "Archiviert", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SetStatusUncheckedAsync(started.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(waiting.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(completed.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(archived.Id, WorkItemStatus.Archived, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.NotFinishedWorkItems.Select(a => a.Id).Should().BeEquivalentTo([newItem.Id, started.Id, waiting.Id]);
        sut.FinishedWorkItems.Select(a => a.Id).Should().BeEquivalentTo([completed.Id]);
        sut.NotFinishedWorkItems.Should().NotContain(a => a.Id == archived.Id);
        sut.FinishedWorkItems.Should().NotContain(a => a.Id == archived.Id);
    }

    /// <summary>LadenAsync befüllt bei einem Projekt ohne Aufgaben leere getrennte Listen.</summary>
    [Fact]
    public async Task LoadAsync_EmptyProject_HasEmptyDisconnectedWorkitemslist()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Leeres-Projekt", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.NotFinishedWorkItems.Should().BeEmpty();
        sut.FinishedWorkItems.Should().BeEmpty();
    }

    /// <summary>LadenAsync befüllt bei nur beendeten Aufgaben ausschließlich die beendete Liste.</summary>
    [Fact]
    public async Task LoadAsync_OnlyFinishedWorkItems_FillsOnlyFinishedList()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Nur-Beendete-Projekt", null, TestContext.Current.CancellationToken);
        var beendet1 = await _workItemService.CreateAsync(project.Id, "Beendet 1", null, ct: TestContext.Current.CancellationToken);
        var beendet2 = await _workItemService.CreateAsync(project.Id, "Beendet 2", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SetStatusUncheckedAsync(beendet1.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(beendet2.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.NotFinishedWorkItems.Should().BeEmpty();
        sut.FinishedWorkItems.Select(a => a.Id).Should().BeEquivalentTo([beendet1.Id, beendet2.Id]);
    }

    /// <summary>ReloadAufgabenListAsync aktualisiert die getrennten Listen nach einem Statuswechsel.</summary>
    [Fact]
    public async Task ReloadWorkItemsList_UpdatesDisconnectedListsAtStatuschange()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Reload-Status-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Statuswechsel", null, ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigatesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigatesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.OpenWorkItemCommand.Execute(workItem.Id);
        await _workItemService.SetStatusUncheckedAsync(workItem.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        // Act
        await navigatesViewModel!.RefreshWorkItemListCallback!.Invoke();

        // Assert
        sut.WorkItems.Should().ContainSingle(a => a.Id == workItem.Id);
        sut.NotFinishedWorkItems.Should().NotContain(a => a.Id == workItem.Id);
        sut.FinishedWorkItems.Should().ContainSingle(a => a.Id == workItem.Id);
    }

    // --- Issue-Laden ---

    private static Issue CreateIssue(int number = 1, string title = "Issue-Titel")
        => new(number, title, "Body", [], null, "https://github.com/test/repo/issues/" + number);

    private static ScmAlert CreateAlert(int number = 7, string title = "CodeQL Alert")
        => new(
            number,
            $"github:code-scanning:test/repo:{number}",
            ScmAlertType.CodeScanning,
            title,
            "Alert-Beschreibung",
            $"https://github.com/test/repo/security/code-scanning/{number}",
            "high",
            "open",
            "CodeQL",
            "rule/id",
            "Rule Name",
            "src/File.cs",
            12);

    private Mock<IGitPlugin> SetupGitPlugin(IEnumerable<Issue>? issues = null)
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("SourceCodeManagement");
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues ?? []);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins())
            .Returns([gitPluginMock.Object]);
        return gitPluginMock;
    }

    /// <summary>LadenIssuesAsync lädt Issues wenn IGitPlugin vorhanden ist.</summary>
    [Fact]
    public async Task LoadIssuesAsync_LoadsIssuesWhenRepositorySupportsIssues()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Issue-Test-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        SetupGitPlugin([CreateIssue(1), CreateIssue(2)]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.IssueSuggestions.Should().HaveCount(2);
        sut.IsLoadingIssues.Should().BeFalse();
    }

    /// <summary>LadenIssuesAsync gibt leere Liste zurück wenn kein SCM-Plugin vorhanden ist.</summary>
    [Fact]
    public async Task LoadIssuesAsync_ReturnsEmptyListWhenPluginDoesNotSupport()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Kein-Plugin-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.IssueSuggestions.Should().BeEmpty();
    }

    /// <summary>LadenIssuesAsync handhabt Exceptions gracefully: IssueSuggestions bleibt leer, IsLoadingIssues = false.</summary>
    [Fact]
    public async Task LoadIssuesAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Exception-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Netzwerkfehler"));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.IssueSuggestions.Should().BeEmpty();
        sut.IsLoadingIssues.Should().BeFalse();
    }

    /// <summary>LadenIssuesAsync filtert Issues heraus, deren Nummer bereits in Aufgaben als IssueReferenz vorhanden ist.</summary>
    [Fact]
    public async Task LoadIssuesAsync_FilterOutAlreadyConvertedIssues()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Filter-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);

        var existingIssue = CreateIssue(1);
        await _workItemService.CreateFromIssueAsync(project.Id, existingIssue, ct: TestContext.Current.CancellationToken);

        SetupGitPlugin([CreateIssue(1), CreateIssue(2)]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert: Issue #1 wurde gefiltert, nur Issue #2 bleibt
        sut.IssueSuggestions.Should().HaveCount(1);
        sut.IssueSuggestions[0].Number.Should().Be(2);
    }

    /// <summary>AufgabeAusIssueErstellenCommand erstellt Aufgabe, entfernt Issue aus Vorschlaegen und fügt sie zu Aufgaben hinzu.</summary>
    [Fact]
    public async Task CreateWorkItemOutIssueAsync_CreatesWorkItemAndRemovesFromSuggestions()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Konvertierung-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        var issue = CreateIssue(42, "Mein Issue");
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await sut.CreateWorkItemFromIssueCommand.ExecuteAsync(issue);

        // Assert
        sut.IssueSuggestions.Should().BeEmpty();
        sut.WorkItems.Should().Contain(a => a.Title == "Mein Issue");
        var workItem = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItem.Should().Contain(a => a.Title == "Mein Issue");
    }

    /// <summary>AufgabeAusIssueErstellenCommand tut nichts wenn Benutzer den Bestätigungsdialog abbricht.</summary>
    [Fact]
    public async Task CreateWorkItemOutIssueAsync_UserCancellation_DoesNothing()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Abbruch-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        var issue = CreateIssue(99);
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var vorschlaegBefore = sut.IssueSuggestions.Count;

        // Act
        await sut.CreateWorkItemFromIssueCommand.ExecuteAsync(issue);

        // Assert: Keine Aufgabe erstellt, Vorschlage unverändert
        sut.IssueSuggestions.Should().HaveCount(vorschlaegBefore);
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().BeEmpty();
    }

    /// <summary>LadenOffeneAnforderungenAsync lädt Issues und Alerts gemeinsam und filtert bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task LoadOpenRequirementAsync_LoadsIssuesAndAlertsAndFilterConvertedAlerts()
    {
        var project = await _projectService.CreateAsync("Alert-Laden-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var convertedAlert = CreateAlert(7);
        await _workItemService.CreateFromAlertAsync(project.Id, convertedAlert, CreateIssue(77, "Created issue"), ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([CreateIssue(1)], [convertedAlert, CreateAlert(8, "Neuer Alert")]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IssueSuggestions.Should().ContainSingle(i => i.Number == 1);
        sut.OpenRequirement.Should().HaveCount(2);
        sut.OpenRequirement.Any(a => a.Kind == ScmRequirementKind.Issue && a.Issue != null && a.Issue.Number == 1).Should().BeTrue();
        sut.OpenRequirement.Any(a => a.Kind == ScmRequirementKind.Alert && a.Alert != null && a.Alert.AlertNumber == 8).Should().BeTrue();
        sut.OpenRequirement.Should().NotContain(a => a.SourceKey == convertedAlert.SourceKey);
    }

    /// <summary>CreateWorkItemFromRequirementCommand erstellt bei Alerts erst ein GitHub-Issue und danach die lokale Aufgabe.</summary>
    [Fact]
    public async Task CreateWorkItemOutRequirementAsync_ShouldCreateExternalIssueBeforeLocalTask_WhenRequirementIsAlert()
    {
        var project = await _projectService.CreateAsync("Alert-Konvertierung-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = CreateAlert(9, "Unsichere Abfrage");
        var issueBody = string.Join(Environment.NewLine, new[]
        {
            "Automatisch aus einem GitHub-Code-Scanning-Alert erstellt.",
            string.Empty,
            "Alert-Typ: CodeScanning",
            "Severity: high",
            "Status: open",
            "Tool: CodeQL",
            "Rule: Rule Name",
            "Betroffener Ort: src/File.cs:10",
            "Alert-URL: https://github.com/test/repo/security/code-scanning/9",
            string.Empty,
            "Alert-Beschreibung"
        });
        var plugin = new TestScmPlugin([], [alert])
        {
            CreatedIssue = new Issue(200, "Code scanning alert: Unsichere Abfrage", issueBody, [], null, "https://github.com/test/repo/issues/200")
        };
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await sut.CreateWorkItemFromRequirementCommand.ExecuteAsync(sut.OpenRequirement.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.OpenRequirement.Should().BeEmpty();
        var workItem = (await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken)).Single();
        workItem.Title.Should().Be("Unsichere Abfrage");
        workItem.RequirementDescription.Should().Be(issueBody);
        workItem.IssueReference!.IssueNumber.Should().Be(200);
        workItem.AlertReference!.SourceKey.Should().Be(alert.SourceKey);
    }

    /// <summary>CreateWorkItemFromRequirementCommand erzeugt kein zweites externes Issue für bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task CreateWorkItemOutRequirementAsync_ShouldNotCreateExternalIssue_WhenAlertSourceKeyAlreadyExists()
    {
        var project = await _projectService.CreateAsync("Alert-Duplikat-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = CreateAlert(9, "Unsichere Abfrage");
        await _workItemService.CreateFromAlertAsync(project.Id, alert, new Issue(200, "Code scanning alert: Unsichere Abfrage", "Body", [], null, "https://github.com/test/repo/issues/200"), ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([], [alert]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await sut.CreateWorkItemFromRequirementCommand.ExecuteAsync(ScmRequirement.FromAlert(alert));

        plugin.CreateIssueCalls.Should().Be(0);
        sut.WorkItems.Count(a => a.AlertReference?.SourceKey == alert.SourceKey).Should().Be(1);
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Count(a => a.AlertReference?.SourceKey == alert.SourceKey).Should().Be(1);
    }

    /// <summary>CreateWorkItemFromRequirementCommand erstellt bei fehlgeschlagener Issue-Anlage keine lokale Aufgabe.</summary>
    [Fact]
    public async Task CreateWorkItemOutRequirementAsync_ShouldNotCreateLocalTask_WhenExternalIssueCreationFails()
    {
        var project = await _projectService.CreateAsync("Alert-Fehler-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = CreateAlert(9);
        var plugin = new TestScmPlugin([], [alert])
        {
            CreateIssueResult = IssueCreateResult.Failed("permission denied")
        };
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await sut.CreateWorkItemFromRequirementCommand.ExecuteAsync(sut.OpenRequirement.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.ErrorMessage.Should().Contain("permission denied");
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().BeEmpty();
    }

    /// <summary>CreateWorkItemFromRequirementCommand erzeugt bei Benutzerabbruch weder GitHub-Issue noch lokale Aufgabe.</summary>
    [Fact]
    public async Task CreateWorkItemOutRequirementAsync_ShouldDoNothing_WhenUserCancelsAlertConversion()
    {
        var project = await _projectService.CreateAsync("Alert-Abbruch-Projekt", null, TestContext.Current.CancellationToken);
        await _projectService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([], [CreateAlert(9)]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await sut.CreateWorkItemFromRequirementCommand.ExecuteAsync(sut.OpenRequirement.Single());

        plugin.CreateIssueCalls.Should().Be(0);
        var workItems = await _workItemService.GetByProjectAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().BeEmpty();
    }

    private sealed class TestScmPlugin(
        IEnumerable<Issue> issues,
        IEnumerable<ScmAlert> alerts) : IGitPlugin, IScmAlertProvider, IIssueCreateProvider
    {
        public int CreateIssueCalls { get; private set; }
        public Issue? CreatedIssue { get; init; }
        public IssueCreateResult? CreateIssueResult { get; init; }
        public string PluginName => "Test SCM";
        public string PluginPrefix => "SourceCodeManagement";
        public PluginType PluginType => PluginType.SourceCodeManagement;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
        public Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(issues);
        public Task<IEnumerable<ScmAlert>> GetAlertsAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(alerts);
        public Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IssueCreateResult> CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct = default)
        {
            CreateIssueCalls++;
            return Task.FromResult(CreateIssueResult ?? IssueCreateResult.Success(CreatedIssue ?? new Issue(999, request.Title, request.Body, [], null, "https://github.com/test/repo/issues/999")));
        }

        public Task CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBranchAsync(string localPath, string branchName, CancellationToken ct = default) => Task.CompletedTask;
        public Task PullAsync(string localPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PullRequest> CreatePullRequestAsync(string repositoryId, string branchName, string? baseBranch, string title, string body, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitAsync(string localPath, string message, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IEnumerable<string>> GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<string>());
        public Task<string> GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct = default) => Task.FromResult("main");
        public Task CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<AvailableRepository>> GetAvailableRepositoriesAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<AvailableRepository>());
    }
}
