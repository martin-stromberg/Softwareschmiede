using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für den zeitgesteuerten Prompt-Versand von TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests_ZeitgesteuerterPrompt : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly PromptVorlagenService _promptVorlagenService;
    private readonly PromptVorlagenPlatzhalterService _promptVorlagenPlatzhalterService = new();
    private readonly PromptZeitVersandService _promptZeitVersandService;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>TaskDetailViewModelTests_ZeitgesteuerterPrompt.</summary>
    public TaskDetailViewModelTests_ZeitgesteuerterPrompt()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
        _promptZeitVersandService = new PromptZeitVersandService(_kiService, _timeProvider, NullLogger<PromptZeitVersandService>.Instance);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, NullLogger<PluginSelectionService>.Instance);
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

        var fileExplorerViewModel = TaskDetailViewModelTestFactory.CreateStub();

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
            new Mock<IServiceProvider>().Object,
            NullLogger<TaskDetailViewModel>.Instance,
            _timeProvider,
            fileExplorerViewModel);
    }

    private async Task<Aufgabe> ErstelleAufgabe(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
    }

    private static void SetzeIsCliRunning(TaskDetailViewModel sut, bool value)
        => typeof(TaskDetailViewModel)
            .GetField("_isCliRunning", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sut, value);

    /// <summary>ScheduledPromptTargetHours und ScheduledPromptTargetMinutes feuern PropertyChanged und speichern die gesetzten Werte.</summary>
    [Fact]
    public async Task ScheduledPromptTargetHours_Binding_SetztProperty()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var geaenderteProperties = new List<string>();
        sut.PropertyChanged += (_, e) => geaenderteProperties.Add(e.PropertyName ?? string.Empty);

        sut.ScheduledPromptTargetHours = 14;
        sut.ScheduledPromptTargetMinutes = 30;

        sut.ScheduledPromptTargetHours.Should().Be(14);
        sut.ScheduledPromptTargetMinutes.Should().Be(30);
        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.ScheduledPromptTargetHours));
        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.ScheduledPromptTargetMinutes));
    }

    /// <summary>Sind beide Zeitfelder leer, ist KannPromptPlanen false und der Sofortversand über die ComboBox bleibt unverändert möglich.</summary>
    [Fact]
    public async Task SchedulePrompt_LeereFelder_KeinScheduling()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetzeIsCliRunning(sut, true);

        sut.SelectedPromptVorlage = vorlage;

        sut.KannPromptPlanen.Should().BeFalse("beide Zeitfelder sind leer");
        sut.SchedulePromptCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Eine gültige Zeit mit gewählter Vorlage ruft SchedulePromptAsync des Service auf und setzt den Wartestellung-Status.</summary>
    [Fact]
    public async Task SchedulePrompt_GueltigeZeit_RuftServiceAuf()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetzeIsCliRunning(sut, true);

        // Die Produktivlogik liest die aktuelle Zeit über den injizierten TimeProvider, hier den
        // FakeTimeProvider dieser Testklasse mit fest verdrahteter, nie voranschreitender Uhrzeit
        // (siehe _timeProvider-Feld). Damit ist die Zielzeitberechnung unabhängig von der realen
        // Wanduhrzeit deterministisch, insbesondere ohne die vormals nötige Mitternachts-Sonderbehandlung.
        var zielzeit = _timeProvider.GetLocalNow().AddMinutes(2);
        sut.ScheduledPromptTargetHours = zielzeit.Hour;
        sut.ScheduledPromptTargetMinutes = zielzeit.Minute;
        sut.SelectedPromptVorlage = vorlage;

        await ((AsyncRelayCommand)sut.SchedulePromptCommand).ExecuteAsync();

        sut.ScheduledPromptStatus.Should().Be("Prompt in Wartestellung");
        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().NotBeNull(
            "SchedulePromptAsync des Service muss für die Aufgabe einen Eintrag angelegt haben");
        sut.ScheduledPromptTargetHours.Should().BeNull("die Zeitfelder werden nach dem Planen geleert");
        sut.ScheduledPromptTargetMinutes.Should().BeNull("die Zeitfelder werden nach dem Planen geleert");
    }

    /// <summary>Eine ungültige Stunde (z. B. 25) setzt eine Fehlermeldung und plant keinen Versand.</summary>
    [Fact]
    public async Task SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetzeIsCliRunning(sut, true);

        sut.ScheduledPromptTargetHours = 25;
        sut.ScheduledPromptTargetMinutes = 0;
        sut.SelectedPromptVorlage = vorlage;

        await ((AsyncRelayCommand)sut.SchedulePromptCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().Be("Ungültige Stunde (0–23)");
        sut.ScheduledPromptStatus.Should().BeNull();
        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().BeNull();
    }

    /// <summary>Dispose storniert einen für die Aufgabe geplanten Prompt beim Service.</summary>
    [Fact]
    public async Task Dispose_StorniertGeplantePrompts()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await _promptZeitVersandService.SchedulePromptAsync(aufgabe.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().NotBeNull();

        sut.Dispose();

        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().BeNull(
            "Dispose muss den geplanten Prompt für die Aufgabe stornieren");
    }

    /// <summary>Stoppt der CLI-Prozess (OnCliProcessStatusChanged mit Status ungleich Gestartet), muss der geplante Prompt beim Service storniert werden, damit ein verwaister Timer nicht bei CLI-Neustart in eine falsche Session feuert.</summary>
    [Fact]
    public async Task OnCliProcessStatusChanged_CliGestoppt_StorniertGeplantenPrompt()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        await _promptZeitVersandService.SchedulePromptAsync(aufgabe.Id, "Test", _timeProvider.GetUtcNow().AddMinutes(5));
        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().NotBeNull();

        var method = typeof(TaskDetailViewModel).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(sut, new object[] { aufgabe.Id, CliProcessStatus.Gestoppt });

        _promptZeitVersandService.GetScheduledPromptStatus(aufgabe.Id).Should().BeNull(
            "beim CLI-Stopp muss der geplante Prompt der Aufgabe im Service storniert werden");
    }

    /// <summary>KannPromptPlanen hängt auch von IsCliRunning ab; ein Wechsel des CLI-Laufstatus muss daher PropertyChanged für KannPromptPlanen auslösen.</summary>
    [Fact]
    public async Task IsCliRunning_Aendert_LoestKannPromptPlanenPropertyChangedAus()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        var geaenderteProperties = new List<string>();
        sut.PropertyChanged += (_, e) => geaenderteProperties.Add(e.PropertyName ?? string.Empty);

        var method = typeof(TaskDetailViewModel).GetMethod("OnCliProcessStatusChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(sut, new object[] { aufgabe.Id, CliProcessStatus.Gestartet });

        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.KannPromptPlanen));
    }

    /// <summary>KannPromptPlanen hängt auch von SelectedPromptVorlage ab; eine geänderte Vorlagenauswahl muss daher PropertyChanged für KannPromptPlanen auslösen.</summary>
    [Fact]
    public async Task SelectedPromptVorlage_Aendert_LoestKannPromptPlanenPropertyChangedAus()
    {
        var vorlage = await _promptVorlagenService.CreateAsync("Weitermachen", "Mach bitte weiter");
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Gestartet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();
        SetzeIsCliRunning(sut, true);

        var geaenderteProperties = new List<string>();
        sut.PropertyChanged += (_, e) => geaenderteProperties.Add(e.PropertyName ?? string.Empty);

        sut.SelectedPromptVorlage = vorlage;

        geaenderteProperties.Should().Contain(nameof(TaskDetailViewModel.KannPromptPlanen));
    }
}
