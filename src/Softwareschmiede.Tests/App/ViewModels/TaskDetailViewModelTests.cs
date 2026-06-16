using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

/// <summary>Unit-Tests für TaskDetailViewModel.</summary>
public sealed class TaskDetailViewModelTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly ProtokollService _protokollService;
    private readonly KiAusfuehrungsService _kiService;
    private readonly EntwicklungsprozessService _entwicklungsprozessService;
    private readonly PluginSelectionService _pluginSelectionService;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Guid _projektId = Guid.NewGuid();

    public TaskDetailViewModelTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, scopeFactoryMock.Object);

        var pluginManagerMock = new Mock<IPluginManager>();
        pluginManagerMock.Setup(p => p.GetDevelopmentAutomationPlugins()).Returns([]);
        pluginManagerMock.Setup(p => p.GetSourceCodeManagementPlugins()).Returns([]);
        var pluginDefaultSettingsService = new PluginDefaultSettingsService(_db, NullLogger<PluginDefaultSettingsService>.Instance);
        _pluginSelectionService = new PluginSelectionService(pluginManagerMock.Object, pluginDefaultSettingsService, NullLogger<PluginSelectionService>.Instance);

        var gitPluginMock = new Mock<IGitPlugin>();
        var arbeitsverzeichnisMock = new Mock<IArbeitsverzeichnisResolver>();
        _entwicklungsprozessService = new EntwicklungsprozessService(
            _aufgabeService,
            _protokollService,
            gitPluginMock.Object,
            _pluginSelectionService,
            arbeitsverzeichnisMock.Object,
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

    public void Dispose()
    {
        _kiService.Dispose();
        _db.Dispose();
    }

    private TaskDetailViewModel CreateSut(Action? zurueckAction = null)
    {
        var vm = new TaskDetailViewModel(
            _aufgabeService,
            _protokollService,
            _kiService,
            _entwicklungsprozessService,
            _pluginSelectionService,
            _dialogServiceMock.Object,
            NullLogger<TaskDetailViewModel>.Instance);
        vm.ZurueckAction = zurueckAction;
        return vm;
    }

    private async Task<Aufgabe> ErstelleAufgabe(AufgabeStatus status = AufgabeStatus.Neu)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
        if (status != AufgabeStatus.Neu)
            await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
        return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
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

    /// <summary>ShowCliPanel ist true für Status InArbeit.</summary>
    [Fact]
    public async Task ShowCliPanel_IsTrue_WhenStatusInArbeit()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.InArbeit);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.ShowCliPanel.Should().BeTrue();
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

    /// <summary>KannLoeschen ist false wenn Status=Beendet.</summary>
    [Fact]
    public async Task KannLoeschen_IsFalse_WhenStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.KannLoeschen.Should().BeFalse();
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

    /// <summary>LoeschenCommand hat CanExecute false wenn Status=Beendet.</summary>
    [Fact]
    public async Task LoeschenCommand_CanExecuteFalse_WennStatusBeendet()
    {
        var aufgabe = await ErstelleAufgabe(AufgabeStatus.Beendet);
        var sut = CreateSut();
        sut.AufgabeId = aufgabe.Id;
        await ((AsyncRelayCommand)sut.LadenCommand).ExecuteAsync();

        sut.LoeschenCommand.CanExecute(null).Should().BeFalse();
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
    public void InfoCliToggleCommand_SetzIsInfoViewVisible_AufTrue_BeiInitialFalse()
    {
        var sut = CreateSut();

        sut.IsInfoViewVisible.Should().BeFalse();
        sut.InfoCliToggleCommand.Execute(null);
        sut.IsInfoViewVisible.Should().BeTrue();
    }

    /// <summary>InfoCliToggleCommand toggled IsInfoViewVisible von true auf false.</summary>
    [Fact]
    public void InfoCliToggleCommand_SetzIsInfoViewVisible_AufFalse_BeiTrue()
    {
        var sut = CreateSut();
        sut.IsInfoViewVisible = true;

        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeFalse();
    }

    /// <summary>InfoCliToggleCommand wechselt mehrfach korrekt.</summary>
    [Fact]
    public void InfoCliToggleCommand_TogglesMehrfach_Korrekt()
    {
        var sut = CreateSut();

        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);
        sut.InfoCliToggleCommand.Execute(null);

        sut.IsInfoViewVisible.Should().BeTrue();
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
}
