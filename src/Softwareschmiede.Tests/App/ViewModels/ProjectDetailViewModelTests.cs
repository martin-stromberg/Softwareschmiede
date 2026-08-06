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
    private readonly Mock<IPluginManager> _pluginManagerMock;

    /// <summary>ProjectDetailViewModelTests.</summary>
    public ProjectDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
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
            _aufgabeService,
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

    /// <summary>Der ProjektId-Setter löst LadenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task ProjektId_Setter_UsesFireAndForgetSafely()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("FireAndForget-Projekt", null);
        var sut = CreateSut();

        // Act: ProjektId-Setter löst SafeFireAndForget(LadenAsync) aus, ohne explizites Awaiten von LadenCommand
        sut.ProjektId = projekt.Id;

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Projekt == null)
            await Task.Delay(50);

        sut.Projekt.Should().NotBeNull("der ProjektId-Setter muss LadenAsync per SafeFireAndForget auslösen");
        sut.Projekt!.Name.Should().Be("FireAndForget-Projekt");
    }

    /// <summary>RepositoryZuweisenAsync ruft AddRepositoryAsync auf und aktualisiert SelectedRepository, wenn der Dialog bestätigt wird.</summary>
    [Fact]
    public async Task RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAufUndAktualisiertViewModel()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Repository-Test-Projekt", null);

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

    /// <summary>RepositoryZuweisenAsync persistiert das im Dialog ausgewählte Arbeitsverzeichnis.</summary>
    [Fact]
    public async Task RepositoryZuweisenAsync_Success_PersistiertAusgewaehltesArbeitsverzeichnis()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Repository-Arbeitsverzeichnis-Projekt", null);

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
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.RepositoryZuweisenCommand).ExecuteAsync();

        // Assert
        var detail = await _projektService.GetDetailAsync(projekt.Id);
        var repository = detail!.Repositories.Single();
        var startKonfiguration = await _projektService.GetRepositoryStartKonfigurationAsync(repository.Id);

        startKonfiguration.Should().NotBeNull();
        startKonfiguration!.WorkingDirectoryRelativePath.Should().Be("backend");
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

    /// <summary>AufgabeErstellenCommand uebernimmt das im Projekt ausgewaehlte Repository.</summary>
    [Fact]
    public async Task AufgabeErstellen_AssignsSelectedRepositoryToNewTask()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Erstellungs-Test-Projekt mit Repository", null);
        var repository = await _projektService.AddRepositoryAsync(
            projekt.Id,
            "Softwareschmiede.GitHub",
            "https://github.com/test/repo",
            "test/repo");

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        // Act
        await ((AsyncRelayCommand)sut.AufgabeErstellenCommand).ExecuteAsync();

        // Assert
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().ContainSingle(a => a.GitRepositoryId == repository.Id);
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

    /// <summary>LadenAsync trennt nicht beendete und beendete Aufgaben nach Status.</summary>
    [Fact]
    public async Task LadenAsync_TrenntNichtBeendeteUndBeendeteAufgaben()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Status-Trennung-Projekt", null);
        var neu = await _aufgabeService.CreateAsync(projekt.Id, "Neu", null);
        var gestartet = await _aufgabeService.CreateAsync(projekt.Id, "Gestartet", null);
        var wartend = await _aufgabeService.CreateAsync(projekt.Id, "Wartend", null);
        var beendet = await _aufgabeService.CreateAsync(projekt.Id, "Beendet", null);
        var archiviert = await _aufgabeService.CreateAsync(projekt.Id, "Archiviert", null);

        await _aufgabeService.StatusSetzenAsync(gestartet.Id, AufgabeStatus.Gestartet);
        await _aufgabeService.StatusSetzenAsync(wartend.Id, AufgabeStatus.Wartend);
        await _aufgabeService.StatusSetzenAsync(beendet.Id, AufgabeStatus.Beendet);
        await _aufgabeService.StatusSetzenAsync(archiviert.Id, AufgabeStatus.Archiviert);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.NichtBeendeteAufgaben.Select(a => a.Id).Should().BeEquivalentTo([neu.Id, gestartet.Id, wartend.Id]);
        sut.BeendeteAufgaben.Select(a => a.Id).Should().BeEquivalentTo([beendet.Id]);
        sut.NichtBeendeteAufgaben.Should().NotContain(a => a.Id == archiviert.Id);
        sut.BeendeteAufgaben.Should().NotContain(a => a.Id == archiviert.Id);
    }

    /// <summary>LadenAsync befüllt bei einem Projekt ohne Aufgaben leere getrennte Listen.</summary>
    [Fact]
    public async Task LadenAsync_LeeresProjekt_HatLeereGetrennteAufgabenlisten()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Leeres-Projekt", null);
        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

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
        var projekt = await _projektService.CreateAsync("Nur-Beendete-Projekt", null);
        var beendet1 = await _aufgabeService.CreateAsync(projekt.Id, "Beendet 1", null);
        var beendet2 = await _aufgabeService.CreateAsync(projekt.Id, "Beendet 2", null);

        await _aufgabeService.StatusSetzenAsync(beendet1.Id, AufgabeStatus.Beendet);
        await _aufgabeService.StatusSetzenAsync(beendet2.Id, AufgabeStatus.Beendet);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

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
        var projekt = await _projektService.CreateAsync("Reload-Status-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Statuswechsel", null);

        TaskDetailViewModel? navigiertesViewModel = null;
        var sut = CreateSut(navigateToTaskViewCallback: vm => navigiertesViewModel = vm);
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(TaskDetailViewModel)))
            .Returns(() => CreateTaskDetailViewModel());

        sut.AufgabeOeffnenCommand.Execute(aufgabe.Id);
        await _aufgabeService.StatusSetzenAsync(aufgabe.Id, AufgabeStatus.Beendet);

        // Act
        await navigiertesViewModel!.AufgabeListeAktualisierenCallback!.Invoke();

        // Assert
        sut.Aufgaben.Should().ContainSingle(a => a.Id == aufgabe.Id);
        sut.NichtBeendeteAufgaben.Should().NotContain(a => a.Id == aufgabe.Id);
        sut.BeendeteAufgaben.Should().ContainSingle(a => a.Id == aufgabe.Id);
    }

    // --- Issue-Laden ---

    private static Issue ErstelleIssue(int nummer = 1, string titel = "Issue-Titel")
        => new(nummer, titel, "Body", [], null, "https://github.com/test/repo/issues/" + nummer);

    private static ScmAlert ErstelleAlert(int nummer = 7, string titel = "CodeQL Alert")
        => new(
            nummer,
            $"github:code-scanning:test/repo:{nummer}",
            ScmAlertType.CodeScanning,
            titel,
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
        var projekt = await _projektService.CreateAsync("Issue-Test-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");
        SetupGitPlugin([ErstelleIssue(1), ErstelleIssue(2)]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

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
        var projekt = await _projektService.CreateAsync("Kein-Plugin-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

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
        var projekt = await _projektService.CreateAsync("Exception-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Netzwerkfehler"));
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

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
        var projekt = await _projektService.CreateAsync("Filter-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");

        var existingIssue = ErstelleIssue(1);
        await _aufgabeService.CreateFromIssueAsync(projekt.Id, existingIssue);

        SetupGitPlugin([ErstelleIssue(1), ErstelleIssue(2)]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

        // Act
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Issue #1 wurde gefiltert, nur Issue #2 bleibt
        sut.IssueVorschlaege.Should().HaveCount(1);
        sut.IssueVorschlaege[0].Nummer.Should().Be(2);
    }

    /// <summary>AufgabeAusIssueErstellenCommand erstellt Aufgabe, entfernt Issue aus Vorschlaegen und fügt sie zu Aufgaben hinzu.</summary>
    [Fact]
    public async Task AufgabeAusIssueErstellenAsync_CreatesAufgabeAndRemovesFromVorschlaege()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Konvertierung-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");
        var issue = ErstelleIssue(42, "Mein Issue");
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await sut.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);

        // Assert
        sut.IssueVorschlaege.Should().BeEmpty();
        sut.Aufgaben.Should().Contain(a => a.Titel == "Mein Issue");
        var aufgabe = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgabe.Should().Contain(a => a.Titel == "Mein Issue");
    }

    /// <summary>AufgabeAusIssueErstellenCommand tut nichts wenn Benutzer den Bestätigungsdialog abbricht.</summary>
    [Fact]
    public async Task AufgabeAusIssueErstellenAsync_UserCancellation_DoesNothing()
    {
        // Arrange
        var projekt = await _projektService.CreateAsync("Abbruch-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test-repo");
        var issue = ErstelleIssue(99);
        SetupGitPlugin([issue]);

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var vorschlaegVorher = sut.IssueVorschlaege.Count;

        // Act
        await sut.AufgabeAusIssueErstellenCommand.ExecuteAsync(issue);

        // Assert: Keine Aufgabe erstellt, Vorschlage unverändert
        sut.IssueVorschlaege.Should().HaveCount(vorschlaegVorher);
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().BeEmpty();
    }

    /// <summary>LadenOffeneAnforderungenAsync lädt Issues und Alerts gemeinsam und filtert bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task LadenOffeneAnforderungenAsync_LoadsIssuesAndAlertsAndFiltersConvertedAlerts()
    {
        var projekt = await _projektService.CreateAsync("Alert-Laden-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo");
        var convertedAlert = ErstelleAlert(7);
        await _aufgabeService.CreateFromAlertAsync(
            projekt.Id,
            convertedAlert,
            ErstelleIssue(77, "Created issue"));
        var plugin = new TestScmPlugin([ErstelleIssue(1)], [convertedAlert, ErstelleAlert(8, "Neuer Alert")]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IssueVorschlaege.Should().ContainSingle(i => i.Nummer == 1);
        sut.OffeneAnforderungen.Should().HaveCount(2);
        sut.OffeneAnforderungen.Any(a => a.Kind == ScmRequirementKind.Issue && a.Issue != null && a.Issue.Nummer == 1).Should().BeTrue();
        sut.OffeneAnforderungen.Any(a => a.Kind == ScmRequirementKind.Alert && a.Alert != null && a.Alert.AlertNumber == 8).Should().BeTrue();
        sut.OffeneAnforderungen.Should().NotContain(a => a.SourceKey == convertedAlert.SourceKey);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erstellt bei Alerts erst ein GitHub-Issue und danach die lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldCreateExternalIssueBeforeLocalTask_WhenRequirementIsAlert()
    {
        var projekt = await _projektService.CreateAsync("Alert-Konvertierung-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo");
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
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.OffeneAnforderungen.Should().BeEmpty();
        var aufgabe = (await _aufgabeService.GetByProjektAsync(projekt.Id)).Single();
        aufgabe.Titel.Should().Be("Unsichere Abfrage");
        aufgabe.AnforderungsBeschreibung.Should().Be(issueBody);
        aufgabe.IssueReferenz!.IssueNummer.Should().Be(200);
        aufgabe.AlertReferenz!.SourceKey.Should().Be(alert.SourceKey);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erzeugt kein zweites externes Issue für bereits konvertierte Alerts.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldNotCreateExternalIssue_WhenAlertSourceKeyAlreadyExists()
    {
        var projekt = await _projektService.CreateAsync("Alert-Duplikat-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo");
        var alert = ErstelleAlert(9, "Unsichere Abfrage");
        await _aufgabeService.CreateFromAlertAsync(
            projekt.Id,
            alert,
            new Issue(200, "Code scanning alert: Unsichere Abfrage", "Body", [], null, "https://github.com/test/repo/issues/200"));
        var plugin = new TestScmPlugin([], [alert]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(ScmRequirement.FromAlert(alert));

        plugin.CreateIssueCalls.Should().Be(0);
        sut.Aufgaben.Count(a => a.AlertReferenz?.SourceKey == alert.SourceKey).Should().Be(1);
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Count(a => a.AlertReferenz?.SourceKey == alert.SourceKey).Should().Be(1);
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erstellt bei fehlgeschlagener Issue-Anlage keine lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldNotCreateLocalTask_WhenExternalIssueCreationFails()
    {
        var projekt = await _projektService.CreateAsync("Alert-Fehler-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo");
        var alert = ErstelleAlert(9);
        var plugin = new TestScmPlugin([], [alert])
        {
            CreateIssueResult = IssueCreateResult.Failed("permission denied")
        };
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(1);
        sut.FehlerMeldung.Should().Contain("permission denied");
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().BeEmpty();
    }

    /// <summary>AufgabeAusAnforderungErstellenCommand erzeugt bei Benutzerabbruch weder GitHub-Issue noch lokale Aufgabe.</summary>
    [Fact]
    public async Task AufgabeAusAnforderungErstellenAsync_ShouldDoNothing_WhenUserCancelsAlertConversion()
    {
        var projekt = await _projektService.CreateAsync("Alert-Abbruch-Projekt", null);
        await _projektService.AddRepositoryAsync(projekt.Id, "SourceCodeManagement", "https://github.com/test/repo", "test/repo");
        var plugin = new TestScmPlugin([], [ErstelleAlert(9)]);
        _pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([plugin]);
        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.ProjektId = projekt.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await sut.AufgabeAusAnforderungErstellenCommand.ExecuteAsync(sut.OffeneAnforderungen.Single());

        plugin.CreateIssueCalls.Should().Be(0);
        var aufgaben = await _aufgabeService.GetByProjektAsync(projekt.Id);
        aufgaben.Should().BeEmpty();
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
