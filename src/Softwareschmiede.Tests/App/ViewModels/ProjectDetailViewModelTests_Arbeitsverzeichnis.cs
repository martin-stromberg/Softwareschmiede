using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Tests für die nachträgliche Bearbeitung des Arbeitsverzeichnisses in <see cref="ProjectDetailViewModel"/>.</summary>
public sealed class ProjectDetailViewModelTests_Arbeitsverzeichnis : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests_Arbeitsverzeichnis.</summary>
    public ProjectDetailViewModelTests_Arbeitsverzeichnis()
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

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projectService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);

    private async Task<(Softwareschmiede.Domain.Entities.Project Project, GitRepository Repository)> ErstelleProjektMitRepositoryAsync(string? workingDirectory = null)
    {
        var project = await _projectService.CreateAsync("Arbeitsverzeichnis-Test-Projekt", null);
        var repository = await _projectService.AddRepositoryAsync(
            project.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo");

        if (workingDirectory != null)
            await _projectService.SaveRepositoryWorkingDirectoryAsync(repository.Id, workingDirectory);

        return (project, repository);
    }

    /// <summary>ArbeitsverzeichnisBearbeitenCommand ist nicht ausführbar, solange kein Repository zugewiesen ist.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenCommand_CanExecuteFalse_OhneRepository()
    {
        var project = await _projectService.CreateAsync("Ohne-Repository-Projekt", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkingDirectoryEditCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>ArbeitsverzeichnisBearbeitenCommand ist ausführbar, sobald ein Repository zugewiesen ist.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenCommand_CanExecuteTrue_MitRepository()
    {
        var (project, _) = await ErstelleProjektMitRepositoryAsync();
        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkingDirectoryEditCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Bestätigt der Benutzer den Dialog, wird das neue Arbeitsverzeichnis persistiert.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Confirmed_PersistiertNeuesArbeitsverzeichnis()
    {
        var (project, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.WorkingDirectoryEditDialog(It.IsAny<WorkingDirectoryEditViewModel>()))
            .Callback<WorkingDirectoryEditViewModel>(vm => vm.SelectedWorkingDirectory = "frontend")
            .Returns(true);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(WorkingDirectoryEditViewModel)))
            .Returns(() => new WorkingDirectoryEditViewModel(NullLogger<WorkingDirectoryEditViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.WorkingDirectoryEditCommand).ExecuteAsync();

        var startConfiguration = await _projectService.GetRepositoryStartConfigurationAsync(repository.Id, TestContext.Current.CancellationToken);
        startConfiguration.Should().NotBeNull();
        startConfiguration!.WorkingDirectoryRelativePath.Should().Be("frontend");
    }

    /// <summary>Bricht der Benutzer den Dialog ab, bleibt das bisherige Arbeitsverzeichnis unverändert.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Aborted_AendertNichts()
    {
        var (project, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.WorkingDirectoryEditDialog(It.IsAny<WorkingDirectoryEditViewModel>()))
            .Callback<WorkingDirectoryEditViewModel>(vm => vm.SelectedWorkingDirectory = "frontend")
            .Returns(false);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(WorkingDirectoryEditViewModel)))
            .Returns(() => new WorkingDirectoryEditViewModel(NullLogger<WorkingDirectoryEditViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.WorkingDirectoryEditCommand).ExecuteAsync();

        var startConfiguration = await _projectService.GetRepositoryStartConfigurationAsync(repository.Id, TestContext.Current.CancellationToken);
        startConfiguration.Should().NotBeNull();
        startConfiguration!.WorkingDirectoryRelativePath.Should().Be("backend");
    }

    /// <summary>Wird das Root-Verzeichnis ('.') ausgewählt, wird das gespeicherte Arbeitsverzeichnis auf null (Repository-Root) zurückgesetzt.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Confirmed_RootAuswahl_SetztAufNullZurueck()
    {
        var (project, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.WorkingDirectoryEditDialog(It.IsAny<WorkingDirectoryEditViewModel>()))
            .Callback<WorkingDirectoryEditViewModel>(vm => vm.SelectedWorkingDirectory = ".")
            .Returns(true);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(WorkingDirectoryEditViewModel)))
            .Returns(() => new WorkingDirectoryEditViewModel(NullLogger<WorkingDirectoryEditViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.WorkingDirectoryEditCommand).ExecuteAsync();

        var startConfiguration = await _projectService.GetRepositoryStartConfigurationAsync(repository.Id, TestContext.Current.CancellationToken);
        startConfiguration!.WorkingDirectoryRelativePath.Should().BeNull();
    }
}
