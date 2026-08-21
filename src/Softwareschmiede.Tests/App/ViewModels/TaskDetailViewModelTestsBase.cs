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
    protected readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;

    /// <summary>_aufgabeService.</summary>
    protected readonly AufgabeService _aufgabeService;

    /// <summary>_protokollService.</summary>
    protected readonly ProtokollService _protokollService;

    /// <summary>_todoService.</summary>
    protected readonly TodoService _todoService;

    /// <summary>_kiService.</summary>
    protected readonly KiAusfuehrungsService _kiService;

    /// <summary>_promptVorlagenService.</summary>
    protected readonly PromptVorlagenService _promptVorlagenService;

    /// <summary>_promptVorlagenPlatzhalterService.</summary>
    protected readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();

    /// <summary>_promptZeitVersandService.</summary>
    protected readonly PromptZeitVersandService _promptZeitVersandService;

    /// <summary>_einstellungService.</summary>
    protected readonly AppEinstellungService _einstellungService;

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
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        _einstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
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
    /// <returns>Das erzeugte TaskDetailViewModel.</returns>
    protected TaskDetailViewModel CreateSut(
        Mock<IProzessStarter>? prozessStarterMock = null,
        IVisualStudioCodeLocator? visualStudioCodeLocator = null,
        IReadOnlyList<IIdePlugin>? idePlugins = null)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);

        var visualStudioPlugin = new VisualStudioIdePlugin((prozessStarterMock ?? new Mock<IProzessStarter>()).Object);
        var visualStudioCodePlugin = new VisualStudioCodeIdePlugin(
            (prozessStarterMock ?? new Mock<IProzessStarter>()).Object,
            visualStudioCodeLocator ?? new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable));
        var effectiveIdePlugins = idePlugins ?? [visualStudioPlugin, visualStudioCodePlugin];
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns(effectiveIdePlugins);
        pluginManagerMock.Setup(p => p.GetDefaultIdePlugin()).Returns(effectiveIdePlugins[0]);

        var appEinstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(appEinstellungService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance, appEinstellungService);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        var entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: _kiService),
            NullLogger<EntwicklungsprozessService>.Instance);

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();
        var arbeitsverzeichnisOeffnenService = TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService(prozessStarterMock);
        var serviceProviderMock = new Mock<IServiceProvider>();
        var autonomAufgabeStartCoordinator = new AutonomAufgabeStartCoordinator(
            serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _aufgabeService,
            NullLogger<AutonomAufgabeStartCoordinator>.Instance);

        return new TaskDetailViewModel(
            _aufgabeService,
            _protokollService,
            _kiService,
            entwicklungsprozessService,
            pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptZeitVersandService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService,
            autonomAufgabeStartCoordinator);
    }

    /// <summary>Legt ein GitRepository (optional mit RepositoryStartKonfiguration) sowie eine damit verknüpfte Aufgabe an.</summary>
    /// <param name="workingDirectoryRelativePath">Relativer Arbeitsverzeichnis-Pfad für die Startkonfiguration, oder null für keine Konfiguration.</param>
    /// <returns>Die neu angelegte, mit dem Repository verknüpfte Aufgabe.</returns>
    protected async Task<Aufgabe> ErstelleAufgabeMitRepositoryAsync(string? workingDirectoryRelativePath)
    {
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = _projektId,
            PluginTyp = "Softwareschmiede.TestGit",
            RepositoryUrl = "https://example.test/repo.git",
            RepositoryName = "TestRepo",
            Aktiv = true
        };
        _db.GitRepositories.Add(repository);

        if (workingDirectoryRelativePath is not null)
        {
            _db.RepositoryStartKonfigurationen.Add(new RepositoryStartKonfiguration
            {
                Id = Guid.NewGuid(),
                GitRepositoryId = repository.Id,
                WorkingDirectoryRelativePath = workingDirectoryRelativePath,
                Aktiv = true
            });
        }

        await _db.SaveChangesAsync();

        return await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung", repository.Id);
    }

    /// <summary>Erstellt einen Test-IdeEntryPoint mit gesetztem DisplayName.</summary>
    /// <param name="path">Der Pfad des Einstiegspunkts.</param>
    /// <param name="displayName">Die anzuzeigende Bezeichnung des Einstiegspunkts.</param>
    /// <returns>Der erzeugte IdeEntryPoint.</returns>
    protected static IdeEntryPoint ErzeugeEntryPointMitDisplayName(string path, string displayName)
        => new(path, displayName);
}
