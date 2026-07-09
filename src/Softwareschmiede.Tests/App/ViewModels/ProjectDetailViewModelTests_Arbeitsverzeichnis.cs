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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests_Arbeitsverzeichnis.</summary>
    public ProjectDetailViewModelTests_Arbeitsverzeichnis()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectDetailViewModel CreateSut() =>
        new(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);

    private async Task<(Softwareschmiede.Domain.Entities.Projekt Projekt, GitRepository Repository)> ErstelleProjektMitRepositoryAsync(string? workingDirectory = null)
    {
        var projekt = await _projektService.CreateAsync("Arbeitsverzeichnis-Test-Projekt", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test-repo");

        if (workingDirectory != null)
            await _projektService.SaveRepositoryWorkingDirectoryAsync(repository.Id, workingDirectory);

        return (projekt, repository);
    }

    /// <summary>ArbeitsverzeichnisBearbeitenCommand ist nicht ausführbar, solange kein Repository zugewiesen ist.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenCommand_CanExecuteFalse_OhneRepository()
    {
        var projekt = await _projektService.CreateAsync("Ohne-Repository-Projekt", null);
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ArbeitsverzeichnisBearbeitenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>ArbeitsverzeichnisBearbeitenCommand ist ausführbar, sobald ein Repository zugewiesen ist.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenCommand_CanExecuteTrue_MitRepository()
    {
        var (projekt, _) = await ErstelleProjektMitRepositoryAsync();
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ArbeitsverzeichnisBearbeitenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Bestätigt der Benutzer den Dialog, wird das neue Arbeitsverzeichnis persistiert.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Confirmed_PersistiertNeuesArbeitsverzeichnis()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.ArbeitsverzeichnisBearbeitenDialog(It.IsAny<ArbeitsverzeichnisBearbeitenViewModel>()))
            .Callback<ArbeitsverzeichnisBearbeitenViewModel>(vm => vm.SelectedWorkingDirectory = "frontend")
            .Returns(true);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ArbeitsverzeichnisBearbeitenViewModel)))
            .Returns(() => new ArbeitsverzeichnisBearbeitenViewModel(NullLogger<ArbeitsverzeichnisBearbeitenViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.ArbeitsverzeichnisBearbeitenCommand).ExecuteAsync();

        var startKonfiguration = await _projektService.GetRepositoryStartKonfigurationAsync(repository.Id);
        startKonfiguration.Should().NotBeNull();
        startKonfiguration!.WorkingDirectoryRelativePath.Should().Be("frontend");
    }

    /// <summary>Bricht der Benutzer den Dialog ab, bleibt das bisherige Arbeitsverzeichnis unverändert.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Aborted_AendertNichts()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.ArbeitsverzeichnisBearbeitenDialog(It.IsAny<ArbeitsverzeichnisBearbeitenViewModel>()))
            .Callback<ArbeitsverzeichnisBearbeitenViewModel>(vm => vm.SelectedWorkingDirectory = "frontend")
            .Returns(false);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ArbeitsverzeichnisBearbeitenViewModel)))
            .Returns(() => new ArbeitsverzeichnisBearbeitenViewModel(NullLogger<ArbeitsverzeichnisBearbeitenViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.ArbeitsverzeichnisBearbeitenCommand).ExecuteAsync();

        var startKonfiguration = await _projektService.GetRepositoryStartKonfigurationAsync(repository.Id);
        startKonfiguration.Should().NotBeNull();
        startKonfiguration!.WorkingDirectoryRelativePath.Should().Be("backend");
    }

    /// <summary>Wird das Root-Verzeichnis ('.') ausgewählt, wird das gespeicherte Arbeitsverzeichnis auf null (Repository-Root) zurückgesetzt.</summary>
    [Fact]
    public async Task ArbeitsverzeichnisBearbeitenAsync_Confirmed_RootAuswahl_SetztAufNullZurueck()
    {
        var (projekt, repository) = await ErstelleProjektMitRepositoryAsync(workingDirectory: "backend");

        _dialogServiceMock
            .Setup(d => d.ArbeitsverzeichnisBearbeitenDialog(It.IsAny<ArbeitsverzeichnisBearbeitenViewModel>()))
            .Callback<ArbeitsverzeichnisBearbeitenViewModel>(vm => vm.SelectedWorkingDirectory = ".")
            .Returns(true);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ArbeitsverzeichnisBearbeitenViewModel)))
            .Returns(() => new ArbeitsverzeichnisBearbeitenViewModel(NullLogger<ArbeitsverzeichnisBearbeitenViewModel>.Instance));

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.ArbeitsverzeichnisBearbeitenCommand).ExecuteAsync();

        var startKonfiguration = await _projektService.GetRepositoryStartKonfigurationAsync(repository.Id);
        startKonfiguration!.WorkingDirectoryRelativePath.Should().BeNull();
    }
}
