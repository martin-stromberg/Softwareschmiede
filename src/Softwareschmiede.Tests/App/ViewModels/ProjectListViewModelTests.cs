using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
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

    public ProjectListViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
    }

    public void Dispose() => _db.Dispose();

    private ProjectListViewModel CreateSut()
    {
        return new ProjectListViewModel(
            _projektService,
            _serviceProviderMock.Object,
            NullLogger<ProjectListViewModel>.Instance);
    }

    private ProjectDetailViewModel CreateProjectDetailViewModel()
    {
        return new ProjectDetailViewModel(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _aufgabeService);

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
