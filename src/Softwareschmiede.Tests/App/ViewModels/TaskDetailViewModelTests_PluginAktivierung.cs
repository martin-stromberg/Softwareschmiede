using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für das Single-Plugin-Verhalten von TaskDetailViewModel (Issue #174).</summary>
public sealed class TaskDetailViewModelTests_PluginAktivierung : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly AppEinstellungService _einstellungService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_PluginAktivierung.</summary>
    public TaskDetailViewModelTests_PluginAktivierung()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
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
    }

    private TaskDetailViewModel CreateSut(params IKiPlugin[] kiPlugins)
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns(kiPlugins);

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
        var (arbeitsverzeichnisOeffnenService, ideOeffnenService) = TaskDetailViewModelTestFactory.CreateVerzeichnisAktionenServices();

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
            arbeitsverzeichnisOeffnenService,
            ideOeffnenService,
            _einstellungService);
    }

    private async Task<Aufgabe> ErstelleAufgabe()
        => await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");

    private static Mock<IKiPlugin> CreateKiPluginMock(string name, string prefix)
    {
        var mock = new Mock<IKiPlugin>();
        mock.SetupGet(p => p.PluginName).Returns(name);
        mock.SetupGet(p => p.PluginPrefix).Returns(prefix);
        mock.SetupGet(p => p.PluginType).Returns(PluginType.DevelopmentAutomation);
        mock.Setup(p => p.GetSettingGroups()).Returns([]);
        return mock;
    }

    /// <summary>Bei genau einem aktiven KI-Plugin wird der Selector versteckt (ZeigeKiPluginAuswahl == false).</summary>
    [Fact]
    public async Task LadeVerfuegbarePlugins_VerstecktSelector_BeiEinemAktivenPlugin()
    {
        var einzigesPlugin = CreateKiPluginMock("Einziges KI", "Softwareschmiede.EinzigesKi").Object;
        var aufgabe = await ErstelleAufgabe();
        var sut = CreateSut(einzigesPlugin);
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ZeigeKiPluginAuswahl.Should().BeFalse();
        sut.SelectedKiPluginPrefix.Should().Be("Softwareschmiede.EinzigesKi");
    }

    /// <summary>Bei mehr als einem aktiven KI-Plugin bleibt der Selector sichtbar (ZeigeKiPluginAuswahl == true).</summary>
    [Fact]
    public async Task LadeVerfuegbarePlugins_ZeigtSelector_BeiMehrerenAktivenPlugins()
    {
        var erstesPlugin = CreateKiPluginMock("Erstes KI", "Softwareschmiede.ErstesKi").Object;
        var zweitesPlugin = CreateKiPluginMock("Zweites KI", "Softwareschmiede.ZweitesKi").Object;
        var aufgabe = await ErstelleAufgabe();
        var sut = CreateSut(erstesPlugin, zweitesPlugin);
        sut.AufgabeId = aufgabe.Id;

        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ZeigeKiPluginAuswahl.Should().BeTrue();
    }
}
