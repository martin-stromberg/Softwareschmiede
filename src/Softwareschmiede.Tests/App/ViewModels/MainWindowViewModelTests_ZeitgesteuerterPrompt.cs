using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für MainWindowViewModel im Zusammenhang mit zeitgesteuerten Prompts: Die Seitenleisten-Liste
/// muss den "Prompt in Wartestellung"-Status eines aktiven PromptZeitVersandService-Eintrags übernehmen.</summary>
public sealed class MainWindowViewModelTests_ZeitgesteuerterPrompt : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>MainWindowViewModelTests_ZeitgesteuerterPrompt.</summary>
    public MainWindowViewModelTests_ZeitgesteuerterPrompt()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IPluginManager))).Returns(pluginManagerMock.Object);

        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        var recoveryService = new AufgabeRecoveryService(_db, _runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance);
        var dashboardViewModel = new DashboardViewModel(projektService, _aufgabeService, recoveryService, NullLogger<DashboardViewModel>.Instance);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(DashboardViewModel))).Returns(dashboardViewModel);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
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

    private MainWindowViewModel CreateSut()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        return new MainWindowViewModel(
            darkModeService,
            _serviceProviderMock.Object,
            _aufgabeService,
            _promptZeitVersandService,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            action => action());
    }

    /// <summary>Ist für eine Aufgabe ein zeitgesteuerter Prompt beim PromptZeitVersandService geplant, muss das
    /// entsprechende AktiveAufgabePanelItem in der Seitenleisten-Liste HasScheduledPrompt=true tragen.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_MitGeplantemPrompt_SetztHasScheduledPrompt()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Aufgabe mit geplantem Prompt", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", "/tmp/klon");
        await _promptZeitVersandService.SchedulePromptAsync(aufgabe.Id, "Testprompt", DateTimeOffset.UtcNow.AddMinutes(5));

        var sut = CreateSut();
        await sut.AktiveAufgabenAktualisierenAsync();

        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == aufgabe.Id && a.HasScheduledPrompt);
    }

    /// <summary>Ist für eine Aufgabe kein Prompt geplant, muss HasScheduledPrompt false bleiben.</summary>
    [Fact]
    public async Task AktiveAufgabenAktualisierenAsync_OhneGeplantenPrompt_HasScheduledPromptBleibtFalse()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Aufgabe ohne geplanten Prompt", null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", "/tmp/klon");

        var sut = CreateSut();
        await sut.AktiveAufgabenAktualisierenAsync();

        sut.AktiveAufgabenListe.Should().ContainSingle(a => a.Id == aufgabe.Id && !a.HasScheduledPrompt);
    }
}
