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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProjektService _projektService;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;

    public ProjectDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
    }

    public void Dispose() => _db.Dispose();

    private ProjectDetailViewModel CreateSut(
        Action? zurueckAction = null,
        Func<Task>? projektHinzugefuegtCallback = null,
        Action<TaskDetailViewModel>? navigateToTaskViewCallback = null,
        Action? navigateBackToProjectCallback = null)
    {
        var vm = new ProjectDetailViewModel(
            _projektService,
            _aufgabeService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
        vm.ZurueckAction = zurueckAction;
        vm.ProjektListeAktualisierenCallback = projektHinzugefuegtCallback;
        vm.NavigateToTaskViewCallback = navigateToTaskViewCallback;
        vm.NavigateBackToProjectCallback = navigateBackToProjectCallback;
        return vm;
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _aufgabeService);

    /// <summary>ProjektSpeichernAsync ruft CreateAsync auf und navigiert zurück, wenn die ID leer ist.</summary>
    [Fact]
    public async Task ProjektSpeichernAsync_ErstelltNeuesProjekt_WennIdLeer()
    {
        // Arrange
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.ProjektName = "Neues Projekt";
        sut.ProjektBeschreibung = "Eine Beschreibung";

        // Act
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        // Assert: ZurueckAction wird nach Erstellung aufgerufen
        zurueckAufgerufen.Should().BeTrue();
        // Projekt ist in der DB vorhanden
        var projekte = await _projektService.GetAllAsync();
        projekte.Should().Contain(p => p.Name == "Neues Projekt");
    }

    /// <summary>ProjektSpeichernAsync ruft UpdateAsync auf, wenn eine ID gesetzt ist.</summary>
    [Fact]
    public async Task ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Alter Name", null);
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ProjektName = "Neuer Name";
        sut.ProjektBeschreibung = "Neue Beschreibung";

        // Act
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        // Assert
        var aktualisiert = await _projektService.GetByIdAsync(projekt.Id);
        aktualisiert!.Name.Should().Be("Neuer Name");
        aktualisiert.Beschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>ProjektLoeschenAsync ruft DeleteAsync und ZurueckAction auf.</summary>
    [Fact]
    public async Task ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Zu löschendes Projekt", null);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.ProjektId = projekt.Id;
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        // Assert
        zurueckAufgerufen.Should().BeTrue();
        var deleted = await _projektService.GetByIdAsync(projekt.Id);
        deleted.Should().BeNull();
    }

    /// <summary>ProjektLoeschenAsync ruft weder DeleteAsync noch ZurueckAction auf, wenn Benutzer abbricht.</summary>
    [Fact]
    public async Task ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Bestehendes Projekt", null);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.ProjektId = projekt.Id;
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        // Assert
        zurueckAufgerufen.Should().BeFalse();
        var nochVorhanden = await _projektService.GetByIdAsync(projekt.Id);
        nochVorhanden.Should().NotBeNull();
    }

    /// <summary>SpeichernCommand ist nicht ausführbar, wenn der Projektname leer ist.</summary>
    [Fact]
    public void ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WennNameLeer()
    {
        // Arrange
        var sut = CreateSut();
        sut.ProjektName = string.Empty;

        // Act & Assert
        sut.SpeichernCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>SpeichernCommand ist nicht ausführbar, wenn der Projektname nur Leerzeichen enthält.</summary>
    [Fact]
    public void ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WennNameNurLeerzeichen()
    {
        // Arrange
        var sut = CreateSut();
        sut.ProjektName = "   ";

        // Act & Assert
        sut.SpeichernCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>ProjektSpeichernAsync ruft ProjektHinzugefuegtCallback auf, nachdem ein neues Projekt erstellt wurde.</summary>
    [Fact]
    public async Task ProjektSpeichernAsync_Success_RuftProjektHinzugefuegtCallbackAuf()
    {
        // Arrange
        var callbackAufgerufen = false;
        var sut = CreateSut(projektHinzugefuegtCallback: () => { callbackAufgerufen = true; return Task.CompletedTask; });
        sut.ProjektName = "Callback-Test-Projekt";

        // Act
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        // Assert
        callbackAufgerufen.Should().BeTrue();
    }

    /// <summary>RepositoryZuweisenAsync ruft AddRepositoryAsync auf und aktualisiert SelectedRepository, wenn der Dialog bestätigt wird.</summary>
    [Fact]
    public async Task RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAufUndAktualisiertViewModel()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Repository-Test-Projekt", null);

        var testRepo = new AvailableRepository("test-repo", DateTime.UtcNow, "test/repo", "https://github.com/test/repo");

        var pluginMock = new Mock<IGitPlugin>();
        pluginMock.Setup(p => p.PluginType).Returns(PluginType.SourceCodeManagement);

        var repositoryAssignVm = new RepositoryAssignViewModel(NullLogger<RepositoryAssignViewModel>.Instance);
        repositoryAssignVm.SelectedRepository = testRepo;
        repositoryAssignVm.SelectedScmPlugin = pluginMock.Object;

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(RepositoryAssignViewModel)))
            .Returns(repositoryAssignVm);

        _dialogServiceMock
            .Setup(d => d.RepositoryZuweisenDialog(It.IsAny<RepositoryAssignViewModel>()))
            .Returns(true);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.RepositoryZuweisenCommand).ExecuteAsync();

        // Assert: Service hat AddRepositoryAsync aufgerufen
        var detail = await _projektService.GetDetailAsync(projekt.Id);
        detail!.Repositories.Should().HaveCount(1);
        detail.Repositories[0].RepositoryUrl.Should().Be("https://github.com/test/repo");

        // Assert: ViewModel hat SelectedRepository aktualisiert
        sut.SelectedRepository.Should().NotBeNull();
    }

    /// <summary>RepositoryOeffnenCommand hat CanExecute false, wenn kein Repository geladen ist.</summary>
    [Fact]
    public void RepositoryOeffnenCommand_CanExecuteFalse_OhneRepository()
    {
        // Arrange
        var sut = CreateSut();

        // Assert
        sut.RepositoryOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>RepositoryOeffnenCommand hat CanExecute true, nachdem ein Projekt mit Repository geladen wurde.</summary>
    [Fact]
    public async Task RepositoryOeffnenCommand_CanExecuteTrue_WennRepositoryGeladen()
    {
        // Arrange
        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            PluginTyp = "GitHub",
            RepositoryUrl = "https://github.com/test/repo",
            RepositoryName = "test-repo"
        };

        var projekt = await _projektService.CreateAsync("URL-Test-Projekt", null);
        await _projektService.AddRepositoryAsync(
            projekt.Id,
            repo.PluginTyp,
            repo.RepositoryUrl,
            repo.RepositoryName);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Nach dem Laden ist CanExecute true
        sut.RepositoryOeffnenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>AufgabeOeffnenCommand ruft NavigateToTaskViewCallback auf, wenn eine Aufgabe ausgewählt wird.</summary>
    [Fact]
    public async Task AufgabeOeffnen_CallsNavigateToTaskViewCallback_WhenAufgabeSelected()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Navigations-Test-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Testaufgabe", "Beschreibung");

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.AufgabeOeffnenCommand.Execute(aufgabe.Id);

        // Assert
        navigiertesViewModel.Should().NotBeNull();
    }

    /// <summary>Beim Öffnen einer Aufgabe werden ZurueckAction und AufgabeListeAktualisierenCallback auf dem neuen TaskDetailViewModel gesetzt.</summary>
    [Fact]
    public async Task NavigateToTaskViewCallback_SetsZurueckActionAndCallbackOnTaskVM()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Navigations-Test-Projekt-2", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Testaufgabe", "Beschreibung");

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.AufgabeOeffnenCommand.Execute(aufgabe.Id);

        // Assert
        navigiertesViewModel.Should().NotBeNull();
        navigiertesViewModel!.ZurueckAction.Should().NotBeNull();
        navigiertesViewModel.AufgabeListeAktualisierenCallback.Should().NotBeNull();
    }

    /// <summary>AufgabeErstellenCommand erstellt eine Aufgabe mit Status Neu und navigiert zur TaskDetailView.</summary>
    [Fact]
    public async Task AufgabeErstellen_CreatesTaskWithStatusNeuAndNavigates()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Erstellungs-Test-Projekt", null);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.AufgabeErstellenCommand).ExecuteAsync();

        // Assert
        navigiertesViewModel.Should().NotBeNull();
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().ContainSingle(a => a.Status == AufgabeStatus.Neu);
    }

    /// <summary>ReloadAufgabenListAsync (über AufgabeListeAktualisierenCallback) aktualisiert nur das geänderte Element in der Aufgabenliste.</summary>
    [Fact]
    public async Task ReloadAufgabenList_UpdatesSingleItemAsync_WhenCallbackInvoked()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Reload-Test-Projekt", null);
        var aufgabe1 = await _aufgabeService.CreateAsync(projekt.Id, "Aufgabe 1", "Beschreibung 1");
        var aufgabe2 = await _aufgabeService.CreateAsync(projekt.Id, "Aufgabe 2", "Beschreibung 2");

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.AufgabeOeffnenCommand.Execute(aufgabe1.Id);

        // Aufgabe 1 wird extern geändert (z. B. durch Speichern in TaskDetailView)
        await _aufgabeService.UpdateAsync(aufgabe1.Id, "Aufgabe 1 - Geändert", "Beschreibung 1");

        // Act: Callback ausführen wie es TaskDetailViewModel nach dem Speichern tut
        await navigiertesViewModel!.AufgabeListeAktualisierenCallback!.Invoke();

        // Assert: nur Aufgabe 1 wurde aktualisiert, Aufgabe 2 unverändert, keine Duplikate
        sut.Aufgaben.Should().HaveCount(2);
        sut.Aufgaben.Should().ContainSingle(a => a.Id == aufgabe1.Id && a.Titel == "Aufgabe 1 - Geändert");
        sut.Aufgaben.Should().ContainSingle(a => a.Id == aufgabe2.Id && a.Titel == "Aufgabe 2");
    }
}
