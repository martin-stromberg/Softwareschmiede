using FluentAssertions;
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

/// <summary>Unit-Tests für TaskDetailViewModel.OeffneVisualStudioCodeFallbackAsync(): Nutzung des WorkingDirectoryResolver.</summary>
public sealed class TaskDetailViewModelTests_VisualStudioCode : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly TodoService _todoService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly AppEinstellungService _einstellungService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();
    private readonly TestTempDirectoryFixture _tempDirectoryFixture = new();

    /// <summary>TaskDetailViewModelTests_VisualStudioCode.</summary>
    public TaskDetailViewModelTests_VisualStudioCode()
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

    private string CreateTempDirectory()
        => _tempDirectoryFixture.CreateTempDirectory("tdvm_vscode_tests");

    private TaskDetailViewModel CreateSut(Mock<IProzessStarter>? prozessStarterMock = null, IVisualStudioCodeLocator? visualStudioCodeLocator = null)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);

        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance), pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        var pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);

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
        var (arbeitsverzeichnisOeffnenService, ideOeffnenService) = TaskDetailViewModelTestFactory.CreateVerzeichnisAktionenServices(prozessStarterMock, visualStudioCodeLocator);

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
            new Mock<IServiceProvider>().Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            fileExplorerViewModel,
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            arbeitsverzeichnisOeffnenService,
            ideOeffnenService,
            _einstellungService);
    }

    /// <summary>Legt ein GitRepository (optional mit RepositoryStartKonfiguration) sowie eine damit verknüpfte Aufgabe an.</summary>
    /// <param name="workingDirectoryRelativePath">Relativer Arbeitsverzeichnis-Pfad für die Startkonfiguration, oder null für keine Konfiguration.</param>
    /// <returns>Die neu angelegte, mit dem Repository verknüpfte Aufgabe.</returns>
    private async Task<Aufgabe> ErstelleAufgabeMitRepositoryAsync(string? workingDirectoryRelativePath)
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

    /// <summary>Bei konfiguriertem Arbeitsverzeichnis und aktiviertem VS-Code-Fallback wird VS Code mit dem über WorkingDirectoryResolver aufgelösten Pfad gestartet, nicht mit dem Repository-Root.</summary>
    [Fact]
    public async Task OeffneVisualStudioCodeFallback_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(arbeitsverzeichnis, "backend"));
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync("backend");
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, true);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        var erwarteterPfad = Path.GetFullPath(Path.Combine(arbeitsverzeichnis, "backend"));
        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{erwarteterPfad}\"")),
            Times.Once);
    }

    /// <summary>Ohne RepositoryStartKonfiguration wird der VS-Code-Fallback weiterhin mit dem Repository-Root (LokalerKlonPfad) gestartet.</summary>
    [Fact]
    public async Task OeffneVisualStudioCodeFallback_OhneKonfiguration_RuftServiceMitRepositoryRootAuf()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, true);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(new VisualStudioCodeAvailability(true, "code.cmd"));
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        prozessStarterMock.Verify(
            p => p.Starten(It.Is<ProzessStartAnfrage>(a =>
                a.DateiName == "code.cmd"
                && a.Argumente == $"\"{arbeitsverzeichnis}\"")),
            Times.Once);
    }

    /// <summary>Ist Visual Studio Code nicht verfügbar, wird eine aussagekräftige FehlerMeldung gesetzt und kein Prozess gestartet.</summary>
    [Fact]
    public async Task OeffneVisualStudioCodeFallback_OhneVsCode_ZeigtFehlermeldung()
    {
        var arbeitsverzeichnis = CreateTempDirectory();
        var aufgabe = await ErstelleAufgabeMitRepositoryAsync(null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/x", arbeitsverzeichnis);
        await _einstellungService.SetBoolSettingAsync(AppEinstellungService.OpenVisualStudioCodeWhenNoSolutionFoundKey, true);

        var prozessStarterMock = new Mock<IProzessStarter>();
        var locator = new TestVisualStudioCodeLocator(VisualStudioCodeAvailability.NotAvailable);
        var sut = CreateSut(prozessStarterMock, locator);
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await ((AsyncRelayCommand)sut.OeffneIdeCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Be("Keine Visual-Studio-Solution gefunden und Visual Studio Code wurde nicht gefunden.");
        prozessStarterMock.Verify(p => p.Starten(It.IsAny<ProzessStartAnfrage>()), Times.Never);
    }

    private sealed class TestVisualStudioCodeLocator(VisualStudioCodeAvailability availability) : IVisualStudioCodeLocator
    {
        public VisualStudioCodeAvailability Locate() => availability;
    }
}
