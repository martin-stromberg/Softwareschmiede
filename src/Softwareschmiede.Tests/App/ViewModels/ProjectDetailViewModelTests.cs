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
    private readonly ProjectService _projektService;
    private readonly WorkItemService _workItemService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests.</summary>
    public ProjectDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dialogServiceMock = new Mock<IDialogService>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    private ProjectDetailViewModel CreateSut(
        Action? zurueckAction = null,
        Func<Task>? projektHinzugefuegtCallback = null,
        Action<TaskDetailViewModel>? navigateToTaskViewCallback = null,
        Action? navigateBackToProjectCallback = null)
    {
        var vm = new ProjectDetailViewModel(
            _projektService,
            _workItemService,
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _pluginManagerMock.Object,
            NullLogger<ProjectDetailViewModel>.Instance);
        vm.ZurueckAction = zurueckAction;
        vm.ProjektListeAktualisierenCallback = projektHinzugefuegtCallback;
        vm.NavigateToTaskViewCallback = navigateToTaskViewCallback;
        vm.NavigateBackToProjectCallback = navigateBackToProjectCallback;
        return vm;
    }

    private TaskDetailViewModel CreateTaskDetailViewModel() =>
        TaskDetailViewModelTestFactory.Create(_db, _workItemService);

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
        var projects = await _projektService.GetAllAsync(TestContext.Current.CancellationToken);
        projects.Should().Contain(p => p.Name == "Neues Projekt");
    }

    /// <summary>ProjektSpeichernAsync ruft UpdateAsync auf, wenn eine ID gesetzt ist.</summary>
    [Fact]
    public async Task ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Alter Name", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ProjektName = "Neuer Name";
        sut.ProjektBeschreibung = "Neue Beschreibung";

        // Act
        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        // Assert
        var aktualisiert = await _projektService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
        aktualisiert!.Name.Should().Be("Neuer Name");
        aktualisiert.Description.Should().Be("Neue Beschreibung");
    }

    /// <summary>ProjektLoeschenAsync ruft DeleteAsync und ZurueckAction auf.</summary>
    [Fact]
    public async Task ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Zu löschendes Projekt", null, TestContext.Current.CancellationToken);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.ProjectId = project.Id;
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        // Assert
        zurueckAufgerufen.Should().BeTrue();
        var deleted = await _projektService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    /// <summary>ProjektLoeschenAsync ruft weder DeleteAsync noch ZurueckAction auf, wenn Benutzer abbricht.</summary>
    [Fact]
    public async Task ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Bestehendes Projekt", null, TestContext.Current.CancellationToken);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.ProjectId = project.Id;
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        // Assert
        zurueckAufgerufen.Should().BeFalse();
        var nochVorhanden = await _projektService.GetByIdAsync(project.Id, TestContext.Current.CancellationToken);
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

    /// <summary>Der ProjektId-Setter löst LadenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task ProjektId_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var project = await _projektService.CreateAsync("FireAndForget-Projekt", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act: ProjektId-Setter löst SafeFireAndForget(LadenAsync) aus, ohne explizites Awaiten von LadenCommand
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
    public async Task RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAufUndAktualisiertViewModel()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Repository-Test-Projekt", null, TestContext.Current.CancellationToken);

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
            .Setup(d => d.RepositoryZuweisenDialog(It.IsAny<RepositoryAssignViewModel>()))
            .Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.RepositoryZuweisenCommand).ExecuteAsync();

        // Assert: Service hat AddRepositoryAsync aufgerufen
        var detail = await _projektService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        detail!.Repositories.Should().HaveCount(1);
        detail.Repositories[0].RepositoryUrl.Should().Be("https://github.com/test/repo");

        // Assert: ViewModel hat SelectedRepository aktualisiert
        sut.SelectedRepository.Should().NotBeNull();
    }

    /// <summary>RepositoryZuweisenAsync persistiert das im Dialog ausgewählte Arbeitsverzeichnis.</summary>
    [Fact]
    public async Task RepositoryZuweisenAsync_Success_PersistiertAusgewaehltesArbeitsverzeichnis()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Repository-Arbeitsverzeichnis-Projekt", null, TestContext.Current.CancellationToken);

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
            .Setup(d => d.RepositoryZuweisenDialog(It.IsAny<RepositoryAssignViewModel>()))
            .Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.RepositoryZuweisenCommand).ExecuteAsync();

        // Assert
        var detail = await _projektService.GetDetailAsync(project.Id, TestContext.Current.CancellationToken);
        var repository = detail!.Repositories.Single();
        var startConfiguration = await _projektService.GetRepositoryStartConfigurationAsync(repository.Id, TestContext.Current.CancellationToken);

        startConfiguration.Should().NotBeNull();
        startConfiguration!.WorkingDirectoryRelativePath.Should().Be("backend");
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
            PluginType = "GitHub",
            RepositoryUrl = "https://github.com/test/repo",
            RepositoryName = "test-repo"
        };

        var project = await _projektService.CreateAsync("URL-Test-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, repo.PluginType, repo.RepositoryUrl, repo.RepositoryName, ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Nach dem Laden ist CanExecute true
        sut.RepositoryOeffnenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>AufgabeOeffnenCommand ruft NavigateToTaskViewCallback auf, wenn eine Aufgabe ausgewählt wird.</summary>
    [Fact]
    public async Task AufgabeOeffnen_CallsNavigateToTaskViewCallback_WhenAufgabeSelected()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Navigations-Test-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.AufgabeOeffnenCommand.Execute(workItem.Id);

        // Assert
        navigiertesViewModel.Should().NotBeNull();
    }

    /// <summary>Beim Öffnen einer Aufgabe werden ZurueckAction und AufgabeListeAktualisierenCallback auf dem neuen TaskDetailViewModel gesetzt.</summary>
    [Fact]
    public async Task NavigateToTaskViewCallback_SetsZurueckActionAndCallbackOnTaskVM()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Navigations-Test-Projekt-2", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        sut.AufgabeOeffnenCommand.Execute(workItem.Id);

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
        var project = await _projektService.CreateAsync("Erstellungs-Test-Projekt", null, TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.AufgabeErstellenCommand).ExecuteAsync();

        // Assert
        navigiertesViewModel.Should().NotBeNull();
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().ContainSingle(a => a.Status == WorkItemStatus.New);
    }

    /// <summary>AufgabeErstellenCommand uebernimmt das im Projekt ausgewaehlte Repository.</summary>
    [Fact]
    public async Task AufgabeErstellen_AssignsSelectedRepositoryToNewTask()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Erstellungs-Test-Projekt mit Repository", null, TestContext.Current.CancellationToken);
        var repository = await _projektService.AddRepositoryAsync(project.Id, "Softwareschmiede.GitHub", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.AufgabeErstellenCommand).ExecuteAsync();

        // Assert
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().ContainSingle(a => a.GitRepositoryId == repository.Id);
    }

    /// <summary>ReloadAufgabenListAsync (über AufgabeListeAktualisierenCallback) aktualisiert nur das geänderte Element in der Aufgabenliste.</summary>
    [Fact]
    public async Task ReloadAufgabenList_UpdatesSingleItemAsync_WhenCallbackInvoked()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Reload-Test-Projekt", null, TestContext.Current.CancellationToken);
        var aufgabe1 = await _workItemService.CreateAsync(project.Id, "Aufgabe 1", "Beschreibung 1", ct: TestContext.Current.CancellationToken);
        var aufgabe2 = await _workItemService.CreateAsync(project.Id, "Aufgabe 2", "Beschreibung 2", ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.AufgabeOeffnenCommand.Execute(aufgabe1.Id);

        // Aufgabe 1 wird extern geändert (z. B. durch Speichern in TaskDetailView)
        await _workItemService.UpdateAsync(aufgabe1.Id, "Aufgabe 1 - Geändert", "Beschreibung 1", ct: TestContext.Current.CancellationToken);

        // Act: Callback ausführen wie es TaskDetailViewModel nach dem Speichern tut
        await navigiertesViewModel!.AufgabeListeAktualisierenCallback!.Invoke();

        // Assert: nur Aufgabe 1 wurde aktualisiert, Aufgabe 2 unverändert, keine Duplikate
        sut.WorkItems.Should().HaveCount(2);
        sut.WorkItems.Should().ContainSingle(a => a.Id == aufgabe1.Id && a.Title == "Aufgabe 1 - Geändert");
        sut.WorkItems.Should().ContainSingle(a => a.Id == aufgabe2.Id && a.Title == "Aufgabe 2");
    }

    /// <summary>LadenAsync trennt nicht beendete und beendete Aufgaben nach Status.</summary>
    [Fact]
    public async Task LadenAsync_TrenntNichtBeendeteUndBeendeteAufgaben()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Status-Trennung-Projekt", null, TestContext.Current.CancellationToken);
        var neu = await _workItemService.CreateAsync(project.Id, "Neu", null, ct: TestContext.Current.CancellationToken);
        var gestartet = await _workItemService.CreateAsync(project.Id, "Gestartet", null, ct: TestContext.Current.CancellationToken);
        var waiting = await _workItemService.CreateAsync(project.Id, "Wartend", null, ct: TestContext.Current.CancellationToken);
        var beendet = await _workItemService.CreateAsync(project.Id, "Beendet", null, ct: TestContext.Current.CancellationToken);
        var archiviert = await _workItemService.CreateAsync(project.Id, "Archiviert", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SetStatusUncheckedAsync(gestartet.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(waiting.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(beendet.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(archiviert.Id, WorkItemStatus.Archived, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.NichtBeendeteAufgaben.Select(a => a.Id).Should().BeEquivalentTo([neu.Id, gestartet.Id, waiting.Id]);
        sut.BeendeteAufgaben.Select(a => a.Id).Should().BeEquivalentTo([beendet.Id]);
        sut.NichtBeendeteAufgaben.Should().NotContain(a => a.Id == archiviert.Id);
        sut.BeendeteAufgaben.Should().NotContain(a => a.Id == archiviert.Id);
    }

    /// <summary>LadenAsync befüllt bei einem Projekt ohne Aufgaben leere getrennte Listen.</summary>
    [Fact]
    public async Task LadenAsync_LeeresProjekt_HatLeereGetrennteAufgabenlisten()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Leeres-Projekt", null, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.NichtBeendeteAufgaben.Should().BeEmpty();
        sut.BeendeteAufgaben.Should().BeEmpty();
    }

    /// <summary>LadenAsync befüllt bei nur beendeten Aufgaben ausschließlich die beendete Liste.</summary>
    [Fact]
    public async Task LadenAsync_NurBeendeteAufgaben_FuelltNurBeendeteListe()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Nur-Beendete-Projekt", null, TestContext.Current.CancellationToken);
        var beendet1 = await _workItemService.CreateAsync(project.Id, "Beendet 1", null, ct: TestContext.Current.CancellationToken);
        var beendet2 = await _workItemService.CreateAsync(project.Id, "Beendet 2", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SetStatusUncheckedAsync(beendet1.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);
        await _workItemService.SetStatusUncheckedAsync(beendet2.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.NichtBeendeteAufgaben.Should().BeEmpty();
        sut.BeendeteAufgaben.Select(a => a.Id).Should().BeEquivalentTo([beendet1.Id, beendet2.Id]);
    }

    /// <summary>ReloadAufgabenListAsync aktualisiert die getrennten Listen nach einem Statuswechsel.</summary>
    [Fact]
    public async Task ReloadAufgabenList_AktualisiertGetrennteListenBeiStatuswechsel()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Reload-Status-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Statuswechsel", null, ct: TestContext.Current.CancellationToken);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.AufgabeOeffnenCommand.Execute(workItem.Id);
        await _workItemService.SetStatusUncheckedAsync(workItem.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        // Act
        await navigiertesViewModel!.AufgabeListeAktualisierenCallback!.Invoke();

        // Assert
        sut.WorkItems.Should().ContainSingle(a => a.Id == workItem.Id);
        sut.NichtBeendeteAufgaben.Should().NotContain(a => a.Id == workItem.Id);
        sut.BeendeteAufgaben.Should().ContainSingle(a => a.Id == workItem.Id);
    }

    // --- Issue-Laden ---

    private static Issue ErstelleIssue(int nummer = 1, string title = "Issue-Titel")
        => new(nummer, title, "Body", [], null, "https://github.com/test/repo/issues/" + nummer);

    private static ScmAlert ErstelleAlert(int nummer = 7, string title = "CodeQL Alert")
        => new(
            nummer,
            $"github:code-scanning:test/repo:{nummer}",
            ScmAlertType.CodeScanning,
            title,
            "Alert-Beschreibung",
            $"https://github.com/test/repo/security/code-scanning/{nummer}",
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
    public async Task LadenIssuesAsync_LoadsIssuesWhenRepositorySupportsIssues()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Issue-Test-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        SetupGitPlugin([ErstelleIssue(1), ErstelleIssue(2)]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.IssueVorschlaege.Should().HaveCount(2);
        sut.IsLoadingIssues.Should().BeFalse();
    }

    /// <summary>LadenIssuesAsync gibt leere Liste zurück wenn kein SCM-Plugin vorhanden ist.</summary>
    [Fact]
    public async Task LadenIssuesAsync_ReturnsEmptyListWhenPluginDoesNotSupport()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Kein-Plugin-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.IssueVorschlaege.Should().BeEmpty();
    }

    /// <summary>LadenIssuesAsync handhabt Exceptions gracefully: IssueVorschlaege bleibt leer, IsLoadingIssues = false.</summary>
    [Fact]
    public async Task LadenIssuesAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Exception-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Netzwerkfehler"));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.IssueVorschlaege.Should().BeEmpty();
        sut.IsLoadingIssues.Should().BeFalse();
    }

    /// <summary>LadenIssuesAsync filtert Issues heraus, deren Nummer bereits in Aufgaben als IssueReferenz vorhanden ist.</summary>
    [Fact]
    public async Task LadenIssuesAsync_FiltersOutAlreadyConvertedIssues()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Filter-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);

        var existingIssue = ErstelleIssue(1);
        await _workItemService.CreateFromIssueAsync(project.Id, existingIssue, ct: TestContext.Current.CancellationToken);

        SetupGitPlugin([ErstelleIssue(1), ErstelleIssue(2)]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Issue #1 wurde gefiltert, nur Issue #2 bleibt
        sut.IssueVorschlaege.Should().HaveCount(1);
        sut.IssueVorschlaege[0].Number.Should().Be(2);
    }

    /// <summary>AufgabeAusIssueErstellenCommand erstellt Aufgabe, entfernt Issue aus Vorschlaegen und fügt sie zu Aufgaben hinzu.</summary>
    [Fact]
    public async Task AufgabeAusIssueErstellenAsync_CreatesAufgabeAndRemovesFromVorschlaege()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Konvertierung-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        var issue = ErstelleIssue(42, "Mein Issue");
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await sut.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);

        // Assert
        sut.IssueVorschlaege.Should().BeEmpty();
        sut.WorkItems.Should().Contain(a => a.Title == "Mein Issue");
        var workItem = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItem.Should().Contain(a => a.Title == "Mein Issue");
    }

    /// <summary>AufgabeAusIssueErstellenCommand tut nichts wenn Benutzer den Bestätigungsdialog abbricht.</summary>
    [Fact]
    public async Task AufgabeAusIssueErstellenAsync_UserCancellation_DoesNothing()
    {
        // Arrange
        var project = await _projektService.CreateAsync("Abbruch-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo", ct: TestContext.Current.CancellationToken);
        var issue = ErstelleIssue(99);
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var vorschlaegVorher = sut.IssueVorschlaege.Count;

        // Act
        await sut.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);

        // Assert: Keine Aufgabe erstellt, Vorschlage unverändert
        sut.IssueVorschlaege.Should().HaveCount(vorschlaegVorher);
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().BeEmpty();
    }

    /// <summary>LadenOffeneAnforderungenAsync lädt Issues und Alerts gemeinsam und filtert bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task LadenOffeneAnforderungenAsync_LoadsIssuesAndAlertsAndFiltersConvertedAlerts()
    {
        var project = await _projektService.CreateAsync("Alert-Laden-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var convertedAlert = ErstelleAlert(7);
        await _workItemService.CreateFromAlertAsync(project.Id, convertedAlert, ErstelleIssue(77, "Created issue"), ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([ErstelleIssue(1)], [convertedAlert, ErstelleAlert(8, "Neuer Alert")]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);

        var sut = CreateSut();
        sut.ProjectId = project.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IssueVorschlaege.Should().ContainSingle(i => i.Number == 1);
        sut.OffeneAnforderungen.Should().HaveCount(2);
        sut.OffeneAnforderungen.Any(a => a.Kind == ScmRequirementKind.Issue && a.Issue != null && a.Issue.Number == 1).Should().BeTrue();
        sut.OffeneAnforderungen.Any(a => a.Kind == ScmRequirementKind.Alert && a.Alert != null && a.Alert.AlertNumber == 8).Should().BeTrue();
        sut.OffeneAnforderungen.Should().NotContain(a => a.SourceKey == convertedAlert.SourceKey);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erstellt bei Alerts erst ein GitHub-Issue und danach die lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldCreateExternalIssueBeforeLocalTask_WhenRequirementIsAlert()
    {
        var project = await _projektService.CreateAsync("Alert-Konvertierung-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = ErstelleAlert(9, "Unsichere Abfrage");
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
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.OffeneAnforderungen.Should().BeEmpty();
        var workItem = (await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken)).Single();
        workItem.Title.Should().Be("Unsichere Abfrage");
        workItem.RequirementDescription.Should().Be(issueBody);
        workItem.IssueReference!.IssueNumber.Should().Be(200);
        workItem.AlertReference!.SourceKey.Should().Be(alert.SourceKey);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erzeugt kein zweites externes Issue für bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldNotCreateExternalIssue_WhenAlertSourceKeyAlreadyExists()
    {
        var project = await _projektService.CreateAsync("Alert-Duplikat-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = ErstelleAlert(9, "Unsichere Abfrage");
        await _workItemService.CreateFromAlertAsync(project.Id, alert, new Issue(200, "Code scanning alert: Unsichere Abfrage", "Body", [], null, "https://github.com/test/repo/issues/200"), ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([], [alert]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(ScmRequirement.FromAlert(alert));

        plugin.CreateIssueCalls.Should().Be(0);
        sut.WorkItems.Count(a => a.AlertReference?.SourceKey == alert.SourceKey).Should().Be(1);
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Count(a => a.AlertReference?.SourceKey == alert.SourceKey).Should().Be(1);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erstellt bei fehlgeschlagener Issue-Anlage keine lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldNotCreateLocalTask_WhenExternalIssueCreationFails()
    {
        var project = await _projektService.CreateAsync("Alert-Fehler-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var alert = ErstelleAlert(9);
        var plugin = new TestScmPlugin([], [alert])
        {
            CreateIssueResult = IssueCreateResult.Failed("permission denied")
        };
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.FehlerMeldung.Should().Contain("permission denied");
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
        workItems.Should().BeEmpty();
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erzeugt bei Benutzerabbruch weder GitHub-Issue noch lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldDoNothing_WhenUserCancelsAlertConversion()
    {
        var project = await _projektService.CreateAsync("Alert-Abbruch-Projekt", null, TestContext.Current.CancellationToken);
        await _projektService.AddRepositoryAsync(project.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo", ct: TestContext.Current.CancellationToken);
        var plugin = new TestScmPlugin([], [ErstelleAlert(9)]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjectId = project.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(0);
        var workItems = await _workItemService.GetByProjektAsync(project.Id, TestContext.Current.CancellationToken);
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
