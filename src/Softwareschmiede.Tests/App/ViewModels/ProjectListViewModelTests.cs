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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
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
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance, _pluginManagerMock.Object);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectListViewModel CreateSut()
    {
        return new ProjectListViewModel(
            _projektService,
            _serviceProviderMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectListViewModel>.Instance);
    }

    private ProjectDetailViewModel CreateProjectDetailViewModel()
    {
        return new ProjectDetailViewModel(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _aufgabeService);

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
    public async Task LadenRepositorienSuggestionsAsync_ShouldLoadAndPopulateUnassignedRepositories()
    {
        // Arrange
        var repo = new AvailableRepository("test-repo", DateTime.UtcNow, "owner/test-repo", "https://github.com/owner/test-repo");
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = CreateSut();

        var loadingWasTrue = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsLoadingRepositories) && sut.IsLoadingRepositories)
                loadingWasTrue = true;
        };

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.UnassignedRepositories.Should().HaveCount(1);
        sut.UnassignedRepositories.Single().Url.Should().Be("https://github.com/owner/test-repo");
        loadingWasTrue.Should().BeTrue();
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
        var projekte = await _projektService.GetAllAsync();
        projekte.Should().HaveCount(1);
        projekte[0].Name.Should().Be("new-repo");

        var detail = await _projektService.GetDetailAsync(projekte[0].Id);
        detail!.Repositories.Should().HaveCount(1);
        detail.Repositories[0].RepositoryUrl.Should().Be(repoUrl);
    }

    /// <summary>RepositoryDoubleclickCommand lädt Projekte und Repositories nach der Erstellung neu.</summary>
    [Fact]
    public async Task RepositoryDoubleclickCommand_ShouldReloadProjectsAndRepositories_AfterCreation()
    {
        // Arrange
        var repo = new AvailableRepository("new-repo", DateTime.UtcNow, "owner/new-repo", "https://github.com/owner/new-repo");
        var plugin = CreatePluginMockWithRepositories(repo);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin.Object]);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await sut.RepositoryDoubleclickCommand.ExecuteAsync(repo);

        // Assert
        sut.Projekte.Should().HaveCount(1);
        sut.UnassignedRepositories.Should().BeEmpty();
    }

    /// <summary>KehreZuProjectZurueck lädt unzugeordnete Repositories neu.</summary>
    [Fact]
    public async Task KehreZuProjectZurueck_ShouldReloadUnassignedRepositories()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Test-Projekt", null);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Testaufgabe", "Beschreibung");

        var repo = new AvailableRepository("reload-repo", DateTime.UtcNow, "owner/reload-repo", "https://github.com/owner/reload-repo");
        var tcs = new TaskCompletionSource<IEnumerable<AvailableRepository>>();
        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginName).Returns("TestPlugin");
        pluginMock.Setup(p => p.PluginPrefix).Returns("TestPlugin");
        pluginMock.Setup(p => p.GetAvailableRepositoriesAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([pluginMock.Object]);
        var sut = CreateSut();
        sut.SelectedProjekt = projekt;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;
        projectDetailViewModel.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        tcs.SetResult([repo]);

        // Act
        var taskViewModel = (TaskDetailViewModel)sut.DetailViewModel!;
        taskViewModel.ZurueckCommand.Execute(null);
        await Task.Delay(200);

        // Assert
        sut.UnassignedRepositories.Should().HaveCount(1);
        sut.UnassignedRepositories.Single().Url.Should().Be("https://github.com/owner/reload-repo");
    }

    /// <summary>ZeigeTaskDetailView setzt DetailViewModel auf das übergebene TaskDetailViewModel.</summary>
    [Fact]
    public async Task ProjectListViewModel_ZeigeTaskDetailView_SetsDetailViewModelToTask()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Task-Navigations-Projekt", null);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var sut = CreateSut();
        sut.SelectedProjekt = projekt;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Testaufgabe", "Beschreibung");

        // Act
        projectDetailViewModel.AufgabeOeffnenCommand.Execute(aufgabe.Id);

        // Assert
        sut.DetailViewModel.Should().BeOfType<TaskDetailViewModel>();
    }

    /// <summary>KehreZuProjectZurueck setzt DetailViewModel zurück zum ProjectDetailViewModel.</summary>
    [Fact]
    public async Task ProjectListViewModel_KehreZuProjectZurueck_RestoresProjectDetailView()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Zurueck-Navigations-Projekt", null);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ProjectDetailViewModel)))
            .Returns(() => CreateProjectDetailViewModel());
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        var sut = CreateSut();
        sut.SelectedProjekt = projekt;
        var projectDetailViewModel = (ProjectDetailViewModel)sut.DetailViewModel!;

        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Testaufgabe", "Beschreibung");
        projectDetailViewModel.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        var taskDetailViewModel = (TaskDetailViewModel)sut.DetailViewModel!;

        // Act
        taskDetailViewModel.ZurueckCommand.Execute(null);

        // Assert
        sut.DetailViewModel.Should().BeSameAs(projectDetailViewModel);
    }
}
