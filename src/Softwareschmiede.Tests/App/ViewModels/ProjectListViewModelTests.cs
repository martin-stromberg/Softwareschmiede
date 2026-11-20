using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für ProjectListViewModel.</summary>
public sealed class ProjectListViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectListViewModelTests.</summary>
    public ProjectListViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        _pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance, _pluginManagerMock.Object);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectListViewModel CreateSut()
    {
        return new ProjectListViewModel(
            _projectService,
            _serviceProviderMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectListViewModel>.Instance);
    }

    private ProjectDetailViewModel CreateProjectDetailViewModel()
    {
        return new ProjectDetailViewModel(
            _projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _workItemService);

    private static Mock<IGitPlugin> CreatePluginMockWithRepositories(params AvailableRepository[] repositories)
    {
        var mock = new Mock<IGitPlugin>();
        mock.Setup(p => p.PluginName).Returns("TestPlugin");
        mock.Setup(p => p.PluginPrefix).Returns("TestPlugin");
        mock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);
        return mock;
    }

    /// <summary>LadenRepositorienSuggestionsAsync befüllt UnassignedRepositories und setzt IsLoadingRepositories korrekt.</summary>
    [Fact]
    public async Task LoadRepositoriesSuggestionAsync_ShouldLoadAndPopulateUnassignedRepositories()
    {
        // Arrange
        var repo = new AvailableRepository("test-repo", DateTime.UtcNow, "owner/test-repo", "https://github.com/owner/test-repo");
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = CreateSut();

        var loadingWhatTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoadingRepositories) && sut.IsLoadingRepositories)
                loadingWhatTrue = true;
        };

        // Act
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.UnassignedRepositories.Should().HaveCount(1);
        sut.UnassignedRepositories.Single().Url.Should().Be("https://github.com/owner/test-repo");
        loadingWhatTrue.Should().BeTrue();
        sut.IsLoadingRepositories.Should().BeFalse();
    }

    /// <summary>RepositoryDoubleclickCommand erstellt ein Projekt und ordnet das Repository zu.</summary>
    [Fact]
    public async Task RepositoryDoubleclickCommand_ShouldCreateProjectAndAssignRepository()
    {
        // Arrange
        var repoUrl = "https://github.com/owner/new-repo";
        var repo = new AvailableRepository("new-repo", DateTime.UtcNow, "owner/new-repo", repoUrl);
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = CreateSut();

        // Act
        await sut.RepositoryDoubleclickCommand.ExecuteAsync(repo);

        // Assert
        var projects = await _projectService.GetAllAsync(TestContext.Current.CancellationToken);
        projects.Should().HaveCount(1);
        projects[0].Name.Should().Be("new-repo");

        var detail = await _projectService.GetDetailAsync(projects[0].Id, TestContext.Current.CancellationToken);
        detail!.Repositories.Should().HaveCount(1);
        detail.Repositories[0].RepositoryUrl.Should().Be(repoUrl);
    }

    /// <summary>RepositoryDoubleclickCommand lädt Projects und Repositories nach der Erstellung neu.</summary>
    [Fact]
    public async Task RepositoryDoubleclickCommand_ShouldReloadProjectsAndRepositories_AfterCreation()
    {
        // Arrange
        var repo = new AvailableRepository("new-repo", DateTime.UtcNow, "owner/new-repo", "https://github.com/owner/new-repo");
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await sut.RepositoryDoubleclickCommand.ExecuteAsync(repo);

        // Assert
        sut.Projects.Should().HaveCount(1);
        sut.UnassignedRepositories.Should().BeEmpty();
    }

    /// <summary>KehreZuProjectZurueck lädt unzugeordnete Repositories neu.</summary>
    [Fact]
    public async Task ReturnToProjectBack_ShouldReloadUnassignedRepositories()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Test-Projekt", null, TestContext.Current.CancellationToken);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        var repo = new AvailableRepository("reload-repo", DateTime.UtcNow, "owner/reload-repo", "https://github.com/owner/reload-repo");
        var tcs = new TaskCompletionSource<IEnumerable<AvailableRepository>>();
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginName).Returns("TestPlugin");
        pluginMock.Setup(p => p.PluginPrefix).Returns("TestPlugin");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();
        sut.SelectedProject = project;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;
        projectDetailViewModel.OpenWorkItemCommand.Execute(workItem.Id);
        tcs.SetResult([repo]);

        // Act
        var taskViewModel = (TaskDetailViewModel)sut.DetailViewModel!;
        taskViewModel.BackCommand.Execute(null);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert
        sut.UnassignedRepositories.Should().HaveCount(1);
        sut.UnassignedRepositories.Single().Url.Should().Be("https://github.com/owner/reload-repo");
    }

    /// <summary>ZeigeTaskDetailView setzt DetailViewModel auf das übergebene TaskDetailViewModel.</summary>
    [Fact]
    public async Task ProjectListViewModel_ShowTaskDetailView_SetsDetailViewModelToTask()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Task-Navigations-Projekt", null, TestContext.Current.CancellationToken);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var sut = CreateSut();
        sut.SelectedProject = project;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;

        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        // Act
        projectDetailViewModel.OpenWorkItemCommand.Execute(workItem.Id);

        // Assert
        sut.DetailViewModel.Should().BeOfType<TaskDetailViewModel>();
    }

    /// <summary>KehreZuProjectZurueck setzt DetailViewModel zurück zum ProjectDetailViewModel.</summary>
    [Fact]
    public async Task ProjectListViewModel_ReturnToProjectBack_RestoresProjectDetailView()
    {
        // Arrange
        var project = await _projectService.CreateAsync("Zurueck-Navigations-Projekt", null, TestContext.Current.CancellationToken);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var sut = CreateSut();
        sut.SelectedProject = project;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;

        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
        projectDetailViewModel.OpenWorkItemCommand.Execute(workItem.Id);
        var taskDetailViewModel = (TaskDetailViewModel)sut.DetailViewModel!;

        // Act
        taskDetailViewModel.BackCommand.Execute(null);

        // Assert
        sut.DetailViewModel.Should().BeSameAs(projectDetailViewModel);
    }
}
