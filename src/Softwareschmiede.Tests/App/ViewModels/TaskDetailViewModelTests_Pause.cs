using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für die Pausen-Funktion von TaskDetailViewModel (Issue 151).</summary>
public sealed class TaskDetailViewModelTests_Pause : IDisposable
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
    private readonly AppEinstellungService _appEinstellungService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly AufgabeLaufdatenChangedNotifier _laufdatenChangedNotifier;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_Pause.</summary>
    public TaskDetailViewModelTests_Pause()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _kiService = TestKiAusfuehrungsServiceFactory.Create();
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        _appEinstellungService = new AppEinstellungService(_db, NullLogger<AppEinstellungService>.Instance);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        var pluginActivationService = new PluginActivationService(_appEinstellungService, pluginManagerMock.Object, NullLogger<PluginActivationService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, pluginActivationService, NullLogger<PluginSelectionService>.Instance);
        _promptVorlagenService = new PromptVorlagenService(_db, NullLogger<PromptVorlagenService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        _entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            _pluginSelectionService,
            arbeitsverzeichnisMock.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: _kiService),
            NullLogger<EntwicklungsprozessService>.Instance);

        _dialogServiceMock = new Mock<IDialogService>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(AufgabePausierenDialogViewModel)))
            .Returns(new AufgabePausierenDialogViewModel());
        _laufdatenChangedNotifier = new AufgabeLaufdatenChangedNotifier();

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

    private TaskDetailViewModel CreateSut()
    {
        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetIdePlugins()).Returns([]);

        var autonomAufgabeStartService = TaskDetailViewModelTestFactory.CreateAutonomAufgabeStartService(
            _serviceProviderMock.Object,
            _dialogServiceMock.Object,
            _aufgabeService,
            _db,
            appEinstellungService: _appEinstellungService);

        return new TaskDetailViewModel(
            _aufgabeService,
            _protokollService,
            _kiService,
            _entwicklungsprozessService,
            _pluginSelectionService,
            _promptVorlagenService,
            _promptVorlagenPlatzhalterService,
            _promptZeitVersandService,
            _dialogServiceMock.Object,
            pluginManagerMock.Object,
            _serviceProviderMock.Object,
            NullLogger<TaskDetailViewModel>.Instance,
            TimeProvider.System,
            TaskDetailViewModelTestFactory.CreateStub(),
            new TodoListViewModel(_todoService, NullLogger<TodoListViewModel>.Instance),
            TaskDetailViewModelTestFactory.CreateArbeitsverzeichnisOeffnenService(),
            autonomAufgabeStartService,
            _appEinstellungService,
            Options.Create(new AutonomAufgabenOptions()),
            laufdatenChangedNotifier: _laufdatenChangedNotifier);
    }

    private async Task<TaskDetailViewModel> LadeSutAsync(Aufgabe aufgabe)
    {
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        return sut;
    }

    private async Task<Aufgabe> ErstelleAufgabeAsync(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return (await _aufgabeService.GetByIdAsync(aufgabe.Id))!;
    }

    private async Task<Aufgabe> ErstelleAufgabeMitAusfuehrungsStatusAsync(AufgabeStatus status, AufgabeAusfuehrungsStatus ausfuehrungsStatus)
    {
        var aufgabe = await ErstelleAufgabeAsync(status);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.AusfuehrungsStatus = ausfuehrungsStatus;
        await _db.SaveChangesAsync();
        return tracked;
    }

    private static void SetzeIsCliRunning(TaskDetailViewModel sut, bool value)
        => typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, value);

    /// <summary>KannPausieren ist für die pausierbaren Stati Neu, Gestartet und Wartend true.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Neu)]
    [InlineData(AufgabeStatus.Gestartet)]
    [InlineData(AufgabeStatus.Wartend)]
    public async Task KannPausieren_ShouldBeTrue_FuerPausierbareStati(AufgabeStatus status)
    {
        var aufgabe = await ErstelleAufgabeAsync(status);
        var sut = await LadeSutAsync(aufgabe);

        sut.KannPausieren.Should().BeTrue();
        sut.PauseEinstellenCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>KannPausieren ist für beendete Aufgaben false.</summary>
    [Fact]
    public async Task KannPausieren_ShouldBeFalse_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Beendet);
        var sut = await LadeSutAsync(aufgabe);

        sut.KannPausieren.Should().BeFalse();
        sut.PauseEinstellenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>IstPausiert und PauseAnzeigeText spiegeln den geladenen PausiertBisUtc-Wert wider.</summary>
    [Fact]
    public async Task IstPausiert_UndPauseAnzeigeText_ReflektierenGeladenenWert()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _aufgabeService.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(2));
        var sut = await LadeSutAsync(aufgabe);

        sut.IstPausiert.Should().BeTrue();
        sut.PausiertBisUtc.Should().NotBeNull();
        sut.PauseAnzeigeText.Should().StartWith("⏸ Pausiert bis ");
    }

    /// <summary>Bei aktiver Pause darf der Start-Command nicht ausführbar sein (Issue 151).</summary>
    [Fact]
    public async Task StartenCommand_CanExecuteFalse_WhenAufgabePausiert()
    {
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Neu);
        var sut = await LadeSutAsync(aufgabe);
        sut.StartenCommand.CanExecute(null).Should().BeTrue("ohne Pause ist der Start erlaubt");

        await _aufgabeService.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.StartenCommand.CanExecute(null).Should().BeFalse("eine pausierte Aufgabe darf nicht gestartet werden");
    }

    /// <summary>Bei aktiver Pause darf der CLI-Neustart nicht ausführbar sein, obwohl der Ausführungsstatus ihn sonst erlauben würde (Issue 151).</summary>
    [Fact]
    public async Task KannCliNeuStarten_False_WhenAufgabePausiert()
    {
        var aufgabe = await ErstelleAufgabeMitAusfuehrungsStatusAsync(AufgabeStatus.Gestartet, AufgabeAusfuehrungsStatus.Beendet);
        var sut = await LadeSutAsync(aufgabe);
        sut.KannCliNeuStarten.Should().BeTrue("ohne Pause ist der CLI-Neustart bei beendeter Ausführung erlaubt");

        await _aufgabeService.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannCliNeuStarten.Should().BeFalse("eine pausierte Aufgabe darf nicht neu gestartet werden");
        sut.CliNeustartenCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Bei aktiver Pause sind KannPromptVorlageSenden und KannPromptPlanen false — trotz laufender CLI, vorhandener Vorlagen und ausgefüllter Zeitfelder (Issue 151).</summary>
    [Fact]
    public async Task PromptGuards_False_WhenAufgabePausiert()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = await LadeSutAsync(aufgabe);

        // Vorbedingung: Ohne Pause sind die Guards bei laufender CLI + Vorlage + Zeitangabe erfüllt.
        SetzeIsCliRunning(sut, true);
        sut.ScheduledPromptTargetMinutes = 30;
        sut.SelectedPromptVorlage = vorlage;
        sut.KannPromptVorlageSenden.Should().BeTrue();
        sut.KannPromptPlanen.Should().BeTrue();

        await _aufgabeService.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        // LadenAsync setzt IsCliRunning aus dem KiService neu — für die Guard-Prüfung erneut
        // simulieren, ebenso die durch das Neuladen zurückgesetzten Eingaben.
        SetzeIsCliRunning(sut, true);
        sut.ScheduledPromptTargetMinutes = 30;
        sut.SelectedPromptVorlage = vorlage;

        sut.KannPromptVorlageSenden.Should().BeFalse("bei aktiver Pause darf keine Promptvorlage gesendet werden");
        sut.KannPromptPlanen.Should().BeFalse("bei aktiver Pause darf kein Prompt geplant werden");
    }

    /// <summary>PauseEinstellenCommand ruft den Dialog auf, persistiert das Ergebnis via SetPauseAsync und meldet die Laufdaten-Änderung.</summary>
    [Fact]
    public async Task PauseEinstellenCommand_SetztPause_UeberDialogUndService()
    {
        var pausiertBis = DateTimeOffset.UtcNow.AddHours(2);
        _dialogServiceMock
            .Setup(d => d.ShowAufgabePausierenDialogAsync(It.IsAny<AufgabePausierenDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AufgabePausierenErgebnis(pausiertBis, Aufheben: false));
        var notified = new List<Guid>();
        _laufdatenChangedNotifier.LaufdatenChanged += id => notified.Add(id);

        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = await LadeSutAsync(aufgabe);

        await ((AsyncRelayCommand)sut.PauseEinstellenCommand).ExecuteAsync();

        var geladen = await _db.Aufgaben.FindAsync(aufgabe.Id);
        geladen!.PausiertBisUtc.Should().BeCloseTo(pausiertBis, TimeSpan.FromSeconds(5));
        sut.IstPausiert.Should().BeTrue();
        notified.Should().Contain(aufgabe.Id);
        _dialogServiceMock.Verify(
            d => d.ShowAufgabePausierenDialogAsync(It.IsAny<AufgabePausierenDialogViewModel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>PauseEinstellenCommand mit Aufheben-Ergebnis leert PausiertBisUtc.</summary>
    [Fact]
    public async Task PauseEinstellenCommand_HebtPauseAuf_BeiAufhebenErgebnis()
    {
        _dialogServiceMock
            .Setup(d => d.ShowAufgabePausierenDialogAsync(It.IsAny<AufgabePausierenDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AufgabePausierenErgebnis(null, Aufheben: true));

        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        await _aufgabeService.SetPauseAsync(aufgabe.Id, DateTimeOffset.UtcNow.AddHours(1));
        var sut = await LadeSutAsync(aufgabe);
        sut.IstPausiert.Should().BeTrue();

        await ((AsyncRelayCommand)sut.PauseEinstellenCommand).ExecuteAsync();

        var geladen = await _db.Aufgaben.FindAsync(aufgabe.Id);
        geladen!.PausiertBisUtc.Should().BeNull();
        sut.IstPausiert.Should().BeFalse();
        sut.PauseAnzeigeText.Should().BeNull();
    }

    /// <summary>PauseEinstellenCommand ändert nichts, wenn der Dialog abgebrochen wird.</summary>
    [Fact]
    public async Task PauseEinstellenCommand_AendertNichts_BeiAbbruch()
    {
        _dialogServiceMock
            .Setup(d => d.ShowAufgabePausierenDialogAsync(It.IsAny<AufgabePausierenDialogViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AufgabePausierenErgebnis?)null);

        var aufgabe = await ErstelleAufgabeAsync(AufgabeStatus.Gestartet);
        var sut = await LadeSutAsync(aufgabe);

        await ((AsyncRelayCommand)sut.PauseEinstellenCommand).ExecuteAsync();

        (await _db.Aufgaben.FindAsync(aufgabe.Id))!.PausiertBisUtc.Should().BeNull();
    }
}
