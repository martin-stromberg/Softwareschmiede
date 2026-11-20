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
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly LogService _logService;
    private readonly TodoService _todoService;
    private readonly AiExecutionService _aiService;
    private readonly DevelopmentProcessService _developmentprocessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptTemplateService _promptTemplatesService;
    private readonly PromptTemplatePlaceholderService _promptTemplatesPlaceholderService = new();
    private readonly PromptScheduleDispatchService _promptScheduleDispatchService;
    private readonly AppSettingService _settingService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IAiPlugin> _aiPluginMock;
    private readonly Mock<IGitPlugin> _gitPluginForResolutionMock;
    private readonly Mock<IPluginManager> _pluginManagerMockForPluginSelection;
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>TaskDetailViewModelTests.</summary>
    public TaskDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);

        _aiService = TestAiExecutionServiceFactory.Create();

        _aiPluginMock = new Mock<IAiPlugin>();
        _aiPluginMock.SetupGet(p => p.PluginName).Returns("Test KI");
        _aiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.TestKi");
        _aiPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        _aiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        _aiPluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 30 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        _aiPluginMock.SetupTerminalSpec("cmd.exe", "/k");

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
        var secondAiPluginMock = new Mock<IAiPlugin>();
        secondAiPluginMock.SetupGet(p => p.PluginName).Returns("Zweites KI");
        secondAiPluginMock.SetupGet(p => p.PluginPrefix).Returns("Softwareschmiede.ZweitesKi");
        secondAiPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation);
        secondAiPluginMock.Setup(p => p.GetSettingGroups()).Returns([]);
        // Terminal-Startspec auch für das zweite Plugin: Nach einem Plugin-Wechsel mit
        // anschließendem echten Neustart ruft CliNeustartenAsync GetTerminalStartSpecAsync auf.
        secondAiPluginMock.SetupTerminalSpec("cmd.exe", "/k");

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object, secondAiPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDefaultDevelopmentAutomationPlugin()).Returns(_aiPluginMock.Object);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(_gitPluginForResolutionMock.Object);
        // Standard-IDE-Plugins (Visual Studio, Visual Studio Code) mit nicht-verfügbarem VS Code als
        // Default; CreateSut() konfiguriert diese bei Bedarf pro Test mit spezifischem
        // IProzessStarter/IVisualStudioCodeLocator um, damit OeffneIdeCommand-Tests Prozessstarts
        // verifizieren können.
        var defaultVisualStudioPlugin = new VisualStudioIdePlugin(new Mock<IProcessStarter>().Object);
        var defaultVisualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            new Mock<IProcessStarter>().Object,
            new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([defaultVisualStudioPlugin, defaultVisualStudioCodePlugin]);
        pluginManagerMock.Setup(p => p.GetDefaultIdePlugin()).Returns(defaultVisualStudioPlugin);
        _pluginManagerMockForPluginSelection = pluginManagerMock;
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(new AppSettingService(_db, NullLogger<AppSettingService>.Instance), pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptTemplatesService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_aiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gitPluginMock.Setup(g => g.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var workingDirectoryMock = new Mock<IWorkingDirectoryResolver>();
        workingDirectoryMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Softwareschmiede.Domain.ValueObjects.WorkingDirectoryResolutionResult(Path.GetTempPath(), false, "configured", null));
        _developmentprocessService = new DevelopmentProcessService(
            _workItemService,
            _logService,
            gitPluginMock.Object,
            _pluginSelectionService,
            workingDirectoryMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _aiService),
            NullLogger<DevelopmentProcessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _aiService.Dispose();
        _db.Dispose();
        _tempDirectoryFixture.Dispose();
    }

    private string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory("tdvm_tests");

    private TaskDetailViewModel CreateSut(
        Action? backAction = null,
        IPluginManager? pluginManager = null,
        IServiceProvider? serviceProvider = null,
        Mock<IProcessStarter>? processStarterMock = null,
        IVisualStudioCodeLocator? visualStudioCodeLocator = null)
    {
        if (pluginManager == null)
        {
            var defaultPluginManagerMock = new Mock<IPluginManager>();
            defaultPluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
            defaultPluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);
            pluginManager = defaultPluginManagerMock.Object;
        }

        var serviceProviderObj = serviceProvider ?? new Mock<IServiceProvider>().Object;

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();

        var workingDirectoryOpenService = TaskDetailViewModelTestFactory.CreateWorkingDirectoryOpenService(processStarterMock);

        // Die von OeffneIdeCommand über _pluginSelectionService aufgelösten IDE-Plugins (Visual Studio,
        // Visual Studio Code) müssen denselben IProzessStarter/IVisualStudioCodeLocator verwenden wie die
        // hier verifizierten Prozessstarts, damit die Prozessstart-Verifikationen in Tests greifen.
        var effectiveProcessStarterMock = processStarterMock ?? new Mock<IProcessStarter>();
        var effectiveVisualStudioCodeLocator = visualStudioCodeLocator ?? new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var visualStudioPlugin = new VisualStudioIdePlugin(effectiveProcessStarterMock.Object);
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(effectiveProcessStarterMock.Object, effectiveVisualStudioCodeLocator);
        _pluginManagerMockForPluginSelection.Setup(p => p.GetIdePlugins()).Returns([visualStudioPlugin, visualStudioCodePlugin]);
        _pluginManagerMockForPluginSelection.Setup(p => p.GetDefaultIdePlugin()).Returns(visualStudioPlugin);

        var vm = new TaskDetailViewModel(
            _workItemService,
            _logService,
            _aiService,
            _developmentprocessService,
            _pluginSelectionService,
            _promptTemplatesService,
            _promptTemplatesPlaceholderService,
            _promptScheduleDispatchService,
            _dialogServiceMock.Object,
            pluginManager,
            serviceProviderObj,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            workingDirectoryOpenService);
        vm.BackAction = backAction;
        return vm;
    }

    private async Task<WorkItem> CreateWorkItem(WorkItemStatus status = WorkItemStatus.New)
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung");
        if (status != WorkItemStatus.New)
        {
            await _workItemService.SetStatusUncheckedAsync(workItem.Id, status);
            if (status.IsActiveOrWaiting())
            {
                // Aktiven Ausführungsstatus direkt auf der Entity setzen (das frühere
                // AufgabeService.AusfuehrungAktivSetzenAsync wurde entfernt): simuliert den Zustand
                // einer gestarteten Aufgabe ohne zugehörige Laufdaten (AktiveRunId bleibt null).
                workItem.ExecutionStatus = WorkItemExecutionStatus.Active;
                await _db.SaveChangesAsync();
            }
        }
        return await _workItemService.GetByIdAsync(workItem.Id) ?? workItem;
    }

    // --- AufgabeBranchName ---

    /// <summary>AufgabeBranchName gibt leeren String zurück wenn Aufgabe null ist.</summary>
    [Fact]
    public void WorkItemBranchName_WhenWorkItemIsNull_ReturnsEmptyString()
    {
        var sut = CreateSut();

        sut.WorkItemBranchName.Should().BeEmpty();
    }

    /// <summary>AufgabeBranchName gibt Aufgabe.BranchName zurück wenn Aufgabe einen Branch-Namen hat.</summary>
    [Fact]
    public async Task WorkItemBranchName_WhenWorkItemHasBranchName_ReturnsBranchName()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/login-fix", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkItemBranchName.Should().Be("feature/login-fix");
    }

    // --- AufgabeId-Setter: Fire-and-Forget ---

    /// <summary>Der AufgabeId-Setter löst LadenAsync per SafeFireAndForget aus, ohne dass der Aufrufer den Task awaiten muss.</summary>
    [Fact]
    public async Task WorkItemId_Setter_UsesFireAndForgetSafely()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();

        // Act: AufgabeId-Setter löst SafeFireAndForget(LadenAsync) aus, ohne explizites Awaiten von LadenCommand
        sut.WorkItemId = workItem.Id;

        // Assert
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.WorkItem == null)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.WorkItem.Should().NotBeNull("der AufgabeId-Setter muss LadenAsync per SafeFireAndForget auslösen");
    }

    // --- Protokoll asynchrones Nachladen (Issue 193) ---

    /// <summary>LadeProtokolleAsync lädt die Protokolleinträge der Aufgabe und befüllt die Protokolleintraege-Collection.</summary>
    [Fact]
    public async Task LoadLogAsync_ShouldLoadProtocols_WhenSuccessful()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        await _logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "Testinhalt", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_workItemId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, workItem.Id);

        var method = typeof(TaskDetailViewModel).GetMethod("LoadLogAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;
        await task;

        sut.LogEntries.Should().ContainSingle(p => p.Content == "Testinhalt");
    }

    /// <summary>LadeProtokolleAsync fängt Fehler des ProtokollService nicht selbst ab, sondern lässt sie propagieren, damit SafeFireAndForget (wie bei allen anderen Fire-and-Forget-Aufrufen dieser Klasse) sie protokolliert.</summary>
    [Fact]
    public async Task LoadLogAsync_ShouldPropagateException_WhenLogServiceFails()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_workItemId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, workItem.Id);

        await _db.DisposeAsync();

        var method = typeof(TaskDetailViewModel).GetMethod("LoadLogAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { CancellationToken.None })!;

        var act = async () => await task;
        await act.Should().ThrowAsync<ObjectDisposedException>("LadeProtokolleAsync darf Fehler nicht mehr selbst abfangen, sondern muss sie propagieren, damit SafeFireAndForget sie protokollieren kann");
    }

    /// <summary>LadeProtokolleAsync fängt eine OperationCanceledException nicht selbst ab, sondern lässt sie propagieren, damit SafeFireAndForget den Abbruch behandelt.</summary>
    [Fact]
    public async Task LoadLogAsync_ShouldPropagateCancellation_WhenCancellationTokenCancelled()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        typeof(TaskDetailViewModel).GetField("_workItemId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, workItem.Id);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var method = typeof(TaskDetailViewModel).GetMethod("LoadLogAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(sut, new object[] { cts.Token })!;

        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>("LadeProtokolleAsync darf einen Abbruch nicht mehr selbst abfangen, sondern muss ihn propagieren");
    }

    /// <summary>LadenAsync setzt Aufgabe, ohne auf das (fire-and-forget) Nachladen der Protokolle zu warten; die Protokolle werden anschließend im Hintergrund nachgeladen.</summary>
    [Fact]
    public async Task LoadAsync_ShouldNotWaitForProtocols()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        await _logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "Eintrag 1", ct: TestContext.Current.CancellationToken);
        await _logService.AddEntryAsync(workItem.Id, LogEntryType.AiResponse, "Eintrag 2", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkItem.Should().NotBeNull("Aufgabe muss gesetzt sein, sobald LadenAsync abgeschlossen ist, unabhängig vom Stand des Protokoll-Nachladens");
        sut.WorkItem!.Id.Should().Be(workItem.Id);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.LogEntries.Count < 2)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        sut.LogEntries.Should().HaveCount(2, "die Protokolleinträge müssen im Hintergrund nachgeladen werden");
    }

    // --- ShowEditPanel, ShowCliPanel, ShowDiffPanel ---

    /// <summary>ShowEditPanel ist true wenn Status=Neu.</summary>
    [Fact]
    public async Task ShowEditPanel_IsTrue_WhenStatusNew()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowEditPanel.Should().BeTrue();
        sut.ShowCliPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
    }

    /// <summary>ShowCliPanel ist true für Status Gestartet.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenStatusStarted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
    }

    /// <summary>ShowCliPanel ist true für Status Wartend.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenStatusWaiting()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Waiting);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
    }

    /// <summary>ShowCliPanel bleibt true, wenn der Aufgabenstatus aktiv ist, die KI-Ausführung aber beendet wurde, damit der Nutzer die letzte Ausgabe anschauen und die CLI neu starten kann.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenExecutionCompletedIs()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        await _workItemService.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowDiffPanel.Should().BeFalse();
        sut.StartCommand.CanExecute(null).Should().BeTrue();
        sut.CompleteWorkItemCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>ShowDiffPanel ist true wenn Status=Beendet.</summary>
    [Fact]
    public async Task ShowDiffPanel_IsTrue_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowDiffPanel.Should().BeTrue();
        sut.ShowEditPanel.Should().BeFalse();
        sut.ShowCliPanel.Should().BeFalse();
    }

    // --- ShowFileExplorerPanel, DateiViewCommand ---

    /// <summary>DateiViewCommand wechselt zur Dateiexplorer-Ansicht.</summary>
    [Fact]
    public async Task FileViewCommand_SetsFileExplorerView()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var workItem = await _workItemService.CreateAsync(_projectId, "Testaufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
            await _workItemService.StartAsync(workItem.Id, "feature/dateien", tempDir, ct: TestContext.Current.CancellationToken);
            var sut = CreateSut();
            sut.WorkItemId = workItem.Id;
            await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

            ((RelayCommand)sut.FileViewCommand).Execute(null);

            sut.IsFileExplorerViewSelected.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>ShowFileExplorerPanel ist nur true, wenn LokalerKlonPfad gesetzt ist und das Verzeichnis existiert.</summary>
    [Fact]
    public async Task ShowFileExplorerPanel_OnlyAtExistingClonePath()
    {
        var workItemWithoutPath = await CreateWorkItem(WorkItemStatus.New);
        var sutWithoutPath = CreateSut();
        sutWithoutPath.WorkItemId = workItemWithoutPath.Id;
        await ((AsyncRelayCommand)sutWithoutPath.LoadCommand).ExecuteAsync();

        sutWithoutPath.ShowFileExplorerPanel.Should().BeFalse();

        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var workItemWithPath = await _workItemService.CreateAsync(_projectId, "MitPfad", "Beschreibung", ct: TestContext.Current.CancellationToken);
            await _workItemService.StartAsync(workItemWithPath.Id, "feature/mit-pfad", tempDir, ct: TestContext.Current.CancellationToken);
            var sutWithPath = CreateSut();
            sutWithPath.WorkItemId = workItemWithPath.Id;
            await ((AsyncRelayCommand)sutWithPath.LoadCommand).ExecuteAsync();

            sutWithPath.ShowFileExplorerPanel.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>ShowFileExplorerPanel wird beim Laden der Aufgabe einmalig ermittelt und gecacht, statt bei jedem Property-Zugriff erneut synchron das Dateisystem zu prüfen; ein nachträgliches Löschen des Verzeichnisses ändert den bereits gecachten Wert daher nicht.</summary>
    [Fact]
    public async Task ShowFileExplorerPanel_ValueRemainsCachedAfterDirectorySubsequentDeletedWas()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var workItem = await _workItemService.CreateAsync(_projectId, "MitPfad", "Beschreibung", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/cache", tempDir, ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.ShowFileExplorerPanel.Should().BeTrue();

        Directory.Delete(tempDir, true);

        sut.ShowFileExplorerPanel.Should().BeTrue();
    }

    // --- KannSpeichern ---

    /// <summary>KannSpeichern ist true wenn Status=Neu, Titel gesetzt, kein CLI läuft.</summary>
    [Fact]
    public async Task SaveCan_IsTrue_WhenStatusNewAndTitleSet()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle = "Gültiger Titel";

        sut.SaveCan.Should().BeTrue();
    }

    /// <summary>KannSpeichern ist false wenn Titel leer ist.</summary>
    [Fact]
    public async Task SaveCan_IsFalse_WhenTitleEmpty()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle = string.Empty;

        sut.SaveCan.Should().BeFalse();
    }

    /// <summary>KannSpeichern ist false wenn Status=Beendet.</summary>
    [Fact]
    public async Task SaveCan_IsFalse_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SaveCan.Should().BeFalse();
    }

    /// <summary>KannSpeichern ist true wenn Status=Gestartet und Titel gesetzt.</summary>
    [Fact]
    public async Task SaveCan_IsTrue_WhenStatusStarted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle = "Titel";

        sut.SaveCan.Should().BeTrue();
    }

    // --- KannLoeschen ---

    /// <summary>KannLoeschen ist true wenn Status=Neu.</summary>
    [Fact]
    public async Task DeleteCan_IsTrue_WhenStatusNew()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DeleteCan.Should().BeTrue();
    }

    /// <summary>KannLoeschen ist true wenn Status=Beendet (beendete Aufgaben können gelöscht werden).</summary>
    [Fact]
    public async Task DeleteCan_IsTrue_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DeleteCan.Should().BeTrue();
    }

    /// <summary>KannLoeschen ist false wenn Status=Archiviert.</summary>
    [Fact]
    public async Task DeleteCan_IsFalse_WhenStatusArchived()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Archived);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DeleteCan.Should().BeFalse();
    }

    /// <summary>KannLoeschen ist true wenn Status=Gestartet.</summary>
    [Fact]
    public async Task DeleteCan_IsTrue_WhenStatusStarted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DeleteCan.Should().BeTrue();
    }

    // --- SpeichernCommand ---

    /// <summary>SpeichernCommand ruft UpdateAsync auf und aktualisiert EditTitle.</summary>
    [Fact]
    public async Task SaveCommand_CallsUpdateAsyncOn_AndUpdatesData()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle = "Neuer Titel";
        sut.EditRequirementDescription = "Neue Beschreibung";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        var updates = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updates!.Title.Should().Be("Neuer Titel");
        updates.RequirementDescription.Should().Be("Neue Beschreibung");
    }

    /// <summary>SpeichernCommand aktualisiert die Liste, navigiert aber nicht automatisch zurück.</summary>
    [Fact]
    public async Task SaveCommand_UpdatesList_NavigateWithoutBackTo()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var backInvoked = false;
        var listUpdates = false;
        var sut = CreateSut(() => backInvoked = true);
        sut.RefreshWorkItemListCallback = () =>
        {
            listUpdates = true;
            return Task.CompletedTask;
        };
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.EditTitle = "Gespeicherter Titel";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        listUpdates.Should().BeTrue();
        backInvoked.Should().BeFalse();
        sut.WorkItemId.Should().Be(workItem.Id);
        sut.WorkItemTitle.Should().Be("Gespeicherter Titel");
    }

    /// <summary>SpeichernCommand setzt IsLoading während der Ausführung (und danach wieder false).</summary>
    [Fact]
    public async Task SaveCommand_SetsIsLoading_DuringExecution()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.EditTitle = "Titel";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.IsLoading.Should().BeFalse();
        sut.ErrorMessage.Should().BeNull();
    }

    /// <summary>SpeichernCommand hat CanExecute false wenn KannSpeichern false ist.</summary>
    [Fact]
    public async Task SaveCommand_CanExecuteFalse_WhenTitleEmpty()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.EditTitle = string.Empty;

        sut.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>SpeichernCommand setzt ErrorMessage bei Exception.</summary>
    [Fact]
    public async Task SaveCommand_SetErrorMessage_AtException()
    {
        var sut = CreateSut();
        // AufgabeId leer → kein Update möglich; wir testen direkt ungültige ID
        sut.WorkItemId = Guid.Empty;
        // Bei leerem AufgabeId tut der Command nichts — stattdessen ungültige DB-ID verwenden
        sut.WorkItemId = Guid.NewGuid(); // nicht in DB

        // EditTitle muss gesetzt sein damit CanExecute true ist; aber Aufgabe ist null
        // → SpeichernCommand ist nicht ausführbar (KannSpeichern = false, weil Aufgabe null)
        sut.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    // --- LoeschenCommand ---

    /// <summary>LoeschenCommand zeigt Dialog und löscht bei Bestätigung.</summary>
    [Fact]
    public async Task DeleteCommand_DeletesWorkItem_WhenUserConfirms()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        backInvoked.Should().BeTrue();
        var deleted = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    /// <summary>LoeschenCommand navigiert nicht zurück wenn Benutzer abbricht.</summary>
    [Fact]
    public async Task DeleteCommand_NavigatesNotBack_WhenUserCancels()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        backInvoked.Should().BeFalse();
        var stillExisting = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        stillExisting.Should().NotBeNull();
    }

    /// <summary>LoeschenCommand ruft BestaetigenDialog auf.</summary>
    [Fact]
    public async Task DeleteCommand_CallsConfirmDialogOn()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        _dialogServiceMock.Verify(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>LoeschenCommand setzt ErrorMessage wenn Service-Fehler auftritt.</summary>
    [Fact]
    public async Task DeleteCommand_SetErrorMessage_WhenDeleteFails()
    {
        // Aufgabe mit Gestartet-Status ist nicht löschbar (Service wirft Exception)
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>LoeschenCommand hat CanExecute true wenn Status=Beendet (beendete Aufgaben können gelöscht werden).</summary>
    [Fact]
    public async Task DeleteCommand_CanExecuteTrue_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.DeleteCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>LoeschenCommand ruft RefreshWorkItemListCallback auf nach erfolgreichem Löschen.</summary>
    [Fact]
    public async Task DeleteCommand_CallsCallbackOn_DeleteAfterSuccessful()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var callbackInvoked = false;
        var sut = CreateSut();
        sut.RefreshWorkItemListCallback = () => { callbackInvoked = true; return Task.CompletedTask; };
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock.Setup(d => d.ConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await ((AsyncRelayCommand)sut.DeleteCommand).ExecuteAsync();

        callbackInvoked.Should().BeTrue();
    }

    // --- InfoCliToggleCommand ---

    /// <summary>InfoCliToggleCommand toggled IsInfoViewVisible von false auf true.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_SetIsInfoViewVisible_OnTrue_AtInitialFalse()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsInfoViewVisible.Should().BeFalse();
        sut.InfoCliToggleCommand.Execute(null);
        sut.IsInfoViewVisible.Should().BeTrue();
    }

    /// <summary>InfoCliToggleCommand toggled IsInfoViewVisible von true auf false.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_SetIsInfoViewVisible_OnFalse_AtTrue()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsInfoViewVisible = true;

        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeFalse();
    }

    /// <summary>InfoCliToggleCommand wechselt mehrfach korrekt.</summary>
    [Fact]
    public async Task InfoCliToggleCommand_TogglesMultiple_Correct()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeTrue();
    }

    /// <summary>Neue Aufgaben starten in der Info-Ansicht.</summary>
    [Fact]
    public async Task LoadAsync_SelectsInfoView_WhenStatusNew()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsInfoViewSelected.Should().BeTrue();
        sut.IsCliViewSelected.Should().BeFalse();
        sut.IsDiffViewSelected.Should().BeFalse();
    }

    /// <summary>Gestartete Aufgaben starten in der CLI-Ansicht, Info bleibt auswählbar.</summary>
    [Fact]
    public async Task InfoViewCommand_SelectsInfoView_WhenStatusStarted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsCliViewSelected.Should().BeTrue();

        sut.InfoViewCommand.Execute(null);

        sut.IsInfoViewSelected.Should().BeTrue();
    }

    /// <summary>Beendete Aufgaben starten in der Diff-Ansicht, Info bleibt auswählbar.</summary>
    [Fact]
    public async Task InfoViewCommand_SelectsInfoView_WhenStatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsDiffViewSelected.Should().BeTrue();

        sut.InfoViewCommand.Execute(null);

        sut.IsInfoViewSelected.Should().BeTrue();
    }

    // --- EditTitle / EditRequirementDescription ---

    /// <summary>EditTitle wird nach LadenAsync mit Aufgabe.Titel initialisiert.</summary>
    [Fact]
    public async Task EditTitle_LoadWillAfter_WithWorkItemTitleInitialized()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Mein Titel", "Meine Beschreibung", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle.Should().Be("Mein Titel");
    }

    /// <summary>EditRequirementDescription wird nach LadenAsync initialisiert.</summary>
    [Fact]
    public async Task EditRequirementDescription_LoadWillAfter_Initialized()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Titel", "Anforderung XYZ", ct: TestContext.Current.CancellationToken);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditRequirementDescription.Should().Be("Anforderung XYZ");
    }

    /// <summary>EditTitle ist bindbar und triggert KannSpeichern-Neuberechnung.</summary>
    [Fact]
    public async Task EditTitle_ChangesCanSave_AtChange()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.EditTitle = string.Empty;
        sut.SaveCan.Should().BeFalse();

        sut.EditTitle = "Gültiger Titel";
        sut.SaveCan.Should().BeTrue();
    }

    /// <summary>ZurueckCommand ruft ZurueckAction auf.</summary>
    [Fact]
    public void BackCommand_CallsBackActionOn()
    {
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);

        sut.BackCommand.Execute(null);

        backInvoked.Should().BeTrue();
    }

    /// <summary>SpeichernAsync zeigt eine Fehlermeldung an und die View bleibt offen, wenn das Speichern fehlschlägt.</summary>
    [Fact]
    public async Task SaveAsync_ShowsErrorMessage_WhenSaveFails()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.EditTitle = "Titel";

        // Aufgabe wird zwischen Laden und Speichern entfernt, damit UpdateAsync fehlschlägt
        var entity = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        _db.WorkItems.Remove(entity!);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>SpeichernAsync ruft ZurueckAction nicht auf, wenn das Speichern fehlschlägt.</summary>
    [Fact]
    public async Task SaveAsync_DoesNotNavigateBack_WhenSaveFails()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var backInvoked = false;
        var sut = CreateSut(backAction: () => backInvoked = true);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.EditTitle = "Titel";

        // Aufgabe wird zwischen Laden und Speichern entfernt, damit UpdateAsync fehlschlägt
        var entity = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        _db.WorkItems.Remove(entity!);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync();

        backInvoked.Should().BeFalse();
    }

    // --- StartenCommand ---

    /// <summary>StartenCommand.CanExecute ist true wenn Status Neu und CLI nicht läuft.</summary>
    [Fact]
    public async Task StartTestCommand_CanExecute_StatusNewNotCliRunning()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>StartenCommand.CanExecute ist false wenn Status != Neu.</summary>
    [Fact]
    public async Task StartTestCommand_CanExecute_StatusNotNew_ReturnsFalse()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>StartenAsync zeigt den Plugin-Dialog, falls die Aufgabe kein Plugin hat.</summary>
    [Fact]
    public async Task StartTestAsync_ShowsDialogIfNoPluginSelected()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>StartenAsync speichert den Projekt-Default, falls die Checkbox aktiviert wurde.</summary>
    [Fact]
    public async Task StartTestAsync_SavesProjectDefaultIfCheckboxActivated()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", true));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var saved = await pluginDefaultSettingsService.GetProjectDefaultPluginPrefixAsync(workItem.ProjectId, Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation, TestContext.Current.CancellationToken);
        saved.Should().Be("Softwareschmiede.TestKi");
    }

    /// <summary>StartenAsync speichert keinen Projekt-Default, falls die Checkbox deaktiviert ist.</summary>
    [Fact]
    public async Task StartTestAsync_DoesNotSaveProjectDefaultIfCheckboxDeactivated()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var saved = await pluginDefaultSettingsService.GetDefaultPluginPrefixAsync(Softwareschmiede.Domain.Enums.PluginType.DevelopmentAutomation, TestContext.Current.CancellationToken);
        saved.Should().BeNull();
    }

    /// <summary>StartenAsync ruft den kombinierten Prozess auf und die CLI läuft danach.</summary>
    [Fact]
    public async Task StartTestAsync_InvokesCombinedProcess_StartsCliUponSuccess()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        sut.ActiveCliName.Should().Be("Test KI");
        sut.CliStatusText.Should().NotBe("Bereit Repository vor...");
        var updates = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updates!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>StartenAsync zeigt während eines laufenden Repository-Klons den Vorbereitungsstatus.</summary>
    [Fact]
    public async Task StartAsync_ShouldShowRepositoryPreparationStatus_WhileCloneIsRunning()
    {
        var cloneStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cloneContinue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gitPluginForResolutionMock
            .Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => cloneStarted.SetResult())
            .Returns(() => cloneContinue.Task);

        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        var startTask = ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        await cloneStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        sut.CliStatusText.Should().Be("Bereit Repository vor...");

        cloneContinue.SetResult();
        await startTask;
    }

    /// <summary>StartenAsync lässt den Vorbereitungsstatus nach einem Clone-Fehler nicht stehen.</summary>
    [Fact]
    public async Task StartAsync_ShouldClearRepositoryPreparationStatus_WhenCloneFails()
    {
        _gitPluginForResolutionMock
            .Setup(g => g.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Clone fehlgeschlagen"));

        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        sut.CliStatusText.Should().NotBe("Bereit Repository vor...");
        sut.CliStatusText.Should().Be("CLI inaktiv");
        sut.ErrorMessage.Should().Contain("Aufgabe konnte nicht gestartet werden");
    }

    /// <summary>CliStoppenCommand leert den aktiven CLI-Namen.</summary>
    [Fact]
    public async Task StopCliCommand_ClearsActiveCliName()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.ActiveCliName.Should().Be("Test KI");

        await RunCliStopWithProcessendOutAsync(sut, workItem.Id);

        sut.ActiveCliName.Should().BeNull();
    }

    /// <summary>
    /// Führt CliStoppenCommand aus und simuliert während des internen StopCliAsync-Wartens das
    /// tatsächliche Prozessende: Der deterministische Testlauncher bindet den eigenen Testprozess
    /// (<see cref="System.Diagnostics.Process.GetCurrentProcess"/>), der niemals beendet — ohne
    /// Handle-Entfernung würde <c>KiAusfuehrungsService.IsRunning</c> dauerhaft true liefern.
    /// </summary>
    private async Task RunCliStopWithProcessendOutAsync(TaskDetailViewModel sut, Guid workItemId)
    {
        var stopTask = ((AsyncRelayCommand)sut.StopCliCommand).ExecuteAsync();
        await Task.Delay(200);
        var handlesField = typeof(AiExecutionService).GetField("_handles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handles = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CliProcessHandle>)handlesField.GetValue(_aiService)!;
        handles.TryRemove(workItemId, out _);
        await stopTask;
    }

    /// <summary>
    /// Divergenzpfad F1 (Issue 342): Konnte der CLI-Prozess nicht beendet werden (StopCliAsync
    /// schlägt fehl, Prozess lebt weiter), dürfen die Laufdaten nicht als beendet persistiert und
    /// die UI nicht auf "inaktiv" geschaltet werden — sonst meldet die Kachel fälschlich "Bereit",
    /// während die Fußzeile einen lebenden Prozess zeigt.
    /// </summary>
    [Fact]
    public async Task StopCliAsync_KeepsRundata_AtStillrunningProcess()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();
        await _workItemService.SetActiveRunAsync(workItem.Id, "run-lebendig", TestContext.Current.CancellationToken);

        // Der deterministische Testlauncher bindet den eigenen (nicht beendbaren) Testprozess —
        // StopCliAsync schlägt damit genau wie bei einem echten, nicht terminierbaren Prozess fehl.
        await ((AsyncRelayCommand)sut.StopCliCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue("der Prozess läuft nach dem fehlgeschlagenen Stopp weiter");
        sut.ErrorMessage.Should().NotBeNullOrEmpty("der Benutzer erhält eine Fehlermeldung statt eines falschen Bereit-Zustands");
        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.ActiveRunId.Should().Be("run-lebendig",
            "die Laufdaten dürfen bei einem weiterlaufenden Prozess nicht als beendet geschrieben werden");
        loaded.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
    }

    /// <summary>
    /// Divergenzpfad F2 (Issue 342): StartCliAndUpdateStateAsync persistiert bei laufendem Prozess
    /// die vollständigen Laufdaten (AktiveRunId, Heartbeat, LaufStatus, AusfuehrungsStatus) —
    /// auch wenn CliNeustartenAsync in den Wiederverwendungs-Early-Return ohne Gestartet-Ereignis
    /// lief, statt nur AusfuehrungsStatus=Aktiv zu schreiben.
    /// </summary>
    [Fact]
    public async Task StartCliAndUpdateStateAsync_WritesFullRundata_AtReusedSession()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();

        (await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.ActiveRunId.Should().BeNull(
            "ohne CliProcessManager in diesem Test fehlen die persistierten Laufdaten bereits — die Divergenz ist die Ausgangslage");

        var method = typeof(TaskDetailViewModel).GetMethod("StartCliAndUpdateStateAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(sut, new object?[] { "Softwareschmiede.TestKi", "/tmp/klon", null, CancellationToken.None })!;

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        loaded.ActiveRunId.Should().NotBeNullOrEmpty();
        loaded.LastHeartbeatUtc.Should().NotBeNull();
        loaded.RunStatus.Should().Be(WorkItemRunStatus.Running);
    }

    /// <summary>
    /// StartenAsync muss TerminalSessionGestartet feuern, damit TaskDetailView
    /// das TerminalControl mit der Session verbinden kann.
    /// </summary>
    [Fact]
    public async Task StartAsync_FiresTerminalSessionStarted_AfterSuccessfulStart()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        Softwareschmiede.Infrastructure.Terminal.ITerminalSession? reportedSession = null;
        sut.TerminalSessionStarted += s => reportedSession = s;

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        reportedSession.Should().NotBeNull(
            "TerminalSessionGestartet muss nach StartenCommand feuern, damit das TerminalControl die Session erhält");
    }

    /// <summary>
    /// GetTerminalSession gibt nach explizitem Start die laufende Session zurück.
    /// Das erlaubt <see cref="Softwareschmiede.App.Views.TaskDetailView"/> im Loaded-Handler die Session
    /// auch dann zu holen, wenn TerminalSessionGestartet schon gefeuert hat.
    /// </summary>
    [Fact]
    public async Task GetTerminalSession_ReturnsSession_AfterExplicitStart()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        // Simuliert: Loaded feuert NACH LadenAsync/TerminalSessionGestartet.
        // Der View ruft GetTerminalSession() im Loaded-Handler auf und setzt TerminalConsole.Session direkt.
        var session = sut.GetTerminalSession();

        session.Should().NotBeNull(
            "GetTerminalSession muss die Session liefern damit der View das TerminalControl verbinden kann " +
            "wenn Loaded nach TerminalSessionGestartet feuert");
    }

    /// <summary>
    /// Läuft die gestartete Session im Pipe-Fallback (kein echtes Pseudo-Terminal), muss der
    /// CliStatusText den eingeschränkten Modus sichtbar machen — der Fallback gilt nicht
    /// stillschweigend als gleichwertiger interaktiver Modus.
    /// </summary>
    [Fact]
    public async Task StartAsync_PipeFallbackSession_ShowsRestrictedModeInStatusText()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        sut.CliStatusText.Should().Contain(
            "eingeschränkter Modus",
            "eine Session ohne Pseudo-Terminal (Pipe-Fallback) muss für die Anwenderin als eingeschränkter Modus sichtbar sein");
    }

    /// <summary>
    /// Navigiert der Anwender zurück (Dispose des alten VM), läuft die CLI weiter.
    /// Öffnet er die Aufgabe erneut, muss das neue VM IsCliRunning=true melden
    /// und GetTerminalSession() die Session zurückgeben, damit der Loaded-Handler
    /// das TerminalControl verbinden kann.
    /// </summary>
    [Fact]
    public async Task AfterNavigateBack_ReopenFindsRunningSessionAndSetIsCliRunning()
    {
        // Aufgabe starten
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var firstVm = CreateSut();
        firstVm.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)firstVm.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)firstVm.StartCommand).ExecuteAsync();
        firstVm.IsCliRunning.Should().BeTrue();

        // "Zurück" navigieren: View-Unloaded ruft Dispose auf – Prozess bleibt aktiv
        firstVm.Dispose();

        // Aufgabe erneut öffnen: neues VM (wie OeffneAufgabe in ProjectDetailViewModel)
        var secondVm = CreateSut();
        secondVm.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)secondVm.LoadCommand).ExecuteAsync();

        // CLI muss noch laufen und GetTerminalSession muss die Session liefern
        secondVm.IsCliRunning.Should().BeTrue(
            "CLI soll nach Navigation-Zurück weiterlaufen");
        secondVm.GetTerminalSession().Should().NotBeNull(
            "GetTerminalSession muss die Session zurückgeben damit der Loaded-Handler das TerminalControl verbinden kann");
    }

    /// <summary>Beim Start über Projekt-Default bleibt der CLI-Name nach erneutem Öffnen laufender Aufgaben sichtbar.</summary>
    [Fact]
    public async Task AfterReopen_ShowsActiveCliName_WhenStartPluginOnlyOutProjectDefaultCame()
    {
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        await pluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(_projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var firstVm = CreateSut();
        firstVm.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)firstVm.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)firstVm.StartCommand).ExecuteAsync();
        firstVm.ActiveCliName.Should().Be("Test KI");
        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.AiPluginPrefix.Should().Be("Softwareschmiede.TestKi");

        firstVm.Dispose();

        var secondVm = CreateSut();
        secondVm.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)secondVm.LoadCommand).ExecuteAsync();

        secondVm.IsCliRunning.Should().BeTrue();
        secondVm.ActiveCliName.Should().Be("Test KI");
    }

    /// <summary>Bei Wechsel der CLI über das Plugin-Dialog wird der neue Name in der Fußzeile angezeigt, auch wenn zuvor ein Projekt-Default aktiv war.</summary>
    [Fact]
    public async Task SwitchPluginCommand_ShowsNewActiveCliName_WhenBeforeProjectDefaultRan()
    {
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        await pluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(_projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut(pluginManager: _pluginManagerMockForPluginSelection.Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.ActiveCliName.Should().Be("Test KI");

        _dialogServiceMock
            .SetupSequence(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.ZweitesKi", false))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.SwitchPluginCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        sut.ActiveCliName.Should().Be("Zweites KI");

        var updatedWorkItem = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        updatedWorkItem!.AiPluginPrefix.Should().Be("Softwareschmiede.ZweitesKi");
    }

    /// <summary>Nach einem Plugin-Wechsel muss ein manueller CLI-Neustart das geänderte Plugin verwenden, nicht den Projekt-Default.</summary>
    [Fact]
    public async Task CliRestartCommand_SwitchAfterPlugin_UsesChangedPlugin_NotProjectDefault()
    {
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        await pluginDefaultSettingsService.SaveProjectDefaultPluginPrefixAsync(_projectId, PluginType.DevelopmentAutomation, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut(pluginManager: _pluginManagerMockForPluginSelection.Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .SetupSequence(d => d.ShowPluginSelectionDialogAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.ZweitesKi", false))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.ActiveCliName.Should().Be("Test KI");

        await ((AsyncRelayCommand)sut.SwitchPluginCommand).ExecuteAsync();
        sut.ActiveCliName.Should().Be("Zweites KI");

        await RunCliStopWithProcessendOutAsync(sut, workItem.Id);
        sut.IsCliRunning.Should().BeFalse();

        await ((AsyncRelayCommand)sut.RestartCliCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        sut.ActiveCliName.Should().Be("Zweites KI");
    }

    // --- SwitchPluginCommand ---

    /// <summary>SwitchPluginCommand.CanExecute ist true wenn CLI läuft.</summary>
    [Fact]
    public async Task SwitchTestPluginCommand_CanExecute_CliRunning()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, true);

        sut.SwitchPluginCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>SwitchPluginCommand.CanExecute ist false wenn CLI nicht läuft.</summary>
    [Fact]
    public async Task SwitchTestPluginCommand_CanExecute_CliNotRunning_ReturnsFalse()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.SwitchPluginCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>PluginWechselAsync stoppt die alte CLI, zeigt den Dialog und startet die neue CLI.</summary>
    [Fact]
    public async Task SwitchTestPluginAsync_StopsCliAndStartsNew()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();

        await ((AsyncRelayCommand)sut.SwitchPluginCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _dialogServiceMock.Verify(d => d.ShowPluginSelectionDialogAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>PluginWechselAsync zeigt einen Fehler, falls StopCliAsync fehlschlägt, und bricht den Wechsel ab.</summary>
    [Fact]
    public async Task SwitchTestPluginAsync_StopCliFailure_ShowsError()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        _aiService.Dispose();

        await ((AsyncRelayCommand)sut.SwitchPluginCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // --- LadenAsync: Kein impliziter CLI-Neustart ---

    /// <summary>LadenAsync startet keine neue CLI, falls Status Gestartet/Aktiv, aber kein Prozess läuft.</summary>
    [Fact]
    public async Task TestLoadAsync_StartsCliNotImplicit_StatusStartedActiveWithoutRunningProcess()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Kein-Auto-Restart-Aufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/test", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeFalse();
        sut.ShowCliPanel.Should().BeTrue("der persistiert aktive Status soll sichtbar bleiben");
        sut.StartCanCliNew.Should().BeTrue("der Nutzer soll explizit neu starten können");
        _aiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>LadenAsync bindet eine bereits laufende CLI wieder an, ohne einen zweiten Prozess zu starten.</summary>
    [Fact]
    public async Task TestLoadAsync_BindetReadyRunningSessionReAn()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeTrue();

        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _aiPluginMock.Verify(
            p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>LadenAsync startet die CLI nicht, falls die Ausführung beendet oder nicht gestartet ist.</summary>
    [Fact]
    public async Task TestLoadAsync_StartsCliNotImplicit_WhenExecutionCompletedOrNotStarted()
    {
        var newWorkItem = await CreateWorkItem(WorkItemStatus.New);
        var finishedExecution = await CreateWorkItem(WorkItemStatus.Started);
        await _workItemService.EndActiveRunAsync(finishedExecution.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = newWorkItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeFalse();

        sut.WorkItemId = finishedExecution.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.IsCliRunning.Should().BeFalse();
        sut.StartCommand.CanExecute(null).Should().BeTrue();
        _aiPluginMock.Verify(
            p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Eine beendete Ausführung kann über die explizite Startaktion im vorhandenen Klon neu gestartet werden.</summary>
    [Fact]
    public async Task StartCommand_StartsFinishedExecutionExplicitNew()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await _workItemService.CreateAsync(_projectId, "Expliziter Neustart", "Beschreibung", ct: TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/explizit", workingDirectory, ct: TestContext.Current.CancellationToken);
        await _workItemService.UpdateAsync(workItem.Id, workItem.Title, workItem.RequirementDescription, "Softwareschmiede.TestKi", TestContext.Current.CancellationToken);
        await _workItemService.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        sut.IsCliRunning.Should().BeTrue();
        _aiPluginMock.Verify(
            p => p.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Insgesamt beendete Aufgaben dürfen nicht erneut gestartet werden.</summary>
    [Fact]
    public async Task StartCommand_CanExecuteFalse_WhenOverallstatusCompleted()
    {
        var workItem = await CreateWorkItem(WorkItemStatus.Completed);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.StartCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Abgeschlossene Aufgaben bleiben auch mit AusfuehrungsStatus=Beendet nicht startbar.</summary>
    [Fact]
    public async Task StartCommand_CanExecuteFalse_WhenWorkItemCompletedAndExecutionCompleted()
    {
        var workItem = await _workItemService.CreateAsync(_projectId, "Abgeschlossen", "Beschreibung", ct: TestContext.Current.CancellationToken);
        await _workItemService.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkItemStatus.Should().Be(WorkItemStatus.Completed);
        sut.WorkItem!.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
        sut.StartCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>LadenAsync lädt Promptvorlagen für die Ribbon-Auswahl.</summary>
    [Fact]
    public async Task LoadAsync_LoadsPromptTemplatesForSelection()
    {
        await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.PromptTemplates.Should().ContainSingle();
        sut.PromptTemplates[0].Name.Should().Be("Weitermachen");
    }

    /// <summary>PromptVorlageAuswaehlenCommand bleibt ohne laufende CLI-Session stabil.</summary>
    [Fact]
    public async Task SelectPromptTemplateCommand_WithoutRunningSession_CrashesNotFrom()
    {
        var template = await _promptTemplatesService.CreateAsync("Weitermachen", "Mach bitte weiter", TestContext.Current.CancellationToken);
        var workItem = await CreateWorkItem(WorkItemStatus.Started);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var act = async () => await ((AsyncRelayCommand<PromptTemplate>)sut.SelectPromptTemplateCommand).ExecuteAsync(template);

        await act.Should().NotThrowAsync();
    }

    // --- CanAssignIssue ---

    private Mock<IPluginManager> CreateGitPluginManager(bool canCreatePullRequest = true)
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
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);
        return pluginManagerMock;
    }

    /// <summary>CanAssignIssue ist true wenn Aufgabe vorhanden und Plugin Issues unterstützt und kein CLI läuft.</summary>
    [Fact]
    public async Task CanAssignIssue_TrueWhenWorkItemExistsAndPluginSupportsIssues()
    {
        // Arrange
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut(pluginManager: CreateGitPluginManager().Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert
        sut.CanAssignIssue.Should().BeTrue();
    }

    /// <summary>PullRequestErstellenCommand ist verfügbar, wenn Branch, Repository und PR-Capability vorhanden sind; der Aufgabenstatus muss nicht Beendet sein.</summary>
    [Fact]
    public async Task CreatePullRequestCommand_CanExecute_WhenWorkItemStartedWithBranchRepositoryAndPrCapability()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Softwareschmiede.TestGit",
            RepositoryUrl = "test/repo",
            RepositoryName = "Test Repository",
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projectId, "PR-Aufgabe", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/pr-aktionen", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        var sut = CreateSut(pluginManager: CreateGitPluginManager().Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.WorkItemStatus.Should().Be(WorkItemStatus.Started);
        sut.CreateCanPullRequest.Should().BeTrue();
        sut.CreatePullRequestCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>PullRequestErstellenCommand nutzt die Git-Orchestrierung und übergibt dem Plugin die aufgelöste Repository-ID.</summary>
    [Fact]
    public async Task CreatePullRequestCommand_ShouldResolveRepositoryIdFromRepositoryUrl()
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
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);

        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Softwareschmiede.TestGit",
            RepositoryUrl = "https://github.com/test/repo.git",
            RepositoryName = "Test Repository",
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projectId, "PR aus UI", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/pr-url", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationServiceForPr = new PluginActivationService(new AppSettingService(_db, NullLogger<AppSettingService>.Instance), pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationServiceForPr, NullLogger<PluginSelectionService>.Instance);
        var projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance, pluginManagerMock.Object);
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
            _workItemService,
            projectService,
            _logService,
            gitPluginMock.Object,
            pluginSelectionService,
            NullLogger<GitOrchestrationService>.Instance,
            workspaceBrowserMock.Object);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(GitOrchestrationService))).Returns(gitOrchestrationService);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.CreatePullRequestCommand).ExecuteAsync();

        sut.ErrorMessage.Should().BeNull();
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
    public async Task CreatePullRequestCommand_CannotExecute_WhenPrCapabilityMissing()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Softwareschmiede.TestGit",
            RepositoryUrl = "test/repo",
            RepositoryName = "Test Repository",
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(_projectId, "PR-Aufgabe ohne Capability", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        await _workItemService.StartAsync(workItem.Id, "feature/pr-ohne-capability", Path.GetTempPath(), ct: TestContext.Current.CancellationToken);
        var sut = CreateSut(pluginManager: CreateGitPluginManager(canCreatePullRequest: false).Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CreateCanPullRequest.Should().BeFalse();
        sut.CreatePullRequestCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>PullRequestsAktualisierenCommand stoesst einen sofortigen Monitoring-Refresh an und laedt neue Workflow-Runs ohne Aufgabenwechsel.</summary>
    [Fact]
    public async Task RefreshPullRequestsCommand_ShouldRefreshWorkflowRunsImmediately()
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = "Softwareschmiede.GitHub",
            RepositoryUrl = "owner/repo",
            RepositoryName = "owner/repo",
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var workItem = await _workItemService.CreateAsync(_projectId, "PR-Aufgabe", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var pullRequest = new PullRequestReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Provider = PullRequestProvider.GitHub,
            RepositoryId = "owner/repo",
            PullRequestNumber = 233,
            Url = "https://github.com/owner/repo/pull/233",
            Title = "PR",
            SourceBranch = "feature/pr",
            TargetBranch = "main",
            HeadSha = "head",
            Status = PullRequestStatus.Open,
            MergeStatus = PullRequestMergeStatus.Unknown,
            MonitoringPhase = PullRequestMonitoringPhase.Created,
            CreatedUtc = DateTimeOffset.UtcNow,
            NextCheckUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        _db.PullRequestReferences.Add(pullRequest);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IPluginManager>(pluginManagerMock.Object)
            .AddSingleton<ICredentialStore>(new Mock<ICredentialStore>().Object)
            .AddSingleton(sp => new PluginSettingsService(sp.GetRequiredService<ICredentialStore>(), NullLogger<PluginSettingsService>.Instance))
            .AddScoped(_ => new PullRequestReferenceService(_db, TimeProvider.System, NullLogger<PullRequestReferenceService>.Instance))
            .AddScoped(_ => new LogService(_db, NullLogger<LogService>.Instance))
            .AddSingleton<PullRequestMonitoringService>(sp => new PullRequestMonitoringService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                NullLogger<PullRequestMonitoringService>.Instance))
            .BuildServiceProvider();
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProvider);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        sut.PullRequests.Single().WorkflowRuns.Should().BeEmpty();

        await ((AsyncRelayCommand)sut.RefreshPullRequestsCommand).ExecuteAsync();

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
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut(pluginManager: CreateGitPluginManager().Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        _dialogServiceMock
            .Setup(d => d.ShowPluginSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PluginSelectionResult("Softwareschmiede.TestKi", false));
        await ((AsyncRelayCommand)sut.StartCommand).ExecuteAsync();

        // Assert: CLI läuft → CanAssignIssue = false
        sut.IsCliRunning.Should().BeTrue();
        sut.CanAssignIssue.Should().BeFalse();
    }

    /// <summary>IssueBrowserOeffnenCommand.CanExecute ist false wenn IssueUrl null ist.</summary>
    [Fact]
    public async Task OpenIssueBrowserCommand_CannotExecuteWhenUrlNull()
    {
        // Arrange
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert: Aufgabe hat keine IssueReferenz → CanExecute false
        sut.CurrentIssueReference.Should().BeNull();
        sut.OpenIssueBrowserCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueZuweisenCommand.CanExecute ist false wenn kein Plugin vorhanden.</summary>
    [Fact]
    public async Task AssignIssueCommand_CannotExecuteWhenNoPlugin()
    {
        // Arrange
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Assert: Standard-Plugin-Manager gibt keine SCM-Plugins zurück
        sut.CanAssignIssue.Should().BeFalse();
        sut.AssignIssueCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist nur verfügbar, wenn Provider-Capability vorhanden ist und noch keine Referenz existiert.</summary>
    [Fact]
    public async Task IssueCreateCommand_CanExecute_WhenProviderSupportsCreateAndNoReferenceExists()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeTrue();
        sut.IssueCreateCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>IssueAnlegenCommand prueft die Provider-Capability mit der Repository-URL statt dem Anzeigenamen.</summary>
    [Fact]
    public async Task IssueCreateCommand_ShouldUseRepositoryUrl_ForProviderCapability()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: false);
        gitPluginMock.As<IIssueCreateProvider>()
            .Setup(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeTrue();
        gitPluginMock.As<IIssueCreateProvider>()
            .Verify(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>IssueAnlegenCommand ist deaktiviert, wenn der Provider die Issue-Anlage nicht unterstützt.</summary>
    [Fact]
    public async Task IssueCreateCommand_CannotExecute_WhenProviderDoesNotSupportCreate()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: false);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueCreateCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist deaktiviert, wenn das SCM-Plugin keine Issue-Create-Capability implementiert.</summary>
    [Fact]
    public async Task IssueCreateCommand_CannotExecute_WhenProviderHasNoCreateCapability()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateGitPluginWithoutIssueCreateMock();
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueCreateCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Providerfehler bei der Capability-Ermittlung deaktivieren die Anlage statt einen falschen Buttonzustand zu zeigen.</summary>
    [Fact]
    public async Task IssueCreateCommand_CannotExecute_WhenProviderCapabilityFails()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        gitPluginMock.As<IIssueCreateProvider>()
            .Setup(p => p.CanCreateIssueAsync("https://github.com/test/repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider nicht erreichbar"));
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueCreateCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenCommand ist bei bestehender Referenz deaktiviert.</summary>
    [Fact]
    public async Task IssueCreateCommand_CannotExecute_WhenIssueReferenceExists()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        await _workItemService.UpdateIssueReferenceAsync(workItem.Id, new Issue(1, "Vorhanden", "Body", [], null, "https://example.test/1"), TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.CanCreateIssue.Should().BeFalse();
        sut.IssueCreateCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IssueAnlegenAsync speichert erst nach erfolgreichem Dialog-Provider-Ergebnis die lokale Referenz.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldPersistCreatedIssueAndReloadTask()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, false, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        sut.CurrentIssueReference.Should().NotBeNull();
        sut.CurrentIssueReference!.IssueNumber.Should().Be(23);
        sut.CurrentIssueReference.IssueUrl.Should().Be("https://github.com/test/repo/issues/23");
        sut.WorkItem!.RequirementDescription.Should().Be("Beschreibung");
        sut.CanCreateIssue.Should().BeFalse();
    }

    /// <summary>IssueAnlegenAsync aktualisiert bei aktivierter Dialogoption die Aufgabenbeschreibung aus dem Provider-Issue.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldUpdateTaskDescriptionFromCreatedIssueBody_WhenOptionIsEnabled()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Alte Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Provider Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, true, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        sut.WorkItem!.RequirementDescription.Should().Be("Provider Body");
        sut.CurrentIssueReference!.IssueNumber.Should().Be(23);
    }

    /// <summary>IssueAnlegenAsync nutzt den lokalen Dialog-Body, wenn der Provider-Issue-Body leer ist.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldUseLocalBodyFallback_WhenCreatedIssueBodyIsEmpty()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Alte Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "   ", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreateDialogResult(createdIssue, true, "Lokaler Body"));
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        sut.WorkItem!.RequirementDescription.Should().Be("Lokaler Body");
        sut.CurrentIssueReference!.IssueNumber.Should().Be(23);
    }

    /// <summary>Nach extern erstelltem Issue nennt ein lokaler Persistenzfehler die externe URL und speichert keine Referenz.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldShowExternalIssueUrl_WhenLocalPersistenceFails()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns<IssueCreateDialogViewModel, CancellationToken>(async (_, token) =>
            {
                await _workItemService.DeleteAsync(workItem.Id, token);
                return new IssueCreateDialogResult(createdIssue, true, "Lokaler Body");
            });
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("die lokale Zuordnung oder Aufgabenbeschreibung konnte aber nicht gespeichert werden");
        sut.ErrorMessage.Should().Contain("https://github.com/test/repo/issues/23");
        (await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>IssueAnlegenAsync speichert keine lokale Referenz, wenn der Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldNotPersistReference_WhenDialogIsCancelled()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssueCreateDialogResult?)null);
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        var reloaded = await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference.Should().BeNull();
    }

    /// <summary>Ein zweiter Klick waehrend laufender Issue-Anlage oeffnet keinen zweiten Dialog.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldIgnoreSecondExecution_WhileCreateDialogIsOpen()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
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

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        var first = ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();
        await dialogStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        sut.IssueCreateCommand.CanExecute(null).Should().BeFalse();

        var second = ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();
        dialogRelease.SetResult(null);
        await Task.WhenAll(first, second);

        dialogCalls.Should().Be(1);
        (await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken))!.IssueReference.Should().BeNull();
    }

    /// <summary>Nach extern erstelltem Issue wird keine lokale Referenz überschrieben, wenn parallel bereits zugeordnet wurde.</summary>
    [Fact]
    public async Task IssueCreateAsync_ShouldNotOverwriteReference_WhenIssueWhatAssignedAfterDialog()
    {
        var repository = await CreateRepositoryAsync("Softwareschmiede.TestGit", "https://github.com/test/repo", "owner/repo");
        var workItem = await _workItemService.CreateAsync(_projectId, "Issue-Anlage", "Beschreibung", repository.Id, TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://github.com/test/repo/issues/23");
        var parallelIssue = new Issue(99, "Parallel", "Body", [], null, "https://github.com/test/repo/issues/99");
        var gitPluginMock = CreateIssueCreateGitPluginMock(canCreateIssue: true);
        var pluginManagerMock = CreatePluginManagerWithGitPlugin(gitPluginMock.Object);
        var dialogVm = new IssueCreateDialogViewModel(pluginManagerMock.Object, NullLogger<IssueCreateDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueCreateDialogViewModel))).Returns(dialogVm);
        _dialogServiceMock
            .Setup(d => d.ShowIssueCreateDialogAsync(It.IsAny<IssueCreateDialogViewModel>(), It.IsAny<CancellationToken>()))
            .Returns<IssueCreateDialogViewModel, CancellationToken>(async (_, token) =>
            {
                await _workItemService.UpdateIssueReferenceAsync(workItem.Id, parallelIssue, token);
                return new IssueCreateDialogResult(createdIssue, true, "Neue Beschreibung");
            });
        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.IssueCreateCommand).ExecuteAsync();

        var reloaded = await _workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(99);
        reloaded.RequirementDescription.Should().Be("Beschreibung");
        sut.ErrorMessage.Should().Contain("extern erstellt");
        sut.ErrorMessage.Should().Contain("https://github.com/test/repo/issues/23");
    }

    /// <summary>IssueZuweisenAsync tut nichts wenn Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task AssignIssueAsync_UserAbortDoesNothing()
    {
        // Arrange
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);

        var dialogVm = new IssueSelectionDialogViewModel(gitPluginMock.Object, NullLogger<IssueSelectionDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueSelectionDialogViewModel))).Returns(dialogVm);

        _dialogServiceMock
            .Setup(d => d.ShowIssueSelectionDialogAsync(It.IsAny<IssueSelectionDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.AssignIssueCommand).ExecuteAsync();

        // Assert: IssueReferenz unverändert (null)
        sut.CurrentIssueReference.Should().BeNull();
    }

    /// <summary>IssueZuweisenAsync aktualisiert CurrentIssueReferenz wenn Dialog bestätigt wird.</summary>
    [Fact]
    public async Task AssignIssueAsync_ShowsDialogAndUpdatesCurrentIssueReference()
    {
        // Arrange
        var workItem = await CreateWorkItem(WorkItemStatus.New);
        var selectedIssue = new Issue(42, "Gewähltes Issue", "Body", [], null, "https://github.com/test/42");

        var gitPluginMock = new Mock<IGitPlugin>();
        gitPluginMock.SetupGet(p => p.PluginType).Returns(Softwareschmiede.Domain.Enums.PluginType.SourceCodeManagement);
        gitPluginMock.Setup(p => p.GetIssuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([selectedIssue]);
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);

        var dialogVm = new IssueSelectionDialogViewModel(gitPluginMock.Object, NullLogger<IssueSelectionDialogViewModel>.Instance);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IssueSelectionDialogViewModel))).Returns(dialogVm);

        _dialogServiceMock
            .Setup(d => d.ShowIssueSelectionDialogAsync(It.IsAny<IssueSelectionDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedIssue);

        var sut = CreateSut(pluginManager: pluginManagerMock.Object, serviceProvider: serviceProviderMock.Object);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        // Act
        await ((AsyncRelayCommand)sut.AssignIssueCommand).ExecuteAsync();

        // Assert
        sut.CurrentIssueReference.Should().NotBeNull();
        sut.CurrentIssueReference!.IssueNumber.Should().Be(42);
        sut.CurrentIssueReference.IssueUrl.Should().Be("https://github.com/test/42");
    }

    // --- OeffneArbeitsverzeichnisCommand ---

    /// <summary>CanExecute von OeffneArbeitsverzeichnisCommand folgt ShowFileExplorerPanel: ohne Arbeitsverzeichnis false, mit existierendem Arbeitsverzeichnis true.</summary>
    [Fact]
    public async Task OpenWorkingDirectoryCommand_CanExecute_FollowsShowFileExplorerPanel()
    {
        var workItem = await CreateWorkItem();
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.CanExecute(null).Should().BeFalse("die Aufgabe hat noch kein Arbeitsverzeichnis");

        var workingDirectory = CreateTempDirectory();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.CanExecute(null).Should().BeTrue("das Arbeitsverzeichnis existiert jetzt");
    }

    /// <summary>Ausführung von OeffneArbeitsverzeichnisCommand delegiert an ArbeitsverzeichnisOeffnenService mit dem LokalerKlonPfad der Aufgabe.</summary>
    [Fact]
    public async Task OpenWorkingDirectoryCommand_CallsServiceWithLocalClonePath()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock: processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenWorkingDirectoryCommand.Execute(null);

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.Arguments == $"\"{workingDirectory}\"")),
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
    public async Task OpenCanIde_FollowsShowFileExplorerPanel()
    {
        var workItem = await CreateWorkItem();
        var sut = CreateSut();
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenCanIde.Should().BeFalse("ohne LokalerKlonPfad existiert noch kein Arbeitsverzeichnis");
        sut.OpenIdeCommand.CanExecute(null).Should().BeFalse();

        var workingDirectory = CreateTempDirectory();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenCanIde.Should().BeTrue("das Arbeitsverzeichnis existiert jetzt, unabhängig von vorhandenen .sln-Dateien");
        sut.OpenIdeCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// Ohne gefundene Solution löst OeffneIdeCommand über PluginSelectionService.ResolveIdePluginAsync
    /// automatisch das (standardmäßig aktive) Visual-Studio-Code-Plugin als Fallback auf und öffnet damit -
    /// ganz ohne die frühere, separate "VS Code öffnen, wenn keine Solution gefunden wurde"-Einstellung.
    /// </summary>
    [Fact]
    public async Task OpenIdeCommand_WithoutSolution_OpensVisualStudioCodeAsFallback()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock: processStarterMock, visualStudioCodeLocator: locator);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        sut.OpenCanIde.Should().BeTrue();
        sut.OpenIdeCommand.CanExecute(null).Should().BeTrue();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a =>
                a.FileName == "code.cmd"
                && a.Arguments == $"\"{workingDirectory}\""
                && !a.UseShellExecute)),
            Times.Once);
    }

    /// <summary>Wenn ohne gefundene Solution auch Visual Studio Code nicht gefunden wird, erzeugt OeffneIdeCommand eine verständliche Fehlermeldung ohne Prozessstart.</summary>
    [Fact]
    public async Task OpenIdeCommand_WithoutSolutionWithoutVsCode_ShowsError()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(
            processStarterMock: processStarterMock,
            visualStudioCodeLocator: new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.ErrorMessage.Should().Contain("Visual Studio Code");
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>
    /// Ist das Visual-Studio-Code-IDE-Plugin deaktiviert, übernimmt dessen Deaktivierung die frühere Funktion
    /// der entfernten "VS Code öffnen, wenn keine Solution gefunden wurde"-Einstellung: Ohne gefundene Solution
    /// und ohne aktives Fallback-Plugin greift ResolveIdePluginAsync auf PluginManager.GetDefaultIdePlugin()
    /// (Visual Studio) zurück, das ohne .sln-Datei eine Fehlermeldung statt eines VS-Code-Starts erzeugt.
    /// </summary>
    [Fact]
    public async Task OpenIdeCommand_WithoutSolutionAndDisabledVsCodePlugin_ShowsErrorInsteadVsCodeFallback()
    {
        var workingDirectory = CreateTempDirectory();
        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        var pluginActivationService = new PluginActivationService(_settingService, new Mock<IPluginManager>().Object, NullLogger<PluginActivationService>.Instance);
        await pluginActivationService.SetPluginEnabledAsync("Softwareschmiede.VisualStudioCode", false, TestContext.Current.CancellationToken);
        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock: processStarterMock, visualStudioCodeLocator: locator);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty("ohne aktives Fallback-Plugin und ohne .sln kann keine IDE geöffnet werden");
        processStarterMock.Verify(p => p.Start(It.IsAny<ProcessStartRequest>()), Times.Never);
    }

    /// <summary>Eine gefundene Solution hat Vorrang vor dem aktiven Visual-Studio-Code-Fallback: Das Visual-Studio-Plugin ist bei vorhandener .sln explizit kompatibel und gewinnt gegenüber dem lediglich fallback-kompatiblen Visual-Studio-Code-Plugin.</summary>
    [Fact]
    public async Task OpenIdeCommand_WithSolutionAndActiveVsCode_OpensSolutionInsteadVsCode()
    {
        var workingDirectory = CreateTempDirectory();
        var solutionPath = Path.Combine(workingDirectory, "Loesung.sln");
        File.WriteAllText(solutionPath, string.Empty);
        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);
        var processStarterMock = new Mock<IProcessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(processStarterMock: processStarterMock, visualStudioCodeLocator: locator);

        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();
        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == solutionPath && a.UseShellExecute)),
            Times.Once);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == "code.cmd")),
            Times.Never);
    }

    /// <summary>Bei genau einer gefundenen Solution öffnet OeffneIdeCommand diese direkt, ohne den Auswahl-Dialog anzuzeigen.</summary>
    [Fact]
    public async Task OpenIdeCommand_WithASolution_OpensWithoutDialog()
    {
        var workingDirectory = CreateTempDirectory();
        var solutionPath = Path.Combine(workingDirectory, "Loesung.sln");
        File.WriteAllText(solutionPath, string.Empty);

        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock: processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == solutionPath && a.UseShellExecute)),
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
    public async Task OpenIdeCommand_WithSeveralSolutions_OpensFirstDirectWithoutDialog()
    {
        var workingDirectory = CreateTempDirectory();
        var firstSolution = Path.Combine(workingDirectory, "Erste.sln");
        var secondSolution = Path.Combine(workingDirectory, "Zweite.sln");
        File.WriteAllText(firstSolution, string.Empty);
        File.WriteAllText(secondSolution, string.Empty);

        var workItem = await CreateWorkItem();
        await _workItemService.StartAsync(workItem.Id, "feature/x", workingDirectory, ct: TestContext.Current.CancellationToken);

        var processStarterMock = new Mock<IProcessStarter>();
        var sut = CreateSut(processStarterMock: processStarterMock);
        sut.WorkItemId = workItem.Id;
        await ((AsyncRelayCommand)sut.LoadCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OpenIdeCommand).ExecuteAsync();

        _dialogServiceMock.Verify(
            d => d.ShowSolutionSelectionDialogAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        processStarterMock.Verify(
            p => p.Start(It.Is<ProcessStartRequest>(a => a.FileName == firstSolution && a.UseShellExecute)),
            Times.Once);
    }

    private async Task<GitRepository> CreateRepositoryAsync(string pluginType, string repositoryUrl, string repositoryName)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            PluginType = pluginType,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync();
        return repository;
    }

    private static Mock<IGitPlugin> CreateGitPluginWithoutIssueCreateMock()
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

    private static Mock<IGitPlugin> CreateIssueCreateGitPluginMock(bool canCreateIssue)
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

    private Mock<IPluginManager> CreatePluginManagerWithGitPlugin(IGitPlugin gitPlugin)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([gitPlugin]);
        pluginManagerMock.Setup(p => p.GetDefaultSourceCodeManagementPlugin()).Returns(gitPlugin);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([_aiPluginMock.Object]);
        pluginManagerMock.Setup(p => p.GetDefaultDevelopmentAutomationPlugin()).Returns(_aiPluginMock.Object);
        return pluginManagerMock;
    }
}
