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

    private static async Task WarteBisAsync(Func<bool> bedingung)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
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
        sut.QuellEintraege[0].Index.Should().Be(0);
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
        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
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
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await WarteBisAsync(() => sut.StatusText == "Wiedergabe beendet.");
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
        sut.WiedergabeStartenCommand.Execute(null);
        sut.WiedergabeNeustartenCommand.Execute(null);
        sut.WiedergabePausierenCommand.Execute(null);

        sut.IstWiedergabeAktiv.Should().BeFalse();
        sut.IstPausiert.Should().BeFalse();
        sut.Session.Should().BeNull();
    }
}
