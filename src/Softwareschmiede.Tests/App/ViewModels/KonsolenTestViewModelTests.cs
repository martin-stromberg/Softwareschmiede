using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.App.ViewModels;

/// <summary>Unit-Tests für <see cref="KonsolenTestViewModel"/> (Konsolentestfenster).</summary>
public sealed class KonsolenTestViewModelTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly Mock<IDialogService> _dialogServiceMock = new();
    private readonly CliReplayAufzeichnungStore _store = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly List<KonsolenTestViewModel> _suts = [];

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        foreach (var sut in _suts)
            sut.Dispose();
        foreach (var pfad in _tempFiles)
            if (File.Exists(pfad))
                File.Delete(pfad);
    }

    private KonsolenTestViewModel CreateSut()
    {
        var sut = new KonsolenTestViewModel(
            _dialogServiceMock.Object,
            _store,
            _timeProvider,
            NullLogger<KonsolenTestViewModel>.Instance,
            dispatcherInvoke: action => action());
        _suts.Add(sut);
        return sut;
    }

    private async Task<string> ErstelleAufzeichnungsDateiAsync(bool istVollstaendig = true, params byte[][] chunkDaten)
    {
        var chunks = (chunkDaten.Length > 0 ? chunkDaten : [Encoding.UTF8.GetBytes("X")])
            .Select((daten, i) => new CliOutputChunkRecord(TimeSpan.Zero, daten))
            .ToArray();
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "TestPlugin",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = istVollstaendig,
            Chunks = chunks,
        });
        _tempFiles.Add(pfad);
        return pfad;
    }

    private void SetupOpenDialog(string? pfad)
    {
        _dialogServiceMock
            .Setup(d => d.ShowOpenFileDialogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pfad);
    }

    private static async Task WarteBisAsync(Func<bool> bedingung, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!bedingung())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "die Bedingung wurde nicht rechtzeitig erfüllt");
            await Task.Delay(10);
        }
    }

    /// <summary>Nach dem Laden einer .clireplay sind Session, Quell-Einträge, Pfad, Status und
    /// Position befüllt — das Fenster ist bereit zur Wiedergabe.</summary>
    [Fact]
    public async Task AufzeichnungOeffnen_LaadtSessionUndQuellEintraege()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("alpha\x1b[31m"), Encoding.UTF8.GetBytes("beta")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.Session.Should().NotBeNull();
        sut.Session.Should().BeOfType<TerminalReplaySession>();
        sut.DateiPfad.Should().Be(pfad);
        sut.QuellEintraege.Should().HaveCount(2);
        sut.QuellEintraege[0].Index.Should().Be(1, "die #-Spalte zählt 1-basiert wie der PositionsText");
        sut.QuellEintraege[1].Index.Should().Be(2);
        sut.QuellEintraege[0].Laenge.Should().Be(10);
        sut.QuellEintraege[0].Quelltext.Should().Be("alpha␛[31m");
        sut.QuellEintraege[1].Quelltext.Should().Be("beta");
        sut.PositionsText.Should().Be("Chunk 0/2");
        sut.StatusText.Should().Contain("geladen");
        sut.FehlerMeldung.Should().BeNull();
        sut.UnvollstaendigHinweis.Should().BeNull();
        sut.IstWiedergabeAktiv.Should().BeFalse();
    }

    /// <summary>Eine als unvollständig markierte Aufzeichnung erzeugt den Hinweis.</summary>
    [Fact]
    public async Task AufzeichnungOeffnen_UnvollstaendigeAufzeichnung_ZeigtHinweis()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(istVollstaendig: false);
        SetupOpenDialog(pfad);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.UnvollstaendigHinweis.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Eine korrupte Datei setzt FehlerMeldung und lässt den Zustand unverändert.</summary>
    [Fact]
    public async Task AufzeichnungOeffnen_KorrupteDatei_ZeigtFehlermeldung()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-korrupt-{Guid.NewGuid():N}.clireplay");
        await File.WriteAllBytesAsync(pfad, Encoding.ASCII.GetBytes("kein clireplay"));
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
        sut.Session.Should().BeNull();
    }

    /// <summary>Bricht der Öffnen-Dialog ab, ändert sich nichts am Zustand.</summary>
    [Fact]
    public async Task AufzeichnungOeffnen_DialogAbgebrochen_KeinZustandswechsel()
    {
        SetupOpenDialog(null);
        var sut = CreateSut();

        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.Session.Should().BeNull();
        sut.FehlerMeldung.Should().BeNull();
        sut.StatusText.Should().Be("Keine Aufzeichnung geladen.");
    }

    /// <summary>Die Wiedergabe läuft bis zum Ende durch, synchronisiert PositionsText und
    /// AktuellerQuellEintrag und endet im Status „beendet".</summary>
    [Fact]
    public async Task Wiedergabe_LaeuftBisEnde_UndSynchronisiertQuellAnsicht()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        sut.ZeitrafferSchwelleText = "0";

        sut.WiedergabeStartenCommand.Execute(null);
        // Der transiente Zwischenzustand wird nicht synchron assertiert: mit
        // ZeitrafferSchwelle 0 und synchronem Dispatcher kann die 2-Chunk-Wiedergabe den
        // Assert bereits überholt haben — beide Zustände sind gültig.
        sut.StatusText.Should().BeOneOf("Wiedergabe läuft.", "Wiedergabe beendet.");

        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");

        sut.IstWiedergabeAktiv.Should().BeFalse();
        sut.PositionsText.Should().Be("Chunk 2/2");
        sut.AktuellerQuellEintrag.Should().BeSameAs(sut.QuellEintraege[1],
            "der aktuelle Quell-Eintrag muss dem zuletzt abgespielten Chunk entsprechen");
    }

    /// <summary>Der Pausieren-Toggle hält die Wiedergabe an und setzt sie fort — auch über den
    /// Zeitraffer-Ablauf der zweiten Chunk-Pause hinaus bleibt die Wiedergabe gestoppt.</summary>
    [Fact]
    public async Task Wiedergabe_PausierenUndFortsetzen()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-pause-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("A")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(5), Encoding.UTF8.GetBytes("B")),
            ],
        });
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.WiedergabeStartenCommand.Execute(null);
        var replay = (TerminalReplaySession)sut.Session!;
        await WarteBisAsync(() => replay.AktuellerChunkIndex >= 1);

        sut.WiedergabePausierenCommand.Execute(null);
        sut.IstPausiert.Should().BeTrue();
        sut.StatusText.Should().Be("Pausiert.");

        // Die reale Pause des zweiten Chunks (5 min → auf 1 s Schwelle verkürzt) läuft im
        // Pausiert-Zustand ab, ohne dass der Chunk angewendet wird.
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(150);
        replay.AktuellerChunkIndex.Should().Be(1,
            "während der Pausierung darf kein weiterer Chunk angewendet werden");

        sut.WiedergabePausierenCommand.Execute(null);
        sut.IstPausiert.Should().BeFalse();
        // Die Pause des zweiten Chunks wird nach dem Fortsetzen ggf. erst neu auf der Schleife
        // angelegt — wiederholte Advances decken die Registrierungsreihenfolge ab. Großzügigere
        // Deadline: das Warten hängt an realer Thread-Pool-/Dispatcher-Einplanung (die logische
        // Zeit ist per FakeTimeProvider sofort), unter Last können 5 s knapp werden — der Test
        // prüft Liveness, nicht Reaktionszeit.
        await WarteBisAsync(() =>
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(10));
            return sut.StatusText == "Wiedergabe beendet.";
        }, TimeSpan.FromSeconds(15));
        replay.AktuellerChunkIndex.Should().Be(2);
    }

    /// <summary>Nach einem beendeten Durchlauf startet „Abspielen" die Aufzeichnung echt erneut
    /// ab Position 0 — eine frische Replay-Session ersetzt die abgelaufene (kein Dead-Zustand).</summary>
    [Fact]
    public async Task Wiedergabe_NachEnde_StartetErneutAbPosition0()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        sut.ZeitrafferSchwelleText = "0";
        var ersteSession = (TerminalReplaySession)sut.Session!;

        sut.WiedergabeStartenCommand.Execute(null);
        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");

        sut.WiedergabeStartenCommand.CanExecute(null).Should().BeTrue(
            "Abspielen muss nach einem beendeten Durchlauf wieder aktivierbar sein");
        sut.WiedergabeStartenCommand.Execute(null);

        var zweiteSession = (TerminalReplaySession)sut.Session!;
        zweiteSession.Should().NotBeSameAs(ersteSession,
            "ein erneutes Abspielen erzeugt eine frische Session ab Position 0");

        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
        zweiteSession.AktuellerChunkIndex.Should().Be(2,
            "die zweite Wiedergabe muss die Aufzeichnung tatsächlich nochmals durchlaufen");
        sut.AktuellerQuellEintrag.Should().BeSameAs(sut.QuellEintraege[1]);
    }

    /// <summary>Ungültige Zeitraffer-Eingaben setzen die Fehlermeldung und lassen die zuletzt
    /// gültige Schwelle unverändert; gültige Eingaben wirken live auf die Session.</summary>
    [Fact]
    public async Task ZeitrafferSchwelleText_ValidiertUndWirktLive()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync();
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var session = (TerminalReplaySession)sut.Session!;

        session.ZeitrafferSchwelle.Should().Be(TimeSpan.FromSeconds(1), "Default-Text '1' wird beim Laden angewendet");

        sut.ZeitrafferSchwelleText = "0,25";
        session.ZeitrafferSchwelle.Should().Be(TimeSpan.FromMilliseconds(250));
        sut.FehlerMeldung.Should().BeNull();

        sut.ZeitrafferSchwelleText = "keine-zahl";
        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
        session.ZeitrafferSchwelle.Should().Be(TimeSpan.FromMilliseconds(250),
            "die letzte gültige Schwelle bleibt bei ungültiger Eingabe aktiv");

        sut.ZeitrafferSchwelleText = "0";
        session.ZeitrafferSchwelle.Should().Be(TimeSpan.Zero);
        sut.FehlerMeldung.Should().BeNull("ein anschließend gültiger Wert räumt die Validierungsmeldung auf");
    }

    /// <summary>Ungültige Werte werden ohne Exception abgewiesen und ändern die aktive
    /// Schwelle nicht — inkl. Werten jenseits des TimeSpan-Bereichs (1e13 s), für die
    /// TimeSpan.FromSeconds eine OverflowException werfen würde.</summary>
    [Theory]
    [InlineData("-2")]
    [InlineData("1e13")]
    public async Task ZeitrafferSchwelleText_UngueltigeWerte_WerdenAbgewiesen(string eingabe)
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync();
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var session = (TerminalReplaySession)sut.Session!;

        sut.ZeitrafferSchwelleText = eingabe;

        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();
        session.ZeitrafferSchwelle.Should().Be(TimeSpan.FromSeconds(1));
    }

    /// <summary>Steht im Zeitraffer-Feld ein ungültiger Text, muss eine frisch erzeugte
    /// Replay-Session (erneutes Laden oder Neustart) die zuletzt gültige Schwelle übernehmen
    /// statt still auf ihren Echtzeit-Default zurückzufallen.</summary>
    [Fact]
    public async Task ZeitrafferSchwelleText_Ungueltig_UebernimmtLetzteGueltigeSchwelleAufFrischeSession()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync();
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.ZeitrafferSchwelleText = "0"; // maximale Geschwindigkeit
        sut.ZeitrafferSchwelleText = "ungueltig"; // Fehlerbanner — letzte gültige Schwelle bleibt 0
        sut.FehlerMeldung.Should().NotBeNullOrWhiteSpace();

        // Erneutes Laden erzeugt eine frische Session — sie muss die zuletzt gültige
        // Schwelle übernehmen, nicht ihren Default (TimeSpan.MaxValue = Echtzeit).
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        var session = (TerminalReplaySession)sut.Session!;
        session.ZeitrafferSchwelle.Should().Be(TimeSpan.Zero,
            "die frische Session muss die zuletzt gültige Schwelle übernehmen, nicht den Echtzeit-Default");
    }

    /// <summary>„Neu starten" bricht eine pausierte Wiedergabe ab und spielt dieselbe
    /// Aufzeichnung sofort wieder ab Position 0 ab — eine frische Replay-Session ersetzt
    /// die verworfene.</summary>
    [Fact]
    public async Task Wiedergabe_NeustartAusPausiertemLauf_SpieltErneutAbPosition0()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-neustart-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("A")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(5), Encoding.UTF8.GetBytes("B")),
            ],
        });
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var erste = (TerminalReplaySession)sut.Session!;

        sut.WiedergabeNeustartenCommand.CanExecute(null).Should().BeFalse(
            "ohne laufende Wiedergabe ist der Neustart-Command deaktiviert");

        sut.WiedergabeStartenCommand.Execute(null);
        await WarteBisAsync(() => erste.AktuellerChunkIndex >= 1);
        sut.WiedergabePausierenCommand.Execute(null);
        sut.IstPausiert.Should().BeTrue();

        sut.WiedergabeNeustartenCommand.CanExecute(null).Should().BeTrue(
            "der Neustart-Command muss auch aus dem Pausiert-Zustand erreichbar sein");
        sut.WiedergabeNeustartenCommand.Execute(null);

        var zweite = (TerminalReplaySession)sut.Session!;
        zweite.Should().NotBeSameAs(erste, "der Neustart verwirft die laufende Session");
        sut.IstPausiert.Should().BeFalse();
        sut.IstWiedergabeAktiv.Should().BeTrue();
        sut.StatusText.Should().Be("Wiedergabe läuft.");

        await WarteBisAsync(() => zweite.AktuellerChunkIndex >= 1);
        // Die nächste Inter-Chunk-Pause (5 min → 1-s-Schwelle) wird ggf. erst nach dem
        // Index-Update auf der Schleife angelegt — wiederholte Advances decken die
        // Registrierungsreihenfolge ab.
        await WarteBisAsync(() =>
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(10));
            return sut.StatusText == "Wiedergabe beendet.";
        });
        zweite.AktuellerChunkIndex.Should().Be(2,
            "der Neustart muss die Aufzeichnung tatsächlich nochmals vollständig durchlaufen");
    }

    /// <summary>Der Schließen-Command löst CloseRequested aus und dispost die Replay-Session.</summary>
    [Fact]
    public async Task Schliessen_LoestCloseRequestedAus()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync();
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var closeRequested = false;
        sut.CloseRequested += (_, _) => closeRequested = true;

        sut.SchliessenCommand.Execute(null);

        closeRequested.Should().BeTrue();
        sut.Session.Should().BeNull("nach dem Schließen darf keine disposed Session gebunden bleiben");
        sut.WiedergabeStartenCommand.CanExecute(null).Should().BeFalse(
            "Abspielen darf nach dem Entsorgen der Session nicht mehr aktivierbar sein");
    }

    /// <summary>Wiedergabe-Commands sind ohne geladene Aufzeichnung wirkungslos.</summary>
    [Fact]
    public void Commands_OhneAufzeichnung_SindInert()
    {
        var sut = CreateSut();

        sut.WiedergabeStartenCommand.CanExecute(null).Should().BeFalse();
        sut.WiedergabeNeustartenCommand.CanExecute(null).Should().BeFalse();
        sut.WiedergabePausierenCommand.CanExecute(null).Should().BeFalse();
        sut.SchrittVorCommand.CanExecute(null).Should().BeFalse();
        sut.SchrittZurueckCommand.CanExecute(null).Should().BeFalse();
        sut.WiedergabeStartenCommand.Execute(null);
        sut.WiedergabeNeustartenCommand.Execute(null);
        sut.WiedergabePausierenCommand.Execute(null);
        sut.SchrittVorCommand.Execute(null);
        sut.SchrittZurueckCommand.Execute(null);

        sut.IstWiedergabeAktiv.Should().BeFalse();
        sut.IstPausiert.Should().BeFalse();
        sut.Session.Should().BeNull();
    }

    /// <summary>Die Schritt-Commands synchronisieren PositionsText und Quell-Auswahl über den
    /// bestehenden BufferChanged-Pfad — inkl. leerer Selektion an Position 0 und Schritt-StatusText.</summary>
    [Fact]
    public async Task SchrittVor_SchrittZurueck_SynchronisierenPositionUndQuellAuswahl()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B"), Encoding.UTF8.GetBytes("C")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();

        sut.PositionsText.Should().Be("Chunk 0/3");
        sut.AktuellerQuellEintrag.Should().BeNull();

        sut.SchrittVorCommand.Execute(null);
        sut.SchrittVorCommand.Execute(null);

        sut.PositionsText.Should().Be("Chunk 2/3");
        sut.AktuellerQuellEintrag.Should().BeSameAs(sut.QuellEintraege[1],
            "die Quell-Selektion markiert den zuletzt angewendeten Chunk");
        sut.StatusText.Should().Be("Einzelschritt — Chunk 2/3 angewendet.");

        sut.SchrittZurueckCommand.Execute(null);

        sut.PositionsText.Should().Be("Chunk 1/3");
        sut.AktuellerQuellEintrag.Should().BeSameAs(sut.QuellEintraege[0]);
        sut.StatusText.Should().Be("Schritt zurück — Chunk 2/3 zurückgenommen.",
            "der Rückwärtsschritt benennt den zurückgenommenen Chunk (alte Position = #-Zeile)");

        sut.SchrittZurueckCommand.Execute(null);

        sut.PositionsText.Should().Be("Chunk 0/3");
        sut.AktuellerQuellEintrag.Should().BeNull("an Position 0 ist kein Chunk angewendet");
        sut.StatusText.Should().Be("Schritt zurück — Chunk 1/3 zurückgenommen.");
    }

    /// <summary>Die CanExecute-Logik der Schritt-Commands hängt von geladener Session,
    /// Wiedergabe-Zustand (unpausiert laufend → gesperrt) und Positionsgrenzen ab.</summary>
    [Fact]
    public async Task SchrittCommands_CanExecute_NachZustand()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-schritt-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("A")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(5), Encoding.UTF8.GetBytes("B")),
            ],
        });
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var session = (TerminalReplaySession)sut.Session!;

        // Geladen, Wiedergabe nicht gestartet: Vor aktiv, Zurück an Position 0 inaktiv.
        sut.SchrittVorCommand.CanExecute(null).Should().BeTrue();
        sut.SchrittZurueckCommand.CanExecute(null).Should().BeFalse();

        // Laufend unpausiert (die Schleife parkt im Delay des zweiten Chunks): beide inaktiv.
        sut.WiedergabeStartenCommand.Execute(null);
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        sut.IstWiedergabeAktiv.Should().BeTrue();
        sut.IstPausiert.Should().BeFalse();
        sut.SchrittVorCommand.CanExecute(null).Should().BeFalse(
            "Schritte sind während unpausierter Wiedergabe gesperrt");
        sut.SchrittZurueckCommand.CanExecute(null).Should().BeFalse();

        // Pausiert: beide aktiv.
        sut.WiedergabePausierenCommand.Execute(null);
        sut.SchrittVorCommand.CanExecute(null).Should().BeTrue();
        sut.SchrittZurueckCommand.CanExecute(null).Should().BeTrue();

        // Ende per Schritt erreicht: Vor inaktiv, Zurück aktiv.
        sut.SchrittVorCommand.Execute(null);
        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
        sut.SchrittVorCommand.CanExecute(null).Should().BeFalse("am Ende ist kein Vorwärtsschritt mehr möglich");
        sut.SchrittZurueckCommand.CanExecute(null).Should().BeTrue("auch im beendeten Zustand darf zurückgeschritten werden");

        // Zurück vom Ende: Vor wieder aktiv.
        sut.SchrittZurueckCommand.Execute(null);
        sut.SchrittVorCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>Programmatische <c>Execute</c>-Aufrufe der Schritt-Commands während
    /// unpausierter Wiedergabe sind No-Ops: <see cref="RelayCommand.Execute"/> wertet
    /// CanExecute nicht aus — der interne Guard verhindert die Positions-Mutation
    /// mitten im laufenden Durchlauf.</summary>
    [Fact]
    public async Task SchrittCommands_Execute_WaehrendLaufenderWiedergabe_SindNoOp()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-schrittguard-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("A")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(5), Encoding.UTF8.GetBytes("B")),
            ],
        });
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var session = (TerminalReplaySession)sut.Session!;

        // Laufend unpausiert: die Schleife parkt im Delay des zweiten Chunks.
        sut.WiedergabeStartenCommand.Execute(null);
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        sut.IstWiedergabeAktiv.Should().BeTrue();
        sut.IstPausiert.Should().BeFalse();

        sut.SchrittVorCommand.Execute(null);
        sut.SchrittZurueckCommand.Execute(null);

        session.AktuellerChunkIndex.Should().Be(1,
            "programmatische Schritte während unpausierter Wiedergabe dürfen die Position nicht verändern");
        sut.PositionsText.Should().Be("Chunk 1/2");
        sut.StatusText.Should().Be("Wiedergabe läuft.");
    }

    /// <summary>„Neu starten" ist auch aus dem reinen Schrittmodus (Position &gt; 0 ohne
    /// gestartete Wiedergabe) erreichbar und spielt die Aufzeichnung ab Position 0 ab —
    /// der direkte Rückweg zum Anfang ohne dutzende Rückwärtsschritte oder Neuladen.</summary>
    [Fact]
    public async Task WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B"), Encoding.UTF8.GetBytes("C")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        sut.ZeitrafferSchwelleText = "0";
        var ersteSession = (TerminalReplaySession)sut.Session!;

        sut.WiedergabeNeustartenCommand.CanExecute(null).Should().BeFalse(
            "an Position 0 ohne laufende Wiedergabe ist der Neustart-Command deaktiviert");

        sut.SchrittVorCommand.Execute(null);
        sut.SchrittVorCommand.Execute(null);
        sut.PositionsText.Should().Be("Chunk 2/3");
        sut.IstWiedergabeAktiv.Should().BeFalse("reines Schreiten startet keine Wiedergabe");

        sut.WiedergabeNeustartenCommand.CanExecute(null).Should().BeTrue(
            "aus dem Schrittmodus muss der direkte Rückweg zu Position 0 erreichbar sein");
        sut.WiedergabeNeustartenCommand.Execute(null);

        var zweiteSession = (TerminalReplaySession)sut.Session!;
        zweiteSession.Should().NotBeSameAs(ersteSession,
            "der Neustart verwirft die geschrittene Session zugunsten einer frischen ab Position 0");
        sut.IstWiedergabeAktiv.Should().BeTrue();
        sut.StatusText.Should().Be("Wiedergabe läuft.");

        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
        zweiteSession.AktuellerChunkIndex.Should().Be(3,
            "der Neustart muss die Aufzeichnung tatsächlich von vorn durchlaufen");
    }

    /// <summary>Reines Schreiten bis ans Ende endet im Status „Wiedergabe beendet."; ein
    /// anschließender Rückwärtsschritt löscht den Beendet-Zustand, sodass „Abspielen" an der
    /// Schrittposition fortfährt (dieselbe Session, kein Ersatz).</summary>
    [Fact]
    public async Task SchrittVor_BisEnde_ZeigtStatusBeendet()
    {
        var pfad = await ErstelleAufzeichnungsDateiAsync(
            chunkDaten: [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B"), Encoding.UTF8.GetBytes("C")]);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        sut.ZeitrafferSchwelleText = "0";
        var session = (TerminalReplaySession)sut.Session!;

        sut.SchrittVorCommand.Execute(null);
        sut.SchrittVorCommand.Execute(null);
        sut.SchrittVorCommand.Execute(null);

        sut.StatusText.Should().Be("Wiedergabe beendet.",
            "der letzte Schritt feuert Exited — der OnReplayExited-Text gilt, nicht der Schritt-Hinweis");
        sut.PositionsText.Should().Be("Chunk 3/3");

        sut.SchrittZurueckCommand.Execute(null);
        sut.PositionsText.Should().Be("Chunk 2/3");

        sut.WiedergabeStartenCommand.Execute(null);

        sut.Session.Should().BeSameAs(session,
            "nach einem Rückwärtsschritt aus dem Beendet-Zustand darf keine frische Session erzeugt werden");
        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
        session.AktuellerChunkIndex.Should().Be(3,
            "die Wiedergabe setzt an der Schrittposition fort und wendet nur den fehlenden Chunk an");
    }

    /// <summary>Kernzusammenspiel auf ViewModel-Ebene: pausierte Wiedergabe → Einzelschritte →
    /// Fortsetzen setzt an der durch die Schritte veränderten Position fort bis zum Ende.</summary>
    [Fact]
    public async Task Pausiert_Schritt_Fortsetzen_ViewModelEbene()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"konsolentest-schrittpause-{Guid.NewGuid():N}.clireplay");
        await _store.SpeichernAsync(pfad, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("A")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(5), Encoding.UTF8.GetBytes("B")),
                new CliOutputChunkRecord(TimeSpan.FromMinutes(10), Encoding.UTF8.GetBytes("C")),
            ],
        });
        _tempFiles.Add(pfad);
        SetupOpenDialog(pfad);
        var sut = CreateSut();
        await ((AsyncRelayCommand)sut.AufzeichnungOeffnenCommand).ExecuteAsync();
        var session = (TerminalReplaySession)sut.Session!;

        sut.WiedergabeStartenCommand.Execute(null);
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        sut.WiedergabePausierenCommand.Execute(null);
        sut.IstPausiert.Should().BeTrue();

        // Schritte im Pausiert-Zustand verändern die Position der laufenden Wiedergabe.
        sut.SchrittZurueckCommand.Execute(null);
        sut.PositionsText.Should().Be("Chunk 0/3");
        sut.SchrittVorCommand.Execute(null);
        sut.PositionsText.Should().Be("Chunk 1/3");

        sut.WiedergabePausierenCommand.Execute(null);
        sut.IstPausiert.Should().BeFalse();

        // Die Inter-Chunk-Delays (5 min, auf die 1-s-Schwelle verkürzt) werden sequenziell und
        // ggf. erst nach dem Fortsetzen neu angelegt — wiederholte Advances decken das ab.
        await WarteBisAsync(() =>
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(10));
            return session.AktuellerChunkIndex >= 2;
        });
        await WarteBisAsync(() =>
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(10));
            return sut.StatusText == "Wiedergabe beendet.";
        });
        session.AktuellerChunkIndex.Should().Be(3);
    }
}
