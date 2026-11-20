using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

/// <summary>Gemeinsame Setup-Infrastruktur für TaskDetailViewModel-Tests rund um Arbeitsverzeichnis- und IDE-Öffnen-Aktionen.</summary>
public abstract class TaskDetailViewModelTestsBase : IDisposable
{
    /// <summary>_db.</summary>
    protected readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;

    /// <summary>_aufgabeService.</summary>
    protected readonly WorkItemService _workItemService;

    /// <summary>_protokollService.</summary>
    protected readonly LogService _logService;

    /// <summary>_todoService.</summary>
    protected readonly TodoService _todoService;

    /// <summary>_kiService.</summary>
    protected readonly AiExecutionService _kiService;

    /// <summary>_promptVorlagenService.</summary>
    protected readonly PromptTemplateService _promptVorlagenService;

    /// <summary>_promptVorlagenPlatzhalterService.</summary>
    protected readonly PromptTemplatePlaceholderService _promptVorlagenPlatzhalterService = new();

    /// <summary>_promptZeitVersandService.</summary>
    protected readonly PromptScheduleDispatchService _promptScheduleDispatchService;

    /// <summary>_einstellungService.</summary>
    protected readonly AppSettingService _settingService;

    /// <summary>_dialogServiceMock.</summary>
    protected readonly Mock<IDialogService> _dialogServiceMock;

    /// <summary>_projektId.</summary>
    protected readonly Guid _projektId = Guid.NewGuid();

    /// <summary>_tempDirectoryFixture.</summary>
    protected readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>TaskDetailViewModelTestsBase.</summary>
    protected TaskDetailViewModelTestsBase()
    {
        _db = TestDbContextFactory.Create();
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _promptVorlagenService = new PromptTemplateService(_db, NullLogger<PromptTemplateService>.Instance);
        _promptScheduleDispatchService = new PromptScheduleDispatchService(_kiService, TimeProvider.System, NullLogger<PromptScheduleDispatchService>.Instance);
        _settingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        _dialogServiceMock = new Mock<IDialogService>();

        _db.Projects.Add(new Project
        {
            Id = _projektId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
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

    /// <summary>Präfix für über CreateTempDirectory() erzeugte temporäre Verzeichnisse, je Testklasse eindeutig.</summary>
    protected abstract string TempDirectoryPrefix { get; }

    /// <summary>Erzeugt ein neues temporäres Verzeichnis für den aktuellen Test.</summary>
    /// <returns>Der Pfad des neu erzeugten temporären Verzeichnisses.</returns>
    protected string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory(TempDirectoryPrefix);

    /// <summary>Erzeugt ein einsatzbereites TaskDetailViewModel für den Test.</summary>
    /// <param name="prozessStarterMock">Optionaler Mock für IProzessStarter zur Prüfung gestarteter Prozesse.</param>
    /// <param name="visualStudioCodeLocator">Optionaler IVisualStudioCodeLocator zur Steuerung der VS-Code-Verfügbarkeit.</param>
    /// <param name="idePlugins">Optionale Überschreibung der aktiven IDE-Plugins (Standard: Visual Studio + Visual Studio Code).</param>
    /// <param name="serviceProvider">Optionaler IServiceProvider (Standard: leerer Mock ohne Registrierungen).</param>
    /// <returns>Das erzeugte TaskDetailViewModel.</returns>
    protected TaskDetailViewModel CreateSut(
        Mock<IProcessStarter>? prozessStarterMock = null,
        IVisualStudioCodeLocator? visualStudioCodeLocator = null,
        IReadOnlyList<IIdePlugin>? idePlugins = null,
        IServiceProvider? serviceProvider = null)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);

        var visualStudioPlugin = new VisualStudioIdePlugin((prozessStarterMock ?? new Mock<IProcessStarter>()).Object);
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            (prozessStarterMock ?? new Mock<IProcessStarter>()).Object,
            visualStudioCodeLocator ?? new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        var effectiveIdePlugins = idePlugins ?? [visualStudioPlugin, visualStudioCodePlugin];
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns(effectiveIdePlugins);
        pluginManagerMock.Setup(p => p.GetDefaultIdePlugin()).Returns(effectiveIdePlugins[0]);

        var appSettingService = new AppSettingService(_db, NullLogger<AppSettingService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(appSettingService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance, appSettingService);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IWorkingDirectoryResolver>();
        var entwicklungsprozessService = new DevelopmentProcessService(
            _workItemService,
            _logService,
            gitPluginMock.Object,
            pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new DevelopmentProcessServiceOptions(AiExecutionService: _kiService),
            NullLogger<DevelopmentProcessService>.Instance);

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();
        var arbeitsverzeichnisOeffnenService = TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService(prozessStarterMock);
        var serviceProviderObj = serviceProvider ?? new Mock<IServiceProvider>().Object;

        return new TaskDetailViewModel(
            _workItemService,
            _logService,
            _kiService,
            entwicklungsprozessService,
            pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptScheduleDispatchService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProviderObj,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService);
    }

    /// <summary>Legt ein GitRepository (optional mit RepositoryStartKonfiguration) sowie eine damit verknüpfte Aufgabe an.</summary>
    /// <param name="workingDirectoryRelativePath">Relativer Arbeitsverzeichnis-Pfad für die Startkonfiguration, oder null für keine Konfiguration.</param>
    /// <returns>Die neu angelegte, mit dem Repository verknüpfte Aufgabe.</returns>
    protected async Task<WorkItem> ErstelleAufgabeMitRepositoryAsync(string? workingDirectoryRelativePath)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            PluginType = "Softwareschmiede.TestGit",
            RepositoryUrl = "https://example.test/repo.git",
            RepositoryName = "TestRepo",
            Active = true
        };
        _db.GitRepositories.Add(repository);

        if (workingDirectoryRelativePath is not null)
        {
            _db.RepositoryStartConfigurations.Add(new RepositoryStartConfiguration
            {
                Id = Guid.NewGuid(),
                GitRepositoryId = repository.Id,
                WorkingDirectoryRelativePath = workingDirectoryRelativePath,
                Active = true
            });
        }

        await _db.SaveChangesAsync();

        return await _workItemService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung", repository.Id);
    }

    /// <summary>Erstellt einen Test-IdeEntryPoint mit gesetztem DisplayName.</summary>
    /// <param name="path">Der Pfad des Einstiegspunkts.</param>
    /// <param name="displayName">Die anzuzeigende Bezeichnung des Einstiegspunkts.</param>
    /// <returns>Der erzeugte IdeEntryPoint.</returns>
    protected static IdeEntryPoint ErzeugeEntryPointMitDisplayName(string path, string displayName)
        => new(path, displayName);
}
