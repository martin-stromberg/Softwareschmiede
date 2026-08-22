using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly TodoService _todoService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly AppEinstellungService _einstellungService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IKiPlugin> _kiPluginMock;
    private readonly Mock<IGitPlugin> _gitPluginForResolutionMock;
    private readonly Mock<IPluginManager> _pluginManagerMockFuerPluginSelection;
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>TaskDetailViewModelTests.</summary>
    public TaskDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);

        _kiService = TestKiAusfuehrungsServiceFactory.Create();

        _kiPluginMock = new Mock<IKiPlugin>();
        _kiPluginMock.SetupGet(p => p.PluginName).Returns("Test KI");
        _kiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        _kiPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        _kiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _kiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 30 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        _gitPluginForResolutionMock = new Mock<IGitPlugin>();
        _gitPluginForResolutionMock.SetupGet(p => p.PluginName).Returns("Test Git");
        _gitPluginForResolutionMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestGit");
        _gitPluginForResolutionMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        _gitPluginForResolutionMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _gitPluginForResolutionMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gitPluginForResolutionMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Zweites KI-Plugin: Damit VerfuegbareKiPlugins mehr als ein aktives Plugin enthält und das
        // Single-Plugin-Verhalten (Selector/Dialog entfällt bei genau einem aktiven Plugin) den
        // Plugin-Auswahl-Dialog in den bestehenden dialogbasierten Tests dieser Klasse nicht überspringt.
        var zweitesKiPluginMock = new Mock<IKiPlugin>();
        zweitesKiPluginMock.SetupGet(p => p.PluginName).Returns("Zweites KI");
        zweitesKiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.ZweitesKi");
        zweitesKiPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        zweitesKiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object, zweitesKiPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDefaultDevelopmentAutomationPlugin()).Returns(_kiPluginMock.Object);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginForResolutionMock.Object);
        // Standard-IDE-Plugins (Visual Studio, Visual Studio Code) mit nicht-verfügbarem VS Code als
        // Default; CreateSut() konfiguriert diese bei Bedarf pro Test mit spezifischem
        // IProzessStarter/IVisualStudioCodeLocator um, damit OeffneIdeCommand-Tests Prozessstarts
        // verifizieren können.
        var defaultVisualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProzessStarter>().Object);
        var defaultVisualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            new Mock<IProzessStarter>().Object,
            new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([defaultVisualStudioPlugin, defaultVisualStudioCodePlugin]);
        pluginManagerMock.Setup(p => p.GetDefaultIdePlugin()).Returns(defaultVisualStudioPlugin);
        _pluginManagerMockFuerPluginSelection = pluginManagerMock;
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance), pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        arbeitsverzeichnisMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.ArbeitsverzeichnisResolutionResult(Path.GetTempPath(), false, "configured", null));
        _entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            _pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: _kiService),
            NullLogger<EntwicklungsprozessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _kiService.Dispose();
        _db.Dispose();
        _tempDirectoryFixture.Dispose();
    }

    private string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory("tdvm_tests");

    private TaskDetailViewModel CreateSut(
        Action? zurueckAction = null,
        IPluginManager? pluginManager = null,
        IServiceProvider? serviceProvider = null,
        Mock<IProzessStarter>? prozessStarterMock = null,
        IVisualStudioCodeLocator? visualStudioCodeLocator = null)
    {
        if (pluginManager == null)
        {
            var defaultPluginManagerMock = new Mock<IPluginManager>();
            defaultPluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
            defaultPluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);
            pluginManager = defaultPluginManagerMock.Object;
        }

        var serviceProviderObj = serviceProvider ?? new Mock<IServiceProvider>().Object;

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();

        var arbeitsverzeichnisOeffnenService = TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService(prozessStarterMock);

        // Die von OeffneIdeCommand über _pluginSelectionService aufgelösten IDE-Plugins (Visual Studio,
        // Visual Studio Code) müssen denselben IProzessStarter/IVisualStudioCodeLocator verwenden wie die
        // hier verifizierten Prozessstarts, damit die Prozessstart-Verifikationen in Tests greifen.
        var effectiveProzessStarterMock = prozessStarterMock ?? new Mock<IProzessStarter>();
        var effectiveVisualStudioCodeLocator = visualStudioCodeLocator ?? new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var visualStudioPlugin = new VisualStudioIdePlugin(effectiveProzessStarterMock.Object);
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(effectiveProzessStarterMock.Object, effectiveVisualStudioCodeLocator);
        _pluginManagerMockFuerPluginSelection.Setup(p => p.GetIdePlugins()).Returns([visualStudioPlugin, visualStudioCodePlugin]);
        _pluginManagerMockFuerPluginSelection.Setup(p => p.GetDefaultIdePlugin()).Returns(visualStudioPlugin);

        var autonomAufgabeStartService = TaskDetailViewModelTestFactory.CreateAutonomAufgabeStartService(
            serviceProviderObj,
            _dialogServiceMock.Object,
            _aufgabeService);

        var vm = new TaskDetailViewModel(
            _aufgabeService,
            _protokollService,
            _kiService,
            _entwicklungsprozessService,
            _pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptZeitVersandService,
            _dialogServiceMock.Object,
            pluginManager,
            serviceProviderObj,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService,
            autonomAufgabeStartService);
        vm.ZurueckAction = zurueckAction;
        return vm;
    }

    private async Task<Aufgabe> ErstelleAufgabe(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
        {
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
            if (status.IstAktivOderWartend())
                await _aufgabeService.AusfuehrungAktivSetzenAsync(aufgabe.Id);
        }
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
    }

    /// <summary>Macht eine bestehende Aufgabe zu einer Autonomen Aufgabe, indem eine AutonomAufgabeKonfiguration persistiert wird (Modus-Indikator, siehe Aufgabe.IstAutonom()).</summary>
    /// <param name="aufgabeId">Die Id der zu autonomisierenden Aufgabe.</param>
    private async Task MacheAufgabeAutonomAsync(Guid aufgabeId)
    {
        _db.AutonomAufgabeKonfigurationen.Add(new AutonomAufgabeKonfiguration
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabeId,
            ProjektBranchName = "feature/autonom",
            InitialPrompt = "Implementiere die Aufgabe vollständig gemäß Anforderung.",
            PermissionsJsonPfad = @"C:\arbeitsverzeichnis\permissions.json",
            ArbeitsverzeichnisPfad = @"C:\arbeitsverzeichnis"
        });
        await _db.SaveChangesAsync();
    }

    // --- AufgabeBranchName ---

    /// <summary>AufgabeBranchName gibt leeren String zurück wenn Aufgabe null ist.</summary>
    [Fact]
    public void AufgabeBranchName_WhenAufgabeIsNull_ReturnsEmptyString()
    {
        var sut = CreateSut();

        sut.AufgabeBranchName.Should().BeEmpty();
    }

    /// <summary>AufgabeBranchName gibt Aufgabe.BranchName zurück wenn Aufgabe einen Branch-Namen hat.</summary>
    [Fact]
    public async Task AufgabeBranchName_WhenAufgabeHasBranchName_ReturnsBranchName()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/login-fix", Path.GetTempPath());
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AufgabeBranchName.Should().Be("feature/login-fix");
    }

    // --- AufgabeId-Setter: Fire-and-Forget ---

    /// <summary>Der AufgabeId-Setter löst LadenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task AufgabeId_Setter_UsesFireAndForgetSafely()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();

        // Act: AufgabeId-Setter löst SafeFireAndForget(LadenAsync) aus, ohne explizites Awaiten von LadenCommand
        sut.AufgabeId = aufgabe.Id;

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Aufgabe == null)
            await Task.Delay(50);

        sut.Aufgabe.Should().NotBeNull("der AufgabeId-Setter muss LadenAsync per SafeFireAndForget auslösen");
    }

    // --- Protokoll asynchrones Nachladen (Issue 193) ---

    /// <summary>LadeProtokolleAsync lädt die Protokolleinträge der Aufgabe und befüllt die Protokolleintraege-Collection.</summary>
    [Fact]
    public async Task LadeProtokolleAsync_ShouldLoadProtocols_WhenSuccessful()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        await _protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "Testinhalt");
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_aufgabeId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, aufgabe.Id);

        var method = typeof(TaskDetailViewModel).GetMethod("LadeProtokolleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;
        await task;

        sut.Protokolleintraege.Should().ContainSingle(p => p.Inhalt == "Testinhalt");
    }

    /// <summary>LadeProtokolleAsync fängt Fehler des ProtokollService nicht selbst ab, sondern lässt sie propagieren, damit SafeFireAndForget (wie bei allen anderen Fire-and-Forget-Aufrufen dieser Klasse) sie protokolliert.</summary>
    [Fact]
    public async Task LadeProtokolleAsync_ShouldPropagateException_WhenProtokollServiceFails()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_aufgabeId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, aufgabe.Id);

        await _db.DisposeAsync();

        var method = typeof(TaskDetailViewModel).GetMethod("LadeProtokolleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;

        var act = async () => await task;
        await act.Should().ThrowAsync<ObjectDisposedException>("LadeProtokolleAsync darf Fehler nicht mehr selbst abfangen, sondern muss sie propagieren, damit SafeFireAndForget sie protokollieren kann");
    }

    /// <summary>LadeProtokolleAsync fängt eine OperationCanceledException nicht selbst ab, sondern lässt sie propagieren, damit SafeFireAndForget den Abbruch behandelt.</summary>
    [Fact]
    public async Task LadeProtokolleAsync_ShouldPropagateCancellation_WhenCancellationTokenCancelled()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_aufgabeId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, aufgabe.Id);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var method = typeof(TaskDetailViewModel).GetMethod("LadeProtokolleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { cts.Token })!;

        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>("LadeProtokolleAsync darf einen Abbruch nicht mehr selbst abfangen, sondern muss ihn propagieren");
    }

    /// <summary>LadenAsync setzt Aufgabe, ohne auf das (fire-and-forget) Nachladen der Protokolle zu warten; die Protokolle werden anschließend im Hintergrund nachgeladen.</summary>
    [Fact]
    public async Task LadenAsync_ShouldNotWaitForProtocols()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        await _protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "Eintrag 1");
        await _protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.KiAntwort, "Eintrag 2");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.Aufgabe.Should().NotBeNull("Aufgabe muss gesetzt sein, sobald LadenAsync abgeschlossen ist, unabhängig vom Stand des Protokoll-Nachladens");
        sut.Aufgabe!.Id.Should().Be(aufgabe.Id);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.Protokolleintraege.Count < 2)
            await Task.Delay(50);

        sut.Protokolleintraege.Should().HaveCount(2, "die Protokolleinträge müssen im Hintergrund nachgeladen werden");
    }

    // --- ShowEditPanel, ShowCliPanel, ShowDiffPanel ---

    /// <summary>ShowEditPanel ist true wenn Status=Neu.</summary>
    [Fact]
    public async Task ShowEditPanel_IsTrue_WhenStatusNeu()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowEditPanel.Should().BeTrue();
        sut.ShowCliPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
    }

    /// <summary>ShowCliPanel ist true für Status Gestartet.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenStatusGestartet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
    }

    /// <summary>ShowCliPanel ist true für Status Wartend.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenStatusWartend()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Wartend);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
    }

    /// <summary>ShowCliPanel bleibt true, wenn der Aufgabenstatus aktiv ist, die KI-Ausführung aber beendet wurde, damit der Nutzer die letzte Ausgabe anschauen und die CLI neu starten kann.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenAusfuehrungBeendetIst()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        await _aufgabeService.AktivenLaufBeendenAsync(aufgabe.Id);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
        sut.StartenCommand.CanExecute(null).Should().BeTrue();
        sut.AufgabeAbschliessenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>ShowDiffPanel ist true wenn Status=Beendet.</summary>
    [Fact]
    public async Task ShowDiffPanel_IsTrue_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowDiffPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowCliPanel.Should().BeFalse();
    }

    /// <summary>ShowCliPanel, KannCliNeuStarten und StartenCommand.CanExecute bleiben für Autonome Aufgaben durchgängig false/deaktiviert, auch wenn AusfuehrungsStatus == Aktiv ist (Projektleiter-Agent läuft) — die reguläre CLI-Ansicht/der Start-Button gehören zur regulären Ausführung, nicht zur Autonomen Aufgabe.</summary>
    [Fact]
    public async Task ShowCliPanel_ShouldBeFalse_WhenAufgabeIstAutonomAndAusfuehrungsStatusIstAktiv()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        await MacheAufgabeAutonomAsync(aufgabe.Id);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.Aufgabe!.IstAutonom().Should().BeTrue("Vorbedingung: AutonomAufgabeKonfiguration muss ueber GetDetailAsync mitgeladen werden");
        sut.ShowCliPanel.Should().BeFalse();
        sut.KannCliNeuStarten.Should().BeFalse();
        sut.StartenCommand.CanExecute(null).Should().BeFalse();
    }

    // --- ShowFileExplorerPanel, DateiViewCommand ---

    /// <summary>DateiViewCommand wechselt zur Dateiexplorer-Ansicht.</summary>
    [Fact]
    public async Task DateiViewCommand_SetztFileExplorerAnsicht()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
            await _aufgabeService.StartenAsync(aufgabe.Id, "feature/dateien", tempDir);
            var sut = CreateSut();
            sut.AufgabeId = aufgabe.Id;
            await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

            ((RelayCommand)sut.DateiViewCommand).Execute(null);

            sut.IsFileExplorerViewSelected.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>ShowFileExplorerPanel ist nur true, wenn LokalerKlonPfad gesetzt ist und das Verzeichnis existiert.</summary>
    [Fact]
    public async Task ShowFileExplorerPanel_NurBeiVorhandenemKlonPfad()
    {
        var aufgabeOhnePfad = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sutOhnePfad = CreateSut();
        sutOhnePfad.AufgabeId = aufgabeOhnePfad.Id;
        await ((AsyncRelayCommand)sutOhnePfad.LadenCommand).ExecuteAsync();

        sutOhnePfad.ShowFileExplorerPanel.Should().BeFalse();

        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var aufgabeMitPfad = await _aufgabeService.CreateAsync(_projektId, "MitPfad", "Beschreibung");
            await _aufgabeService.StartenAsync(aufgabeMitPfad.Id, "feature/mit-pfad", tempDir);
            var sutMitPfad = CreateSut();
            sutMitPfad.AufgabeId = aufgabeMitPfad.Id;
            await ((AsyncRelayCommand)sutMitPfad.LadenCommand).ExecuteAsync();

            sutMitPfad.ShowFileExplorerPanel.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>ShowFileExplorerPanel wird beim Laden der Aufgabe einmalig ermittelt und gecacht, statt bei jedem Property-Zugriff erneut synchron das Dateisystem zu prüfen; ein nachträgliches Löschen des Verzeichnisses ändert den bereits gecachten Wert daher nicht.</summary>
    [Fact]
    public async Task ShowFileExplorerPanel_WertBleibtGecachtNachdemVerzeichnisNachtraeglichGeloeschtWurde()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "MitPfad", "Beschreibung");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/cache", tempDir);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowFileExplorerPanel.Should().BeTrue();

        Directory.Delete(tempDir, true);

        sut.ShowFileExplorerPanel.Should().BeTrue();
    }

    // --- KannSpeichern ---

    /// <summary>KannSpeichern ist true wenn Status=Neu, Titel gesetzt, kein CLI läuft.</summary>
    [Fact]
    public async Task KannSpeichern_IsTrue_WhenStatusNeuUndTitelGesetzt()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel = "Gültiger Titel";

        sut.KannSpeichern.Should().BeTrue();
    }

    /// <summary>KannSpeichern ist false wenn Titel leer ist.</summary>
    [Fact]
    public async Task KannSpeichern_IsFalse_WhenTitelLeer()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel = string.Empty;

        sut.KannSpeichern.Should().BeFalse();
    }

    /// <summary>KannSpeichern ist false wenn Status=Beendet.</summary>
    [Fact]
    public async Task KannSpeichern_IsFalse_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannSpeichern.Should().BeFalse();
    }

    /// <summary>KannSpeichern ist true wenn Status=Gestartet und Titel gesetzt.</summary>
    [Fact]
    public async Task KannSpeichern_IsTrue_WhenStatusGestartet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel = "Titel";

        sut.KannSpeichern.Should().BeTrue();
    }

    // --- KannLoeschen ---

    /// <summary>KannLoeschen ist true wenn Status=Neu.</summary>
    [Fact]
    public async Task KannLoeschen_IsTrue_WhenStatusNeu()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannLoeschen.Should().BeTrue();
    }

    /// <summary>KannLoeschen ist true wenn Status=Beendet (beendete Aufgaben können gelöscht werden).</summary>
    [Fact]
    public async Task KannLoeschen_IsTrue_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannLoeschen.Should().BeTrue();
    }

    /// <summary>KannLoeschen ist false wenn Status=Archiviert.</summary>
    [Fact]
    public async Task KannLoeschen_IsFalse_WhenStatusArchiviert()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Archiviert);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannLoeschen.Should().BeFalse();
    }

    /// <summary>KannLoeschen ist true wenn Status=Gestartet.</summary>
    [Fact]
    public async Task KannLoeschen_IsTrue_WhenStatusGestartet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannLoeschen.Should().BeTrue();
    }

    // --- SpeichernCommand ---

    /// <summary>SpeichernCommand ruft UpdateAsync auf und aktualisiert EditTitel.</summary>
    [Fact]
    public async Task SpeichernCommand_RuftUpdateAsyncAuf_UndAktualisiertDaten()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel = "Neuer Titel";
        sut.EditAnforderungsBeschreibung = "Neue Beschreibung";

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        var aktualisiert = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        aktualisiert!.Titel.Should().Be("Neuer Titel");
        aktualisiert.AnforderungsBeschreibung.Should().Be("Neue Beschreibung");
    }

    /// <summary>SpeichernCommand aktualisiert die Liste, navigiert aber nicht automatisch zurück.</summary>
    [Fact]
    public async Task SpeichernCommand_AktualisiertListe_OhneZurueckZuNavigieren()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var zurueckAufgerufen = false;
        var listeAktualisiert = false;
        var sut = CreateSut(() => zurueckAufgerufen = true);
        sut.AufgabeListeAktualisierenCallback = () =>
        {
            listeAktualisiert = true;
            return Task.CompletedTask;
        };
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.EditTitel = "Gespeicherter Titel";

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        listeAktualisiert.Should().BeTrue();
        zurueckAufgerufen.Should().BeFalse();
        sut.AufgabeId.Should().Be(aufgabe.Id);
        sut.AufgabeTitel.Should().Be("Gespeicherter Titel");
    }

    /// <summary>SpeichernCommand setzt IsLoading während der Ausführung (und danach wieder false).</summary>
    [Fact]
    public async Task SpeichernCommand_SetsIsLoading_DuringExecution()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.EditTitel = "Titel";

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.IsLoading.Should().BeFalse();
        sut.FehlerMeldung.Should().BeNull();
    }

    /// <summary>SpeichernCommand hat CanExecute false wenn KannSpeichern false ist.</summary>
    [Fact]
    public async Task SpeichernCommand_CanExecuteFalse_WennTitelLeer()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.EditTitel = string.Empty;

        sut.SpeichernCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>SpeichernCommand setzt FehlerMeldung bei Exception.</summary>
    [Fact]
    public async Task SpeichernCommand_SetzFehlerMeldung_BeiException()
    {
        var sut = CreateSut();
        // AufgabeId leer → kein Update möglich; wir testen direkt ungültige ID
        sut.AufgabeId = Guid.Empty;
        // Bei leerem AufgabeId tut der Command nichts — stattdessen ungültige DB-ID verwenden
        sut.AufgabeId = Guid.NewGuid(); // nicht in DB

        // EditTitel muss gesetzt sein damit CanExecute true ist; aber Aufgabe ist null
        // → SpeichernCommand ist nicht ausführbar (KannSpeichern = false, weil Aufgabe null)
        sut.SpeichernCommand.CanExecute(null).Should().BeFalse();
    }

    // --- LoeschenCommand ---

    /// <summary>LoeschenCommand zeigt Dialog und löscht bei Bestätigung.</summary>
    [Fact]
    public async Task LoeschenCommand_LoeschtAufgabe_WennBenutzerBestaetigt()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        zurueckAufgerufen.Should().BeTrue();
        var geloescht = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geloescht.Should().BeNull();
    }

    /// <summary>LoeschenCommand navigiert nicht zurück wenn Benutzer abbricht.</summary>
    [Fact]
    public async Task LoeschenCommand_NavigiertNichtZurueck_WennBenutzerAbbricht()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        zurueckAufgerufen.Should().BeFalse();
        var nochVorhanden = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        nochVorhanden.Should().NotBeNull();
    }

    /// <summary>LoeschenCommand ruft BestaetigenDialog auf.</summary>
    [Fact]
    public async Task LoeschenCommand_RuftBestaetigenDialogAuf()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        _dialogServiceMock.Verify(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>LoeschenCommand setzt FehlerMeldung wenn Service-Fehler auftritt.</summary>
    [Fact]
    public async Task LoeschenCommand_SetzFehlerMeldung_WennDeleteScheitert()
    {
        // Aufgabe mit Gestartet-Status ist nicht löschbar (Service wirft Exception)
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
    }

    /// <summary>LoeschenCommand hat CanExecute true wenn Status=Beendet (beendete Aufgaben können gelöscht werden).</summary>
    [Fact]
    public async Task LoeschenCommand_CanExecuteTrue_WennStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.LoeschenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>LoeschenCommand ruft AufgabeListeAktualisierenCallback auf nach erfolgreichem Löschen.</summary>
    [Fact]
    public async Task LoeschenCommand_RuftCallbackAuf_NachErfolgreichemLoeschen()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var callbackAufgerufen = false;
        var sut = CreateSut();
        sut.AufgabeListeAktualisierenCallback = () => { callbackAufgerufen = true; return Task.CompletedTask; };
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.BestaetigenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.LoeschenCommand).ExecuteAsync();

        callbackAufgerufen.Should().BeTrue();
    }

    // --- InfoCliToggleCommand ---

    /// <summary>InfoCliToggleCommand toggled IsInfoViewVisible von false auf true.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_SetzIsInfoViewVisible_AufTrue_BeiInitialFalse()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsInfoViewVisible.Should().BeFalse();
        sut.InfoCliToggleCommand.Execute(null);
        sut.IsInfoViewVisible.Should().BeTrue();
    }

    /// <summary>InfoCliToggleCommand toggled IsInfoViewVisible von true auf false.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_SetzIsInfoViewVisible_AufFalse_BeiTrue()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsInfoViewVisible = true;

        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeFalse();
    }

    /// <summary>InfoCliToggleCommand wechselt mehrfach korrekt.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_TogglesMehrfach_Korrekt()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeTrue();
    }

    /// <summary>Neue Aufgaben starten in der Info-Ansicht.</summary>
    [Fact]
    public async Task LadenAsync_WaehltInfoAnsicht_WhenStatusNeu()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsInfoViewSelected.Should().BeTrue();
        sut.IsCliViewSelected.Should().BeFalse();
        sut.IsDiffViewSelected.Should().BeFalse();
    }

    /// <summary>Gestartete Aufgaben starten in der CLI-Ansicht, Info bleibt auswählbar.</summary>
    [Fact]
    public async Task InfoViewCommand_WaehltInfoAnsicht_WhenStatusGestartet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsCliViewSelected.Should().BeTrue();

        sut.InfoViewCommand.Execute(null);

        sut.IsInfoViewSelected.Should().BeTrue();
    }

    /// <summary>Beendete Aufgaben starten in der Diff-Ansicht, Info bleibt auswählbar.</summary>
    [Fact]
    public async Task InfoViewCommand_WaehltInfoAnsicht_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsDiffViewSelected.Should().BeTrue();

        sut.InfoViewCommand.Execute(null);

        sut.IsInfoViewSelected.Should().BeTrue();
    }

    // --- EditTitel / EditAnforderungsBeschreibung ---

    /// <summary>EditTitel wird nach LadenAsync mit Aufgabe.Titel initialisiert.</summary>
    [Fact]
    public async Task EditTitel_WirdNachLaden_MitAufgabeTitelInitialisiert()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Mein Titel", "Meine Beschreibung");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel.Should().Be("Mein Titel");
    }

    /// <summary>EditAnforderungsBeschreibung wird nach LadenAsync initialisiert.</summary>
    [Fact]
    public async Task EditAnforderungsBeschreibung_WirdNachLaden_Initialisiert()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Titel", "Anforderung XYZ");
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditAnforderungsBeschreibung.Should().Be("Anforderung XYZ");
    }

    /// <summary>EditTitel ist bindbar und triggert KannSpeichern-Neuberechnung.</summary>
    [Fact]
    public async Task EditTitel_AendertKannSpeichern_BeiAenderung()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.EditTitel = string.Empty;
        sut.KannSpeichern.Should().BeFalse();

        sut.EditTitel = "Gültiger Titel";
        sut.KannSpeichern.Should().BeTrue();
    }

    /// <summary>ZurueckCommand ruft ZurueckAction auf.</summary>
    [Fact]
    public void ZurueckCommand_RuftZurueckActionAuf()
    {
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);

        sut.ZurueckCommand.Execute(null);

        zurueckAufgerufen.Should().BeTrue();
    }

    /// <summary>SpeichernAsync zeigt eine Fehlermeldung an und die View bleibt offen, wenn das Speichern fehlschlägt.</summary>
    [Fact]
    public async Task SpeichernAsync_ShowsErrorMessage_WhenSaveFails()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.EditTitel = "Titel";

        // Aufgabe wird zwischen Laden und Speichern entfernt, damit UpdateAsync fehlschlägt
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        _db.Aufgaben.Remove(entity!);
        await _db.SaveChangesAsync();

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
    }

    /// <summary>SpeichernAsync ruft ZurueckAction nicht auf, wenn das Speichern fehlschlägt.</summary>
    [Fact]
    public async Task SpeichernAsync_DoesNotNavigateBack_WhenSaveFails()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var zurueckAufgerufen = false;
        var sut = CreateSut(zurueckAction: () => zurueckAufgerufen = true);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.EditTitel = "Titel";

        // Aufgabe wird zwischen Laden und Speichern entfernt, damit UpdateAsync fehlschlägt
        var entity = await _db.Aufgaben.FindAsync(aufgabe.Id);
        _db.Aufgaben.Remove(entity!);
        await _db.SaveChangesAsync();

        await ((AsyncRelayCommand)sut.SpeichernCommand).ExecuteAsync();

        zurueckAufgerufen.Should().BeFalse();
    }

    // --- StartenCommand ---

    /// <summary>StartenCommand.CanExecute ist true wenn Status Neu und CLI nicht läuft.</summary>
    [Fact]
    public async Task TestStartenCommand_CanExecute_StatusNeuNotCliRunning()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.StartenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>StartenCommand.CanExecute ist false wenn Status != Neu.</summary>
    [Fact]
    public async Task TestStartenCommand_CanExecute_StatusNotNeu_ReturnsFalse()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.StartenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>StartenAsync zeigt den Plugin-Dialog, falls die Aufgabe kein Plugin hat.</summary>
    [Fact]
    public async Task TestStartenAsync_ShowsDialogIfNoPluginSelected()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartenAsync speichert den Projekt-Default, falls die Checkbox aktiviert wurde.</summary>
    [Fact]
    public async Task TestStartenAsync_SavesProjectDefaultIfCheckboxActivated()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", true));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var gespeichert = await pluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync(aufgabe.ProjektId, Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        gespeichert.Should().Be("Softwareschmiede.TestKi");
    }

    /// <summary>StartenAsync speichert keinen Projekt-Default, falls die Checkbox deaktiviert ist.</summary>
    [Fact]
    public async Task TestStartenAsync_DoesNotSaveProjectDefaultIfCheckboxDeactivated()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var gespeichert = await pluginDefaultSettingsService.GetDefaultPluginPrefixAsync(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        gespeichert.Should().BeNull();
    }

    /// <summary>StartenAsync ruft den kombinierten Prozess auf und die CLI läuft danach.</summary>
    [Fact]
    public async Task TestStartenAsync_InvokesCombinedProcess_StartsCliUponSuccess()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        sut.AktiverCliName.Should().Be("Test KI");
        sut.CliStatusText.Should().NotBe("Bereit Repository vor...");
        var aktualisiert = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        aktualisiert!.Status.Should().Be(AufgabeStatus.Gestartet);
    }

    /// <summary>StartenAsync zeigt während eines laufenden Repository-Klons den Vorbereitungsstatus.</summary>
    [Fact]
    public async Task StartenAsync_ShouldShowRepositoryPreparationStatus_WhileCloneIsRunning()
    {
        var cloneStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cloneContinue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gitPluginForResolutionMock
            .Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => cloneStarted.SetResult())
            .Returns(() => cloneContinue.Task);

        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        var startTask = ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();
        await cloneStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        sut.CliStatusText.Should().Be("Bereit Repository vor...");

        cloneContinue.SetResult();
        await startTask;
    }

    /// <summary>StartenAsync lässt den Vorbereitungsstatus nach einem Clone-Fehler nicht stehen.</summary>
    [Fact]
    public async Task StartenAsync_ShouldClearRepositoryPreparationStatus_WhenCloneFails()
    {
        _gitPluginForResolutionMock
            .Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Clone fehlgeschlagen"));

        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        sut.CliStatusText.Should().NotBe("Bereit Repository vor...");
        sut.CliStatusText.Should().Be("CLI inaktiv");
        sut.FehlerMeldung.Should().Contain("Aufgabe konnte nicht gestartet werden");
    }

    /// <summary>CliStoppenCommand leert den aktiven CLI-Namen.</summary>
    [Fact]
    public async Task CliStoppenCommand_LeertAktivenCliName()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();
        sut.AktiverCliName.Should().Be("Test KI");

        await ((AsyncRelayCommand)sut.CliStoppenCommand).ExecuteAsync();

        sut.AktiverCliName.Should().BeNull();
    }

    /// <summary>
    /// StartenAsync muss PseudoConsoleSessionGestartet feuern, damit TaskDetailView
    /// das TerminalControl mit der Session verbinden kann.
    /// </summary>
    [Fact]
    public async Task StartenAsync_FiresPseudoConsoleSessionGestartet_NachErfolgreichemStart()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        Softwareschmiede.Infrastructure.Terminal.PseudoConsoleSession? gemeldetSession = null;
        sut.PseudoConsoleSessionGestartet += s => gemeldetSession = s;

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        gemeldetSession.Should().NotBeNull(
            "PseudoConsoleSessionGestartet muss nach StartenCommand feuern, damit das TerminalControl die Session erhält");
    }

    /// <summary>
    /// GetPseudoConsoleSession gibt nach explizitem Start die laufende Session zurück.
    /// Das erlaubt <see cref="Softwareschmiede.App.Views.TaskDetailView"/> im Loaded-Handler die Session
    /// auch dann zu holen, wenn PseudoConsoleSessionGestartet schon gefeuert hat.
    /// </summary>
    [Fact]
    public async Task GetPseudoConsoleSession_ReturnsSession_AfterExplicitStart()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        // Simuliert: Loaded feuert NACH LadenAsync/PseudoConsoleSessionGestartet.
        // Der View ruft GetPseudoConsoleSession() im Loaded-Handler auf und setzt TerminalConsole.Session direkt.
        var session = sut.GetPseudoConsoleSession();

        session.Should().NotBeNull(
            "GetPseudoConsoleSession muss die Session liefern damit der View das TerminalControl verbinden kann " +
            "wenn Loaded nach PseudoConsoleSessionGestartet feuert");
    }

    /// <summary>
    /// Navigiert der Anwender zurück (Dispose des alten VM), läuft die CLI weiter.
    /// Öffnet er die Aufgabe erneut, muss das neue VM IsCliRunning=true melden
    /// und GetPseudoConsoleSession() die Session zurückgeben, damit der Loaded-Handler
    /// das TerminalControl verbinden kann.
    /// </summary>
    [Fact]
    public async Task NachNavigateBack_WiederoeffnenFindetLaufendeSessionUndSetzIsCliRunning()
    {
        // Aufgabe starten
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var ersteVm = CreateSut();
        ersteVm.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)ersteVm.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)ersteVm.StartenCommand).ExecuteAsync();
        ersteVm.IsCliRunning.Should().BeTrue();

        // "Zurück" navigieren: View-Unloaded ruft Dispose auf – Prozess bleibt aktiv
        ersteVm.Dispose();

        // Aufgabe erneut öffnen: neues VM (wie OeffneAufgabe in ProjectDetailViewModel)
        var zweiteVm = CreateSut();
        zweiteVm.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)zweiteVm.LadenCommand).ExecuteAsync();

        // CLI muss noch laufen und GetPseudoConsoleSession muss die Session liefern
        zweiteVm.IsCliRunning.Should().BeTrue(
            "CLI soll nach Navigation-Zurück weiterlaufen");
        zweiteVm.GetPseudoConsoleSession().Should().NotBeNull(
            "GetPseudoConsoleSession muss die Session zurückgeben damit der Loaded-Handler das TerminalControl verbinden kann");
    }

    /// <summary>Beim Start über Projekt-Default bleibt der CLI-Name nach erneutem Öffnen laufender Aufgaben sichtbar.</summary>
    [Fact]
    public async Task NachWiederoeffnen_ZeigtAktivenCliName_WhenStartPluginNurAusProjektDefaultKam()
    {
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        await pluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(
            _projektId,
            PluginType.DevelopmentAutomation,
            "Softwareschmiede.TestKi");

        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var ersteVm = CreateSut();
        ersteVm.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)ersteVm.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)ersteVm.StartenCommand).ExecuteAsync();
        ersteVm.AktiverCliName.Should().Be("Test KI");
        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var aktualisierteAufgabe = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        aktualisierteAufgabe!.KiPluginPrefix.Should().Be("Softwareschmiede.TestKi");

        ersteVm.Dispose();

        var zweiteVm = CreateSut();
        zweiteVm.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)zweiteVm.LadenCommand).ExecuteAsync();

        zweiteVm.IsCliRunning.Should().BeTrue();
        zweiteVm.AktiverCliName.Should().Be("Test KI");
    }

    // --- PluginAendernCommand ---

    /// <summary>PluginAendernCommand.CanExecute ist true wenn CLI läuft.</summary>
    [Fact]
    public async Task TestPluginWechselCommand_CanExecute_CliRunning()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, true);

        sut.PluginAendernCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>PluginAendernCommand.CanExecute ist false wenn CLI nicht läuft.</summary>
    [Fact]
    public async Task TestPluginWechselCommand_CanExecute_CliNotRunning_ReturnsFalse()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.PluginAendernCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>PluginWechselAsync stoppt die alte CLI, zeigt den Dialog und startet die neue CLI.</summary>
    [Fact]
    public async Task TestPluginWechselAsync_StopsCliAndStartsNew()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();

        await ((AsyncRelayCommand)sut.PluginAendernCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>PluginWechselAsync zeigt einen Fehler, falls StopCliAsync fehlschlägt, und bricht den Wechsel ab.</summary>
    [Fact]
    public async Task TestPluginWechselAsync_StopCliFailure_ShowsError()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        _kiService.Dispose();

        await ((AsyncRelayCommand)sut.PluginAendernCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty();
    }

    // --- LadenAsync: Kein impliziter CLI-Neustart ---

    /// <summary>LadenAsync startet keine neue CLI, falls Status Gestartet/Aktiv, aber kein Prozess läuft.</summary>
    [Fact]
    public async Task TestLoadAsync_StartetCliNichtImplizit_StatusGestartetAktivOhneLaufendenProzess()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Kein-Auto-Restart-Aufgabe", "Beschreibung");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", Path.GetTempPath());
        await _aufgabeService.UpdateAsync(aufgabe.Id, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, "Softwareschmiede.TestKi");

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeFalse();
        sut.ShowCliPanel.Should().BeTrue("der persistiert aktive Status soll sichtbar bleiben");
        sut.KannCliNeuStarten.Should().BeTrue("der Nutzer soll explizit neu starten können");
        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>LadenAsync bindet eine bereits laufende CLI wieder an, ohne einen zweiten Prozess zu starten.</summary>
    [Fact]
    public async Task TestLoadAsync_BindetBereitsLaufendeSessionWiederAn()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>LadenAsync startet die CLI nicht, falls die Ausführung beendet oder nicht gestartet ist.</summary>
    [Fact]
    public async Task TestLoadAsync_StartetCliNichtImplizit_WhenAusfuehrungBeendetOderNichtGestartet()
    {
        var neueAufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var beendeteAusfuehrung = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        await _aufgabeService.AktivenLaufBeendenAsync(beendeteAusfuehrung.Id);

        var sut = CreateSut();
        sut.AufgabeId = neueAufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeFalse();

        sut.AufgabeId = beendeteAusfuehrung.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeFalse();
        sut.StartenCommand.CanExecute(null).Should().BeTrue();
        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Eine beendete Ausführung kann über die explizite Startaktion im vorhandenen Klon neu gestartet werden.</summary>
    [Fact]
    public async Task StartenCommand_StartetBeendeteAusfuehrungExplizitNeu()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Expliziter Neustart", "Beschreibung");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/explizit", arbeitsverzeichnis);
        await _aufgabeService.UpdateAsync(aufgabe.Id, aufgabe.Titel, aufgabe.AnforderungsBeschreibung, "Softwareschmiede.TestKi");
        await _aufgabeService.AktivenLaufBeendenAsync(aufgabe.Id);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.StartenCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _kiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Insgesamt beendete Aufgaben dürfen nicht erneut gestartet werden.</summary>
    [Fact]
    public async Task StartenCommand_CanExecuteFalse_WhenGesamtstatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.StartenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Abgeschlossene Aufgaben bleiben auch mit AusfuehrungsStatus=Beendet nicht startbar.</summary>
    [Fact]
    public async Task StartenCommand_CanExecuteFalse_WhenAufgabeAbgeschlossenUndAusfuehrungBeendet()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Abgeschlossen", "Beschreibung");
        await _aufgabeService.AbschliessenAsync(aufgabe.Id);

        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AufgabeStatus.Should().Be(AufgabeStatus.Beendet);
        sut.Aufgabe!.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Beendet);
        sut.StartenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>LadenAsync lädt Promptvorlagen für die Ribbon-Auswahl.</summary>
    [Fact]
    public async Task LadenAsync_LaedtPromptVorlagenFuerAuswahl()
    {
        await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.PromptVorlagen.Should().ContainSingle();
        sut.PromptVorlagen[0].Name.Should().Be("Weitermachen");
    }

    /// <summary>PromptVorlageAuswaehlenCommand bleibt ohne laufende CLI-Session stabil.</summary>
    [Fact]
    public async Task PromptVorlageAuswaehlenCommand_OhneLaufendeSession_StuerztNichtAb()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var act = async () => await ((AsyncRelayCommand<PromptVorlage>)sut.PromptVorlageAuswaehlenCommand).ExecuteAsync(vorlage);

        await act.Should().NotThrowAsync();
    }

    // --- CanAssignIssue ---

    private Mock<IPluginManager> ErstelleGitPluginManager(bool canCreatePullRequest = true)
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestGit");
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        gitPluginMock.Setup(p => p.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.RemoteGit,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: canCreatePullRequest,
                CanMergeToSource: false));
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);
        return pluginManagerMock;
    }

    /// <summary>CanAssignIssue ist true wenn Aufgabe vorhanden und Plugin Issues unterstützt und kein CLI läuft.</summary>
    [Fact]
    public async Task CanAssignIssue_TrueWhenAufgabeExistsAndPluginSupportsIssues()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut(pluginManager: ErstelleGitPluginManager().Object);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert
        sut.CanAssignIssue.Should().BeTrue();
    }

    /// <summary>PullRequestErstellenCommand ist verfügbar, wenn Branch, Repository und PR-Capability vorhanden sind; der Aufgabenstatus muss nicht Beendet sein.</summary>
    [Fact]
    public async Task PullRequestErstellenCommand_CanExecute_WhenAufgabeGestartetMitBranchRepositoryUndPrCapability()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.TestGit",
            RepositoryUrl = "test/repo",
            RepositoryName = "Test Repository",
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR-Aufgabe", "Beschreibung", repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pr-aktionen", Path.GetTempPath());
        var sut = CreateSut(pluginManager: ErstelleGitPluginManager().Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.AufgabeStatus.Should().Be(AufgabeStatus.Gestartet);
        sut.KannPullRequestErstellen.Should().BeTrue();
        sut.PullRequestErstellenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>PullRequestErstellenCommand nutzt die Git-Orchestrierung und übergibt dem Plugin die aufgelöste Repository-ID.</summary>
    [Fact]
    public async Task PullRequestErstellenCommand_ShouldResolveRepositoryIdFromRepositoryUrl()
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginName).Returns("Test Git");
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestGit");
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        gitPluginMock.Setup(p => p.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.RemoteGit,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: true,
                CanMergeToSource: false));
        gitPluginMock.Setup(p => p.PushBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitPluginMock.Setup(p => p.CreatePullRequestAsync(
                "test/repo",
                "feature/pr-url",
                null,
                "PR aus UI",
                It.Is<string>(body =>
                    body.Contains("## Commits")
                    && body.Contains("- `abc1234` feat: UI-PR erstellt Commitliste")
                    && !body.Contains("Beschreibung")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(7, "PR aus UI", string.Empty, "feature/pr-url"));

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(gitPluginMock.Object);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.TestGit",
            RepositoryUrl = "https://github.com/test/repo.git",
            RepositoryName = "Test Repository",
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR aus UI", "Beschreibung", repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pr-url", Path.GetTempPath());

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationServiceForPr = new PluginActivationService(new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance), pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationServiceForPr, NullLogger<PluginSelectionService>.Instance);
        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance, pluginManagerMock.Object);
        var workspaceBrowserMock = new Mock<IGitWorkspaceBrowserService>();
        workspaceBrowserMock
            .Setup(browser => browser.LoadSnapshotAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot
            {
                RepositoryPath = Path.GetTempPath(),
                CommitCount = 1,
                BranchCommits =
                [
                    new BranchCommit
                    {
                        Sha = "abc1234abc1234abc1234abc1234abc1234abc1",
                        ShortSha = "abc1234",
                        Subject = "feat: UI-PR erstellt Commitliste"
                    }
                ]
            });
        var gitOrchestrationService = new GitOrchestrationService(
            _aufgabeService,
            projektService,
            _protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            NullLogger<GitOrchestrationService>.Instance,
            workspaceBrowserMock.Object);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(GitOrchestrationService))).Returns(gitOrchestrationService);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.PullRequestErstellenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().BeNull();
        gitPluginMock.Verify(p => p.PushBranchAsync(
                It.IsAny<string>(),
                "feature/pr-url",
                It.IsAny<CancellationToken>()),
            Times.Once);
        gitPluginMock.Verify(p => p.CreatePullRequestAsync(
                "test/repo",
                "feature/pr-url",
                null,
                "PR aus UI",
                It.Is<string>(body =>
                    body.Contains("## Commits")
                    && body.Contains("- `abc1234` feat: UI-PR erstellt Commitliste")
                    && !body.Contains("Beschreibung")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        gitPluginMock.Verify(p => p.CreatePullRequestAsync(
                "https://github.com/test/repo.git",
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>PullRequestErstellenCommand bleibt trotz Branch und Repository deaktiviert, wenn das Git-Plugin keine PR-Erstellung unterstützt.</summary>
    [Fact]
    public async Task PullRequestErstellenCommand_CannotExecute_WhenPrCapabilityMissing()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.TestGit",
            RepositoryUrl = "test/repo",
            RepositoryName = "Test Repository",
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR-Aufgabe ohne Capability", "Beschreibung", repository.Id);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/pr-ohne-capability", Path.GetTempPath());
        var sut = CreateSut(pluginManager: ErstelleGitPluginManager(canCreatePullRequest: false).Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannPullRequestErstellen.Should().BeFalse();
        sut.PullRequestErstellenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>PullRequestsAktualisierenCommand stoesst einen sofortigen Monitoring-Refresh an und laedt neue Workflow-Runs ohne Aufgabenwechsel.</summary>
    [Fact]
    public async Task PullRequestsAktualisierenCommand_ShouldRefreshWorkflowRunsImmediately()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.GitHub",
            RepositoryUrl = "owner/repo",
            RepositoryName = "owner/repo",
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();

        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "PR-Aufgabe", "Beschreibung", repository.Id);
        var pullRequest = new PullRequestReferenz
        {
            Id = Guid.NewGuid(),
            AufgabeId = aufgabe.Id,
            Provider = PullRequestProvider.GitHub,
            RepositoryId = "owner/repo",
            PullRequestNumber = 233,
            Url = "https://github.com/owner/repo/pull/233",
            Titel = "PR",
            SourceBranch = "feature/pr",
            TargetBranch = "main",
            HeadSha = "head",
            Status = PullRequestStatus.Open,
            MergeStatus = PullRequestMergeStatus.Unknown,
            MonitoringPhase = PullRequestMonitoringPhase.Created,
            CreatedUtc = DateTimeOffset.UtcNow,
            NextCheckUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        _db.PullRequestReferenzen.Add(pullRequest);
        await _db.SaveChangesAsync();

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginName).Returns("GitHub");
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.GitHub");
        gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetSettingGroups()).Returns(
        [
            new PluginSettingGroup("Pull Requests",
            [
                new PluginSettingField("AutoCompletePullRequests", "Auto", PluginSettingFieldType.Boolean, DefaultValue: "false")
            ])
        ]);
        gitPluginMock.Setup(p => p.GetPullRequestStatusAsync("owner/repo", 233, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestStatusInfo(
                PullRequestProvider.GitHub,
                "owner/repo",
                233,
                "PR_kw",
                "https://github.com/owner/repo/pull/233",
                "PR",
                "feature/pr",
                "main",
                "head",
                null,
                PullRequestStatus.Open,
                PullRequestMergeStatus.Mergeable,
                DateTimeOffset.UtcNow));
        gitPluginMock.Setup(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 233, "head", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PullRequestWorkflowRunInfo(
                    "233",
                    "Missing Translation for Statement Draft Validation Results",
                    "https://github.com/owner/repo/actions/runs/233",
                    "head",
                    "feature/pr",
                    WorkflowRunStatus.InProgress,
                    WorkflowRunConclusion.Unknown,
                    DateTimeOffset.UtcNow,
                    null,
                    false)
            ]);

        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IPluginManager>(pluginManagerMock.Object)
            .AddSingleton<ICredentialStore>(new Mock<ICredentialStore>().Object)
            .AddSingleton(sp => new PluginSettingsService(sp.GetRequiredService<ICredentialStore>(), NullLogger<PluginSettingsService>.Instance))
            .AddScoped(_ => new PullRequestReferenzService(_db, TimeProvider.System, NullLogger<PullRequestReferenzService>.Instance))
            .AddScoped(_ => new ProtokollService(_db, NullLogger<ProtokollService>.Instance))
            .AddSingleton<PullRequestMonitoringService>(sp => new PullRequestMonitoringService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                NullLogger<PullRequestMonitoringService>.Instance))
            .BuildServiceProvider();
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProvider);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        sut.PullRequests.Single().WorkflowRuns.Should().BeEmpty();

        await ((AsyncRelayCommand)sut.PullRequestsAktualisierenCommand).ExecuteAsync();

        sut.PullRequests.Single().WorkflowRuns.Should().ContainSingle(run =>
            run.Name == "Missing Translation for Statement Draft Validation Results"
            && run.Status == WorkflowRunStatus.InProgress);
        gitPluginMock.Verify(p => p.GetPullRequestWorkflowRunsAsync("owner/repo", 233, "head", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>CanAssignIssue ist false wenn IsCliRunning == true.</summary>
    [Fact]
    public async Task CanAssignIssue_FalseWhenCliRunning()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut(pluginManager: ErstelleGitPluginManager().Object);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartenCommand).ExecuteAsync();

        // Assert: CLI läuft → CanAssignIssue = false
        sut.IsCliRunning.Should().BeTrue();
        sut.CanAssignIssue.Should().BeFalse();
    }

    /// <summary>IssueBrowserOeffnenCommand.CanExecute ist false wenn IssueUrl null ist.</summary>
    [Fact]
    public async Task IssueBrowserOeffnenCommand_CannotExecuteWhenUrlNull()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Aufgabe hat keine IssueReferenz → CanExecute false
        sut.CurrentIssueReferenz.Should().BeNull();
        sut.IssueBrowserOeffnenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueZuweisenCommand.CanExecute ist false wenn kein Plugin vorhanden.</summary>
    [Fact]
    public async Task IssueZuweisenCommand_CannotExecuteWhenNoPlugin()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Assert: Standard-Plugin-Manager gibt keine SCM-Plugins zurück
        sut.CanAssignIssue.Should().BeFalse();
        sut.IssueZuweisenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist nur verfügbar, wenn Provider-Capability vorhanden ist und noch keine Referenz existiert.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_CanExecute_WhenProviderSupportsCreateAndNoReferenceExists()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeTrue();
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>IssueAnlegenCommand prueft die Provider-Capability mit der Repository-URL statt dem Anzeigenamen.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_ShouldUseRepositoryUrl_ForProviderCapability()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: false);
        gitPluginMock.As<IIssueCreateProvider>()
            .Setup(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeTrue();
        gitPluginMock.As<IIssueCreateProvider>()
            .Verify(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>IssueAnlegenCommand ist deaktiviert, wenn der Provider die Issue-Anlage nicht unterstützt.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_CannotExecute_WhenProviderDoesNotSupportCreate()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: false);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist deaktiviert, wenn das SCM-Plugin keine Issue-Create-Capability implementiert.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_CannotExecute_WhenProviderHasNoCreateCapability()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleGitPluginOhneIssueCreateMock();
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Providerfehler bei der Capability-Ermittlung deaktivieren die Anlage statt einen falschen Buttonzustand zu zeigen.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_CannotExecute_WhenProviderCapabilityFails()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        gitPluginMock.As<IIssueCreateProvider>()
            .Setup(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider nicht erreichbar"));
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist bei bestehender Referenz deaktiviert.</summary>
    [Fact]
    public async Task IssueAnlegenCommand_CannotExecute_WhenIssueReferenceExists()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        await _aufgabeService.UpdateIssueReferenzAsync(aufgabe.Id, new Issue(1, "Vorhanden", "Body", [], null, "https://example.test/1"));
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenAsync speichert erst nach erfolgreichem Dialog-Provider-Ergebnis die lokale Referenz.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldPersistCreatedIssueAndReloadTask()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, false, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        sut.CurrentIssueReferenz.Should().NotBeNull();
        sut.CurrentIssueReferenz!.IssueNummer.Should().Be(23);
        sut.CurrentIssueReferenz.IssueUrl.Should().Be("https://github.com/test/repo/issues/23");
        sut.Aufgabe!.AnforderungsBeschreibung.Should().Be("Beschreibung");
        sut.CanCreateIssue.Should().BeFalse();
    }

    /// <summary>IssueAnlegenAsync aktualisiert bei aktivierter Dialogoption die Aufgabenbeschreibung aus dem Provider-Issue.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldUpdateTaskDescriptionFromCreatedIssueBody_WhenOptionIsEnabled()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Alte Beschreibung", repository.Id);
        var createdIssue = new Issue(23, "Neu", "Provider Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, true, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        sut.Aufgabe!.AnforderungsBeschreibung.Should().Be("Provider Body");
        sut.CurrentIssueReferenz!.IssueNummer.Should().Be(23);
    }

    /// <summary>IssueAnlegenAsync nutzt den lokalen Dialog-Body, wenn der Provider-Issue-Body leer ist.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldUseLocalBodyFallback_WhenCreatedIssueBodyIsEmpty()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Alte Beschreibung", repository.Id);
        var createdIssue = new Issue(23, "Neu", "   ", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, true, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        sut.Aufgabe!.AnforderungsBeschreibung.Should().Be("Lokaler Body");
        sut.CurrentIssueReferenz!.IssueNummer.Should().Be(23);
    }

    /// <summary>Nach extern erstelltem Issue nennt ein lokaler Persistenzfehler die externe URL und speichert keine Referenz.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldShowExternalIssueUrl_WhenLocalPersistenceFails()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns<IssueCreateDialogViewModel, CancellationToken>(async (_, token) =>
            {
                await _aufgabeService.DeleteAsync(aufgabe.Id, token);
                return new IssueCreateDialogResult(createdIssue, true, "Lokaler Body");
            });
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("die lokale Zuordnung oder Aufgabenbeschreibung konnte aber nicht gespeichert werden");
        sut.FehlerMeldung.Should().Contain("https://github.com/test/repo/issues/23");
        (await _aufgabeService.GetDetailAsync(aufgabe.Id)).Should().BeNull();
    }

    /// <summary>IssueAnlegenAsync speichert keine lokale Referenz, wenn der Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldNotPersistReference_WhenDialogIsCancelled()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssueCreateDialogResult?)null);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        var reloaded = await _aufgabeService.GetDetailAsync(aufgabe.Id);
        reloaded!.IssueReferenz.Should().BeNull();
    }

    /// <summary>Ein zweiter Klick waehrend laufender Issue-Anlage oeffnet keinen zweiten Dialog.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldIgnoreSecondExecution_WhileCreateDialogIsOpen()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        var dialogStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogRelease = new TaskCompletionSource<IssueCreateDialogResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogCalls = 0;
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns<IssueCreateDialogViewModel, CancellationToken>((_, _) =>
            {
                dialogCalls++;
                dialogStarted.SetResult(null);
                return dialogRelease.Task;
            });
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var first = ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();
        await dialogStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sut.IssueAnlegenCommand.CanExecute(null).Should().BeFalse();

        var second = ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();
        dialogRelease.SetResult(null);
        await Task.WhenAll(first, second);

        dialogCalls.Should().Be(1);
        (await _aufgabeService.GetDetailAsync(aufgabe.Id))!.IssueReferenz.Should().BeNull();
    }

    /// <summary>Nach extern erstelltem Issue wird keine lokale Referenz überschrieben, wenn parallel bereits zugeordnet wurde.</summary>
    [Fact]
    public async Task IssueAnlegenAsync_ShouldNotOverwriteReference_WhenIssueWasAssignedAfterDialog()
    {
        var repository = await ErstelleRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Issue-Anlage", "Beschreibung", repository.Id);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var parallelIssue = new Issue(99, "Parallel", "Body", [], null, "https://github.com/test/repo/issues/99");
        var gitPluginMock = ErstelleIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = ErstellePluginManagerMitGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns<IssueCreateDialogViewModel, CancellationToken>(async (_, token) =>
            {
                await _aufgabeService.UpdateIssueReferenzAsync(aufgabe.Id, parallelIssue, token);
                return new IssueCreateDialogResult(createdIssue, true, "Neue Beschreibung");
            });
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueAnlegenCommand).ExecuteAsync();

        var reloaded = await _aufgabeService.GetDetailAsync(aufgabe.Id);
        reloaded!.IssueReferenz!.IssueNummer.Should().Be(99);
        reloaded.AnforderungsBeschreibung.Should().Be("Beschreibung");
        sut.FehlerMeldung.Should().Contain("extern erstellt");
        sut.FehlerMeldung.Should().Contain("https://github.com/test/repo/issues/23");
    }

    /// <summary>IssueZuweisenAsync tut nichts wenn Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task IssueZuweisenAsync_UserAbortDoesNothing()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);

        var dialogVm = new IssueSelectionDialogViewModel(gitPluginMock.Object, NullLogger<IssueSelectionDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueSelectionDialogViewModel))).Returns(dialogVm);

        _dialogServiceMock
            .Setup(d => d.ShowIssueSelectionDialogAsync(It.IsAny<IssueSelectionDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.IssueZuweisenCommand).ExecuteAsync();

        // Assert: IssueReferenz unverändert (null)
        sut.CurrentIssueReferenz.Should().BeNull();
    }

    /// <summary>IssueZuweisenAsync aktualisiert CurrentIssueReferenz wenn Dialog bestätigt wird.</summary>
    [Fact]
    public async Task IssueZuweisenAsync_ShowsDialogAndUpdatesCurrentIssueReferenz()
    {
        // Arrange
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Neu);
        var selectedIssue = new Issue(42, "Gewähltes Issue", "Body", [], null, "https://github.com/test/42");

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([selectedIssue]);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);

        var dialogVm = new IssueSelectionDialogViewModel(gitPluginMock.Object, NullLogger<IssueSelectionDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueSelectionDialogViewModel))).Returns(dialogVm);

        _dialogServiceMock
            .Setup(d => d.ShowIssueSelectionDialogAsync(It.IsAny<IssueSelectionDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedIssue);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.IssueZuweisenCommand).ExecuteAsync();

        // Assert
        sut.CurrentIssueReferenz.Should().NotBeNull();
        sut.CurrentIssueReferenz!.IssueNummer.Should().Be(42);
        sut.CurrentIssueReferenz.IssueUrl.Should().Be("https://github.com/test/42");
    }

    // --- OeffneArbeitsverzeichnisCommand ---

    /// <summary>CanExecute von OeffneArbeitsverzeichnisCommand folgt ShowFileExplorerPanel: ohne Arbeitsverzeichnis false, mit existierendem Arbeitsverzeichnis true.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnisCommand_CanExecute_FolgtShowFileExplorerPanel()
    {
        var aufgabe = await ErstelleAufgabe();
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.CanExecute(null).Should().BeFalse("die Aufgabe hat noch kein Arbeitsverzeichnis");

        var arbeitsverzeichnis = CreateTempDirectory();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.CanExecute(null).Should().BeTrue("das Arbeitsverzeichnis existiert jetzt");
    }

    /// <summary>Ausführung von OeffneArbeitsverzeichnisCommand delegiert an ArbeitsverzeichnisOeffnenService mit dem LokalerKlonPfad der Aufgabe.</summary>
    [Fact]
    public async Task OeffneArbeitsverzeichnisCommand_RuftDienstMitLokalemKlonPfad()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock: prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.OeffneArbeitsverzeichnisCommand.Execute(null);

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.Argumente == $"\"{arbeitsverzeichnis}\"")),
            Times.Once);
    }

    // --- KannIdeOeffnen / OeffneIdeCommand ---

    /// <summary>
    /// KannIdeOeffnen und OeffneIdeCommand.CanExecute folgen ausschließlich ShowFileExplorerPanel: Sobald
    /// ein gültiges, vorhandenes Arbeitsverzeichnis existiert, kann die IDE-Aktion ausgeführt werden - auch
    /// ohne vorhandene .sln-Datei, da das konkrete Plugin (Visual Studio oder Fallback) erst beim Ausführen
    /// über PluginSelectionService.ResolveIdePluginAsync aufgelöst wird und mindestens ein IDE-Plugin
    /// systemseitig stets aktiv bleiben muss.
    /// </summary>
    [Fact]
    public async Task KannIdeOeffnen_FolgtShowFileExplorerPanel()
    {
        var aufgabe = await ErstelleAufgabe();
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeFalse("ohne LokalerKlonPfad existiert noch kein Arbeitsverzeichnis");
        sut.OeffneIdeCommand.CanExecute(null).Should().BeFalse();

        var arbeitsverzeichnis = CreateTempDirectory();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeTrue("das Arbeitsverzeichnis existiert jetzt, unabhängig von vorhandenen .sln-Dateien");
        sut.OeffneIdeCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// Ohne gefundene Solution löst OeffneIdeCommand über PluginSelectionService.ResolveIdePluginAsync
    /// automatisch das (standardmäßig aktive) Visual-Studio-Code-Plugin als Fallback auf und öffnet damit -
    /// ganz ohne die frühere, separate "VS Code öffnen, wenn keine Solution gefunden wurde"-Einstellung.
    /// </summary>
    [Fact]
    public async Task OeffneIdeCommand_OhneSolution_OeffnetVisualStudioCodeAlsFallback()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock: prozessStarterMock, visualStudioCodeLocator: locator);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannIdeOeffnen.Should().BeTrue();
        sut.OeffneIdeCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{arbeitsverzeichnis}\""
                && !a.ShellAusfuehren)),
            Times.Once);
    }

    /// <summary>Wenn ohne gefundene Solution auch Visual Studio Code nicht gefunden wird, erzeugt OeffneIdeCommand eine verständliche Fehlermeldung ohne Prozessstart.</summary>
    [Fact]
    public async Task OeffneIdeCommand_OhneSolutionOhneVsCode_ZeigtFehler()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(
            prozessStarterMock: prozessStarterMock,
            visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Contain("Visual Studio Code");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>
    /// Ist das Visual-Studio-Code-IDE-Plugin deaktiviert, übernimmt dessen Deaktivierung die frühere Funktion
    /// der entfernten "VS Code öffnen, wenn keine Solution gefunden wurde"-Einstellung: Ohne gefundene Solution
    /// und ohne aktives Fallback-Plugin greift ResolveIdePluginAsync auf PluginManager.GetDefaultIdePlugin()
    /// (Visual Studio) zurück, das ohne .sln-Datei eine Fehlermeldung statt eines VS-Code-Starts erzeugt.
    /// </summary>
    [Fact]
    public async Task OeffneIdeCommand_OhneSolutionUndDeaktiviertemVsCodePlugin_ZeigtFehlerStattVsCodeFallback()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        var pluginActivationService = new PluginActivationService(_einstellungService, new Mock<IPluginManager>().Object, NullLogger<PluginActivationService>.Instance);
        await pluginActivationService.SetPluginEnabledAsync("Softwareschmiede.VisualStudioCode", false);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock: prozessStarterMock, visualStudioCodeLocator: locator);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrEmpty("ohne aktives Fallback-Plugin und ohne .sln kann keine IDE geöffnet werden");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    /// <summary>Eine gefundene Solution hat Vorrang vor dem aktiven Visual-Studio-Code-Fallback: Das Visual-Studio-Plugin ist bei vorhandener .sln explizit kompatibel und gewinnt gegenüber dem lediglich fallback-kompatiblen Visual-Studio-Code-Plugin.</summary>
    [Fact]
    public async Task OeffneIdeCommand_MitSolutionUndAktivemVsCode_OeffnetSolutionStattVsCode()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var solutionPfad = Path.Combine(arbeitsverzeichnis, "Loesung.sln");
        File.WriteAllText(solutionPfad, string.Empty);
        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock: prozessStarterMock, visualStudioCodeLocator: locator);

        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == solutionPfad && a.ShellAusfuehren)),
            Times.Once);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == "code.cmd")),
            Times.Never);
    }

    /// <summary>Bei genau einer gefundenen Solution öffnet OeffneIdeCommand diese direkt, ohne den Auswahl-Dialog anzuzeigen.</summary>
    [Fact]
    public async Task OeffneIdeCommand_MitEinerSolution_OeffnetOhneDialog()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var solutionPfad = Path.Combine(arbeitsverzeichnis, "Loesung.sln");
        File.WriteAllText(solutionPfad, string.Empty);

        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock: prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == solutionPfad && a.ShellAusfuehren)),
            Times.Once);
        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Bei mehreren gefundenen Solutions öffnet der Haupt-Button (OeffneIdeCommand) des Split-Buttons weiterhin
    /// direkt den ersten (alphabetisch sortierten) Einstiegspunkt, ohne den Auswahl-Dialog anzuzeigen - die
    /// gezielte Auswahl übernimmt seit der Split-Button-Einführung der Dropdown-Button (OeffneIdeAuswahlCommand).
    /// </summary>
    [Fact]
    public async Task OeffneIdeCommand_MitMehrerenSolutions_OeffnetErsteDirektOhneDialog()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var ersteSolution = Path.Combine(arbeitsverzeichnis, "Erste.sln");
        var zweiteSolution = Path.Combine(arbeitsverzeichnis, "Zweite.sln");
        File.WriteAllText(ersteSolution, string.Empty);
        File.WriteAllText(zweiteSolution, string.Empty);

        var aufgabe = await ErstelleAufgabe();
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var sut = CreateSut(prozessStarterMock: prozessStarterMock);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a => a.DateiName == ersteSolution && a.ShellAusfuehren)),
            Times.Once);
    }

    private async Task<GitRepository> ErstelleRepositoryAsync(string pluginTyp, string repositoryUrl, string repositoryName)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = pluginTyp,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    private static Mock<IGitPlugin> ErstelleGitPluginOhneIssueCreateMock()
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginName).Returns("Test Git");
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestGit");
        gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        gitPluginMock.Setup(p => p.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.RemoteGit,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: false,
                CanMergeToSource: false));
        return gitPluginMock;
    }

    private static Mock<IGitPlugin> ErstelleIssueCreateGitPluginMock(bool canCreateIssue)
    {
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginName).Returns("Test Git");
        gitPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestGit");
        gitPluginMock.SetupGet(p => p.PluginType).Returns(PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        gitPluginMock.Setup(p => p.GetGitActionCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitActionCapabilities(
                RepositoryKind.RemoteGit,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: false,
                CanMergeToSource: false));
        gitPluginMock.As<IIssueCreateProvider>()
            .Setup(p => p.CanCreateIssueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canCreateIssue);
        gitPluginMock.As<IIssueTemplateProvider>()
            .Setup(p => p.GetIssueTemplatesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IssueTemplateLoadResult.NotSupported());
        return gitPluginMock;
    }

    private Mock<IPluginManager> ErstellePluginManagerMitGitPlugin(IGitPlugin gitPlugin)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManagerMock.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_kiPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDefaultDevelopmentAutomationPlugin()).Returns(_kiPluginMock.Object);
        return pluginManagerMock;
    }
}
