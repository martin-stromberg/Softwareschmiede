using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>
/// Tests der Auto-Shutdown-Oberfläche im <see cref="MainWindowViewModel"/>: fachliche
/// Lauf-Anzahl, Block-Sichtbarkeiten, Persistenz der Nebenwerte (Erzwingen/Toleranz),
/// Eingabevalidierung, Enable-/Disable-Weitergabe und Countdown-Text.
/// </summary>
public sealed class MainWindowViewModelAutoShutdownTests : IDisposable
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly string _einstellungenDbPath =
        Path.Combine(Path.GetTempPath(), $"sw-vm-autoshutdown-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _einstellungenProvider;
    private readonly AutoShutdownEinstellungenService _einstellungenService;
    private readonly TodoService _todoService;
    private readonly AufgabeService _aufgabeService;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IRunningAutomationStatusSource> _runningStatusSourceMock;
    private readonly Mock<IAutoShutdownOrchestrator> _orchestratorMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly Guid _projektId = new Guid("33333333-3333-3333-3333-333333333333");

    /// <summary>MainWindowViewModelAutoShutdownTests.</summary>
    public MainWindowViewModelAutoShutdownTests()
    {
        _db = TestDbContextFactory.Create();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, _todoService);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _runningStatusSourceMock = new Mock<IRunningAutomationStatusSource>();
        _orchestratorMock = new Mock<IAutoShutdownOrchestrator>();
        _timeProvider = new FakeTimeProvider();

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(m => m.GetSourceCodeManagementPlugins()).Returns([]);
        pluginManagerMock.Setup(m => m.GetDevelopmentAutomationPlugins()).Returns([]);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IPluginManager)))
            .Returns(pluginManagerMock.Object);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();

        var projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
        var recoveryService = new AufgabeRecoveryService(_db, _runningStatusSourceMock.Object, NullLogger<AufgabeRecoveryService>.Instance);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(DashboardViewModel)))
            .Returns(new DashboardViewModel(projektService, _aufgabeService, recoveryService, NullLogger<DashboardViewModel>.Instance));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SoftwareschmiededDbContext>(
            o => o.UseSqlite($"Data Source={_einstellungenDbPath};Pooling=False"));
        services.AddScoped<AppEinstellungService>();
        _einstellungenProvider = services.BuildServiceProvider();
        using (var initScope = _einstellungenProvider.CreateScope())
        {
            initScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Database.EnsureCreated();
        }

        _einstellungenService = new AutoShutdownEinstellungenService(
            _einstellungenProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoShutdownEinstellungenService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _einstellungenProvider.Dispose();
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_einstellungenDbPath))
            File.Delete(_einstellungenDbPath);
    }

    private MainWindowViewModel CreateSut(AutoShutdownEinstellungenService? einstellungenService = null)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var darkModeService = new DarkModeService(scopeFactoryMock.Object, NullLogger<DarkModeService>.Instance);
        var kiService = TestKiAusfuehrungsServiceFactory.Create();
        var promptZeitVersandService = new PromptZeitVersandService(kiService, TimeProvider.System, NullLogger<PromptZeitVersandService>.Instance);
        return new MainWindowViewModel(
            darkModeService,
            _serviceProviderMock.Object,
            _aufgabeService,
            _todoService,
            promptZeitVersandService,
            NullLogger<MainWindowViewModel>.Instance,
            _runningStatusSourceMock.Object,
            new MainWindowOptionaleDienste(
                DispatcherInvoke: action => action(),
                AutoShutdownOrchestrator: _orchestratorMock.Object,
                AutoShutdownEinstellungenService: einstellungenService ?? _einstellungenService,
                TimeProvider: _timeProvider));
    }

    private async Task<Softwareschmiede.Domain.Entities.Aufgabe> ErstelleGestarteteAufgabeAsync(string titel)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, titel, null);
        await _aufgabeService.StartenAsync(aufgabe.Id, $"feature/{Guid.NewGuid():N}", "/tmp/klon");
        return aufgabe;
    }

    private async Task<(bool Erzwingen, int ToleranzMinuten)> LeseEinstellungenAsync()
    {
        await using var db = TestDbContextFactory.CreateSqlite($"Data Source={_einstellungenDbPath};Pooling=False");
        var service = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        return (await service.GetAutoShutdownErzwingenAsync(), await service.GetAutoShutdownToleranzMinutenAsync());
    }

    /// <summary>Die fachliche Anzahl zählt nur als „Läuft" klassifizierte Aufgaben.</summary>
    [Fact]
    public async Task LaufendeAutomatisierungenAnzahl_ZaehltNurLaeuftKlassifizierteAufgaben()
    {
        var laufende = await ErstelleGestarteteAufgabeAsync("Laufende Aufgabe");
        var wartende = await ErstelleGestarteteAufgabeAsync("Wartende Aufgabe");
        _runningStatusSourceMock.Setup(s => s.GetLiveLaufStatus(laufende.Id)).Returns(AufgabeLaufStatus.Laeuft);
        _runningStatusSourceMock.Setup(s => s.GetLiveLaufStatus(wartende.Id)).Returns(AufgabeLaufStatus.WartetAufEingabe);
        var sut = CreateSut();

        await sut.AktiveAufgabenAktualisierenAsync();

        sut.LaufendeAutomatisierungenAnzahl.Should().Be(1);
        sut.AutoShutdownUiSichtbar.Should().BeTrue();
        sut.AutoShutdownOptionenSichtbar.Should().BeFalse("Optionen erscheinen erst bei aktiviertem Schalter");
        sut.AutoShutdownCountdownAktiv.Should().BeFalse();
    }

    /// <summary>Ohne laufende Automatisierung und ohne Aktivierung ist der Block nicht sichtbar.</summary>
    [Fact]
    public async Task AutoShutdownUiSichtbar_IstFalse_OhneLaufendeAufgabenUndAktivierung()
    {
        var sut = CreateSut();
        await sut.AktiveAufgabenAktualisierenAsync();

        sut.LaufendeAutomatisierungenAnzahl.Should().Be(0);
        sut.AutoShutdownUiSichtbar.Should().BeFalse();
    }

    /// <summary>Der Aktivierungs-Schalter wird an den Orchestrator weitergegeben und steuert die Optionen.</summary>
    [Fact]
    public void AutoShutdownAktiviert_RuftSetEnabledUndSteuertOptionenSichtbarkeit()
    {
        var sut = CreateSut();

        sut.AutoShutdownAktiviert = true;

        _orchestratorMock.Verify(o => o.SetEnabled(true), Times.Once);
        sut.AutoShutdownOptionenSichtbar.Should().BeTrue();
        sut.AutoShutdownUiSichtbar.Should().BeTrue();

        sut.AutoShutdownAktiviert = false;

        _orchestratorMock.Verify(o => o.SetEnabled(false), Times.Once);
        sut.AutoShutdownOptionenSichtbar.Should().BeFalse();
    }

    /// <summary>
    /// Wartet, bis der im Konstruktor gestartete (nicht awaitbare) Einstellungs-Ladevorgang
    /// sein erstes <c>SetOptionen</c> am Orchestrator-Mock ausgeführt hat.
    /// </summary>
    /// <exception cref="TimeoutException">
    /// Das initiale Laden rief innerhalb von 5s kein <c>SetOptionen</c> auf — ohne diese
    /// Synchronisation wären nachfolgende <c>Times.Never</c>-Verifies nicht aussagekräftig.
    /// </exception>
    private async Task WarteAufInitialesLadenAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (_orchestratorMock.Invocations.Any(i => i.Method.Name == nameof(IAutoShutdownOrchestrator.SetOptionen)))
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException(
            "Das initiale Laden der Auto-Shutdown-Einstellungen rief innerhalb von 5s kein SetOptionen am Orchestrator auf.");
    }

    /// <summary>Gespeicherte Nebenwerte werden beim Start geladen (ohne Write-Back) und an den Orchestrator weitergereicht.</summary>
    [Fact]
    public async Task AutoShutdownEinstellungenLaden_LaedtPersistierteWerteOhneAktivierung()
    {
        await _einstellungenService.SpeichereErzwingenAsync(true);
        await _einstellungenService.SpeichereToleranzMinutenAsync(12);
        var sut = CreateSut();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.AutoShutdownToleranzMinuten != "12")
            await Task.Delay(25);

        sut.AutoShutdownErzwingen.Should().BeTrue();
        sut.AutoShutdownToleranzMinuten.Should().Be("12");
        sut.AutoShutdownAktiviert.Should().BeFalse("die Aktivierung ist Session-Zustand und wird nicht persistiert");
        _orchestratorMock.Verify(o => o.SetOptionen(12, true), Times.Once);
        _orchestratorMock.Verify(o => o.SetEnabled(It.IsAny<bool>()), Times.Never);
    }

    /// <summary>Schlägt das initiale Laden fehl, konsumiert das den Einmal-Guard nicht — ein erneuter Aufruf lädt erfolgreich.</summary>
    [Fact]
    public async Task AutoShutdownEinstellungenLaden_LaedtNachFehlschlagErneut()
    {
        await _einstellungenService.SpeichereToleranzMinutenAsync(9);
        var einmaligFehlschlagenderService = new AutoShutdownEinstellungenService(
            new EinmaligFehlschlagendeScopeFactory(_einstellungenProvider.GetRequiredService<IServiceScopeFactory>()),
            NullLogger<AutoShutdownEinstellungenService>.Instance);
        var sut = CreateSut(einmaligFehlschlagenderService);

        // Der Konstruktor-Lauf scheitert am simulierten Scope-Fehler; der Guard darf ihn
        // nicht als "bereits geladen" verbuchen — der nachfolgende Aufruf muss liefern.
        await sut.AutoShutdownEinstellungenLadenAsync();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && sut.AutoShutdownToleranzMinuten != "9")
            await Task.Delay(25);

        sut.AutoShutdownToleranzMinuten.Should().Be("9");
    }

    /// <summary>Ein gültiger Toleranzwert wird persistiert und an den Orchestrator weitergegeben.</summary>
    [Fact]
    public async Task AutoShutdownToleranzMinuten_GueltigerWert_PersistiertUndReichtWeiter()
    {
        var sut = CreateSut();

        sut.AutoShutdownToleranzMinuten = "7";

        sut.AutoShutdownToleranzFehler.Should().BeNull();
        _orchestratorMock.Verify(o => o.SetOptionen(7, false), Times.Once);
        var (erzwingen, toleranz) = await LeseEinstellungenAsync();
        toleranz.Should().Be(7);
        erzwingen.Should().BeFalse();
    }

    /// <summary>Ungültige Eingaben werden weder persistiert noch an den Orchestrator weitergereicht und setzen den Fehlertext.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1441")]
    [InlineData("5.5")]
    public async Task AutoShutdownToleranzMinuten_UngueltigerWert_SetztFehlerOhneSeiteneffekte(string eingabe)
    {
        var sut = CreateSut();
        await WarteAufInitialesLadenAsync();
        _orchestratorMock.Invocations.Clear();

        sut.AutoShutdownToleranzMinuten = eingabe;

        sut.AutoShutdownToleranzFehler.Should().NotBeNullOrEmpty();
        _orchestratorMock.Verify(o => o.SetOptionen(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>Der Erzwingen-Schalter wird persistiert und zusammen mit der aktuellen Toleranz weitergereicht.</summary>
    [Fact]
    public async Task AutoShutdownErzwingen_PersistiertUndReichtOptionenWeiter()
    {
        var sut = CreateSut();
        await WarteAufInitialesLadenAsync();
        sut.AutoShutdownToleranzMinuten = "3";
        _orchestratorMock.Invocations.Clear();

        sut.AutoShutdownErzwingen = true;

        _orchestratorMock.Verify(o => o.SetOptionen(3, true), Times.Once);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        (bool Erzwingen, int ToleranzMinuten) einstellungen;
        do
        {
            einstellungen = await LeseEinstellungenAsync();
            if (einstellungen.Erzwingen)
                break;
            await Task.Delay(50);
        }
        while (DateTime.UtcNow < deadline);

        einstellungen.Erzwingen.Should().BeTrue();
    }

    /// <summary>Steht ein ungültiger Wert im Toleranzfeld, wird Erzwingen trotzdem mit der zuletzt gültigen Toleranz weitergereicht.</summary>
    [Fact]
    public async Task AutoShutdownErzwingen_BeiUngueltigerToleranz_ReichtLetzteGueltigeToleranzWeiter()
    {
        var sut = CreateSut();
        await WarteAufInitialesLadenAsync();
        sut.AutoShutdownToleranzMinuten = "3";
        sut.AutoShutdownToleranzMinuten = "abc"; // ungültig: wirksam bleibt die letzte gültige Toleranz 3
        _orchestratorMock.Invocations.Clear();

        sut.AutoShutdownErzwingen = true;

        _orchestratorMock.Verify(o => o.SetOptionen(3, true), Times.Once);
    }

    /// <summary>Armiert der Orchestrator einen Countdown, zeigt die UI den Restzeit-Text mit Einheit.</summary>
    [Fact]
    public void ZustandGeaendert_MitCountdownEnde_ZeigtCountdownText()
    {
        var sut = CreateSut();
        _orchestratorMock.SetupGet(o => o.CountdownEndeUtc)
            .Returns(_timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));

        _orchestratorMock.Raise(o => o.ZustandGeaendert += null);

        sut.AutoShutdownCountdownAktiv.Should().BeTrue();
        sut.AutoShutdownCountdownText.Should().Be("Herunterfahren in 5 Min. 0 Sek.");
        sut.AutoShutdownUiSichtbar.Should().BeTrue();
    }

    /// <summary>Restzeiten über 60 Minuten werden in Stunden und Minuten angezeigt (Toleranz bis 1440 Minuten).</summary>
    [Fact]
    public void ZustandGeaendert_MitCountdownEndeUeberEinerStunde_ZeigtStundenFormat()
    {
        var sut = CreateSut();
        _orchestratorMock.SetupGet(o => o.CountdownEndeUtc)
            .Returns(_timeProvider.GetUtcNow() + TimeSpan.FromMinutes(80));

        _orchestratorMock.Raise(o => o.ZustandGeaendert += null);

        sut.AutoShutdownCountdownText.Should().Be("Herunterfahren in 1 Std. 20 Min.");
    }

    /// <summary>Entfernt der Orchestrator das Countdown-Ende, verschwinden Text und Aktiv-Markierung.</summary>
    [Fact]
    public void ZustandGeaendert_OhneCountdownEnde_LoeschtCountdownText()
    {
        var sut = CreateSut();
        _orchestratorMock.SetupGet(o => o.CountdownEndeUtc)
            .Returns(_timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));
        _orchestratorMock.Raise(o => o.ZustandGeaendert += null);

        _orchestratorMock.SetupGet(o => o.CountdownEndeUtc).Returns((DateTimeOffset?)null);
        _orchestratorMock.Raise(o => o.ZustandGeaendert += null);

        sut.AutoShutdownCountdownAktiv.Should().BeFalse();
        sut.AutoShutdownCountdownText.Should().BeNull();
    }

    /// <summary>ScopeFactory, die den ersten <c>CreateScope</c>-Aufruf scheitern lässt und danach an die echte Factory delegiert.</summary>
    private sealed class EinmaligFehlschlagendeScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _verbleibendeFehler = 1;

        public IServiceScope CreateScope()
        {
            if (Interlocked.Decrement(ref _verbleibendeFehler) >= 0)
            {
                throw new InvalidOperationException("Simulierter Ladefehler.");
            }

            return inner.CreateScope();
        }
    }
}
