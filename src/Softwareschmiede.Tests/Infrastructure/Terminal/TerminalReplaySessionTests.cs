using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="TerminalReplaySession"/>: Wiedergabe über den echten
/// Renderpfad (Parser → Buffer), Pause/Fortsetzen, Zeitraffer, Rebuild und ITerminalSession-Stubs.</summary>
public sealed class TerminalReplaySessionTests
{
    /// <summary>Der Empfohlene Test aus dem Plan: dieselbe Chunk-Sequenz durch eine
    /// <see cref="PseudoConsoleSession"/> und die Replay-Session muss denselben Bufferinhalt ergeben.</summary>
    [Fact]
    public async Task Wiedergabe_ErzeugtGleichenBufferWieLiveSession()
    {
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("Hallo "),
            Encoding.UTF8.GetBytes("\x1b[31mrot\x1b[0m"),
            Encoding.UTF8.GetBytes("\r\nzweite Zeile "),
            Encoding.UTF8.GetBytes("\x1b[2J\x1b[Hgelscht!"),
        };

        var optionen = new TerminalSessionOptions { DefaultCols = 80, DefaultRows = 24 };
        using var liveSession = TestPseudoConsoleSessionFactory.Create(
            new MemoryStream(),
            new ChunkedStream(chunks),
            options: optionen);
        await GetReadLoopTask(liveSession).WaitAsync(TimeSpan.FromSeconds(5));
        var liveText = BufferAlsText(liveSession.Buffer);

        var timeProvider = new FakeTimeProvider();
        using var replay = new TerminalReplaySession(
            CreateAufzeichnung(chunks, TimeSpan.Zero),
            timeProvider)
        {
            ZeitrafferSchwelle = TimeSpan.Zero,
        };
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        replay.Exited += (_, _) => beendet.TrySetResult();

        replay.WiedergabeStarten();
        await beendet.Task.WaitAsync(TimeSpan.FromSeconds(5));

        BufferAlsText(replay.Buffer).Should().Be(liveText,
            "die Wiedergabe derselben Roh-Chunks muss denselben Bufferzustand wie die Live-Session ergeben");
        replay.AktuellerChunkIndex.Should().Be(chunks.Length);
    }

    /// <summary>Wiedergabe hält die realen Chunk-Pausen ein (über den TimeProvider) — mit
    /// <see cref="TerminalReplaySession.ZeitrafferSchwelle"/> werden lange Pausen verkürzt.</summary>
    [Fact]
    public async Task Wiedergabe_ZeitrafferVerkuerztRealePausen()
    {
        var timeProvider = new FakeTimeProvider();
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("A"),
            Encoding.UTF8.GetBytes("B"),
        };
        var aufzeichnung = CreateAufzeichnung(chunks, TimeSpan.FromSeconds(30));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider)
        {
            ZeitrafferSchwelle = TimeSpan.FromMilliseconds(50),
        };
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, _) => beendet.TrySetResult();

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);

        // 30 s reale Pause sind durch die 50-ms-Schwelle verkürzt: nach 40 ms noch kein zweiter Chunk.
        timeProvider.Advance(TimeSpan.FromMilliseconds(40));
        await Task.Delay(150);
        session.AktuellerChunkIndex.Should().Be(1);

        // Großzügiger Advance deckt auch einen erst nach dem ersten Advance angelegten Delay-Timer ab.
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await beendet.Task.WaitAsync(TimeSpan.FromSeconds(5));
        session.AktuellerChunkIndex.Should().Be(2);
    }

    /// <summary><see cref="TerminalReplaySession.Pausieren"/> hält die Wiedergabe vor dem nächsten
    /// Chunk an (auch über die bereits abgelaufene Pausenzeit hinaus), <see cref="TerminalReplaySession.Fortsetzen"/>
    /// setzt sie fort.</summary>
    [Fact]
    public async Task Pausieren_StopptChunkAnwendung_FortsetzenSetztFort()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.FromSeconds(5));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, _) => beendet.TrySetResult();

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);

        session.Pausieren();
        session.IstPausiert.Should().BeTrue();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Laeuft, "die Wiedergabe läuft auch pausiert weiter");

        // Auch nach Ablauf der realen Pause darf während der Pausierung kein Chunk angewendet werden —
        // unabhängig davon, ob die Schleife bereits im Delay oder noch am Pause-Gate parkt.
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(150);
        session.AktuellerChunkIndex.Should().Be(1,
            "während der Pausierung darf kein weiterer Chunk angewendet werden");

        session.Fortsetzen();
        session.IstPausiert.Should().BeFalse();
        // Parkte die Schleife vor dem Delay am Gate, wird der Delay erst jetzt angelegt —
        // wiederholte Advances decken die Registrierungsreihenfolge ab.
        await WarteBisAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            return beendet.Task.IsCompleted;
        });
        session.AktuellerChunkIndex.Should().Be(2);
    }

    /// <summary><see cref="TerminalReplaySession.RebuildBufferFromReplay"/> baut den Buffer aus den
    /// bis dahin abgespielten Chunks neu auf (nur das gespielte Präfix).</summary>
    [Fact]
    public async Task RebuildBufferFromReplay_BautBufferAusGespieltemPraefix()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("erste"), Encoding.UTF8.GetBytes("ZWEITE")],
            TimeSpan.FromHours(1));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);

        // Explizit zerstören und neu aufbauen: nur der bereits gespielte Chunk darf erscheinen.
        session.Buffer.Reset();
        session.RebuildBufferFromReplay();

        ZeilenText(session.Buffer, 0).TrimEnd().Should().Be("erste");
        ZeilenText(session.Buffer, 1).TrimEnd().Should().BeEmpty(
            "der noch nicht gespielte zweite Chunk darf im Rebuild nicht auftauchen");
    }

    /// <summary>Am Ende der Aufzeichnung feuert <see cref="ITerminalSession.Exited"/> mit
    /// <c>ExitCode = null</c> und der RuntimeStatus kehrt zu <see cref="CliRuntimeStatus.Inaktiv"/> zurück.</summary>
    [Fact]
    public async Task Wiedergabe_AmEnde_FeuertExitedUndWechseltAufInaktiv()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung([Encoding.UTF8.GetBytes("X")], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var exitedArgs = new TaskCompletionSource<TerminalSessionExitedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, e) => exitedArgs.TrySetResult(e);

        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv, "vor der Wiedergabe ist die Session inaktiv");
        session.WiedergabeStarten();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Laeuft);

        var args = await exitedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        args.ExitCode.Should().BeNull("die Replay-Session hat keinen echten Prozess-Exit-Code");
        session.ExitCode.Should().BeNull();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv, "nach der Wiedergabe ist die Session wieder inaktiv");
    }

    /// <summary><see cref="ITerminalSession.OutputChunk"/> liefert die Rohbytes der Aufzeichnung
    /// (ungeparst, chunkweise, in Reihenfolge).</summary>
    [Fact]
    public async Task Wiedergabe_OutputChunkEvent_LiefertRohbytes()
    {
        var timeProvider = new FakeTimeProvider();
        var roh1 = Encoding.UTF8.GetBytes("RAW\x1b[31m");
        var roh2 = Encoding.UTF8.GetBytes("DATA");
        var aufzeichnung = CreateAufzeichnung([roh1, roh2], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var empfangen = new List<byte[]>();
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OutputChunk += (_, e) => empfangen.Add(e.Data.ToArray());
        session.Exited += (_, _) => beendet.TrySetResult();

        session.WiedergabeStarten();
        await beendet.Task.WaitAsync(TimeSpan.FromSeconds(5));

        empfangen.Should().HaveCount(2);
        empfangen[0].Should().Equal(roh1);
        empfangen[1].Should().Equal(roh2);
    }

    /// <summary>Die <see cref="ITerminalSession"/>-Stubs sind unschädlich: ungestarteter Prozess,
    /// Null-Streams, No-op-Eingaben und immer erfolgreiche Resize-/Drain-Aufrufe. Zusätzlich meldet
    /// die Session über <see cref="ITerminalSession.SupportsResize"/> ihre fixierte Geometrie —
    /// das <c>TerminalControl</c> ruft <c>Resize</c>/<c>Buffer.Resize</c> dann gar nicht erst auf.</summary>
    [Fact]
    public async Task Stubs_SindUnschaedlich()
    {
        using var session = new TerminalReplaySession(
            CreateAufzeichnung([Encoding.UTF8.GetBytes("x")], TimeSpan.Zero),
            new FakeTimeProvider());

        session.IsPseudoTerminal.Should().BeFalse();
        session.Failure.Should().BeNull();
        session.ExitCode.Should().BeNull();
        session.Process.Should().NotBeNull();
        session.Process.StartInfo.FileName.Should().BeEmpty(
            "der Prozess-Stub darf nicht gestartet worden sein");
        session.InputStream.Should().BeSameAs(Stream.Null);
        session.OutputStream.Should().BeSameAs(Stream.Null);

        var act = async () =>
        {
            await session.WriteInputAsync(new byte[] { 0x41 });
            await session.WritePromptAsync("echo x", CancellationToken.None);
            session.MarkInputActivity();
            session.MarkOutputActivity();
        };
        await act.Should().NotThrowAsync();
        session.SupportsResize.Should().BeFalse(
            "die Geometrie einer Replay-Session ist auf die Aufzeichnungs-Header-Werte fixiert");
        session.Resize(10, 10).Should().BeTrue();
        session.Resize(0, -3).Should().BeTrue("Resize ist ein Stub und darf nie fehlschlagen");
        (await session.DrainOutputAsync(TimeSpan.FromSeconds(1))).Should().BeTrue();
        session.RebuildBufferFromReplay(); // darf auf leerem gespieltem Präfix nicht werfen
    }

    /// <summary><see cref="TerminalReplaySession.WiedergabeStarten"/> ist idempotent: ein zweiter
    /// Aufruf startet keinen zweiten Durchlauf.</summary>
    [Fact]
    public async Task WiedergabeStarten_ZweiterAufruf_IstNoOp()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung([Encoding.UTF8.GetBytes("X")], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var exitedCount = 0;
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, _) =>
        {
            Interlocked.Increment(ref exitedCount);
            beendet.TrySetResult();
        };

        session.WiedergabeStarten();
        session.WiedergabeStarten();
        await beendet.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        exitedCount.Should().Be(1, "ein bereits gestarteter Durchlauf darf nicht erneut gestartet werden");
    }

    /// <summary><see cref="TerminalReplaySession.Dispose"/> während der Wiedergabe bricht die
    /// Schleife sauber ab (kein Exited, kein Hängenbleiben).</summary>
    [Fact]
    public async Task Dispose_WaehrendWiedergabe_BrichtAb()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.FromHours(1));
        var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var exited = false;
        session.Exited += (_, _) => exited = true;

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        session.Dispose();
        timeProvider.Advance(TimeSpan.FromHours(2));
        await Task.Delay(150);

        exited.Should().BeFalse("nach Dispose darf kein Exited mehr gefeuert werden");
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv);
    }

    /// <summary>Nach <see cref="TerminalReplaySession.Dispose"/> darf die Wiedergabe-Schleife bei
    /// <c>ZeitrafferSchwelle = 0</c> (alle Delays entfallen und das offene Pause-Gate liefert einen
    /// bereits erfüllten Task, dessen WaitAsync den Token nicht auswertet) nicht alle restlichen
    /// Chunks auf der disposed Session drain-en — der Abbruch muss zwischen den Chunks beachtet werden.</summary>
    [Fact]
    public async Task Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen()
    {
        const int chunkCount = 10;
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            Enumerable.Range(0, chunkCount).Select(i => Encoding.UTF8.GetBytes($"chunk-{i}")).ToArray(),
            TimeSpan.Zero);
        var session = new TerminalReplaySession(aufzeichnung, timeProvider)
        {
            ZeitrafferSchwelle = TimeSpan.Zero,
        };

        // Die Schleife wird mitten im ersten Chunk-Apply blockiert (OutputChunk feuert unter dem
        // Render-Lock): Dispose() trifft sie so garantiert mitten im Lauf bei offenem Gate.
        var ersterChunkErreicht = new ManualResetEventSlim();
        var freigabe = new ManualResetEventSlim();
        var ersterAufruf = true;
        session.OutputChunk += (_, _) =>
        {
            if (ersterAufruf)
            {
                ersterAufruf = false;
                ersterChunkErreicht.Set();
                freigabe.Wait();
            }
        };

        try
        {
            session.WiedergabeStarten();
            ersterChunkErreicht.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue(
                "die Wiedergabe-Schleife muss den ersten Chunk erreicht haben");
            session.Dispose();
        }
        finally
        {
            // Freigabe in jedem Fall — ein fehlschlagender Assert darf den blockierten
            // Schleifen-Thread nicht dauerhaft parken.
            freigabe.Set();
            session.Dispose();
        }

        await Task.Delay(200);
        session.AktuellerChunkIndex.Should().BeLessThan(chunkCount,
            "nach dem Abbruch dürfen die restlichen Chunks nicht mehr angewendet werden");
    }

    /// <summary><see cref="TerminalReplaySession.SchrittVor"/> wendet ohne gestartete Wiedergabe
    /// genau einen Chunk pro Aufruf an — zeitstempel-unabhängig (große Offsets werden ignoriert,
    /// kein Advance nötig); <see cref="ITerminalSession.RuntimeStatus"/> bleibt Inaktiv.</summary>
    [Fact]
    public void SchrittVor_WendetNaechstenChunkZeitunabhaengigAn()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.FromHours(1));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);

        session.SchrittVor().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(1);
        ZeilenText(session.Buffer, 0).TrimEnd().Should().Be("A");

        session.SchrittVor().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(2);
        ZeilenText(session.Buffer, 0).TrimEnd().Should().Be("AB");
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv,
            "reines Schreiten ohne gestartete Wiedergabe bleibt inaktiv");
    }

    /// <summary><see cref="TerminalReplaySession.SchrittVor"/> feuert pro angewendetem Chunk das
    /// <see cref="ITerminalSession.OutputChunk"/>-Event (Rohbytes, in Reihenfolge) sowie
    /// <see cref="ITerminalSession.BufferChanged"/>.</summary>
    [Fact]
    public void SchrittVor_FeuertOutputChunkUndBufferChanged()
    {
        var roh1 = Encoding.UTF8.GetBytes("RAW\x1b[31m");
        var roh2 = Encoding.UTF8.GetBytes("DATA");
        var aufzeichnung = CreateAufzeichnung([roh1, roh2], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        var empfangen = new List<byte[]>();
        var bufferChangedCount = 0;
        session.OutputChunk += (_, e) => empfangen.Add(e.Data.ToArray());
        session.BufferChanged += (_, _) => bufferChangedCount++;

        session.SchrittVor();
        session.SchrittVor();

        empfangen.Should().HaveCount(2);
        empfangen[0].Should().Equal(roh1);
        empfangen[1].Should().Equal(roh2);
        bufferChangedCount.Should().Be(2);
    }

    /// <summary>Wendet <see cref="TerminalReplaySession.SchrittVor"/> den letzten Chunk an, feuert
    /// die Session <see cref="ITerminalSession.Exited"/> (ExitCode null, RuntimeStatus → Inaktiv) —
    /// weitere Schritte sind No-Ops ohne zweites Exited.</summary>
    [Fact]
    public void SchrittVor_AmEnde_FeuertExited_IstDannNoOp()
    {
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        var exitedCount = 0;
        var exitedArgs = new List<TerminalSessionExitedEventArgs>();
        session.Exited += (_, e) =>
        {
            exitedCount++;
            exitedArgs.Add(e);
        };

        session.SchrittVor();
        exitedCount.Should().Be(0, "das Ende ist erst mit dem letzten Chunk erreicht");
        session.SchrittVor().Should().BeTrue();

        exitedCount.Should().Be(1);
        exitedArgs[0].ExitCode.Should().BeNull();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv);

        session.SchrittVor().Should().BeFalse("am Ende ist der Vorwärtsschritt ein No-Op");
        session.SchrittVor().Should().BeFalse();
        exitedCount.Should().Be(1, "Exited darf nicht erneut feuern");
    }

    /// <summary><see cref="TerminalReplaySession.SchrittZurueck"/> baut den Buffer deterministisch
    /// aus dem verbleibenden Präfix neu auf — identisch zum Buffer einer Referenz-Session, die nur
    /// bis zu dieser Position schritt (keine Restwirkung zustandsverändernder Sequenzen).</summary>
    [Fact]
    public void SchrittZurueck_BautPraefixDeterministischNeuAuf()
    {
        var chunk1 = Encoding.UTF8.GetBytes("TEXT");
        var chunk2 = Encoding.UTF8.GetBytes("\x1b[2J\x1b[H");
        var aufzeichnung = CreateAufzeichnung([chunk1, chunk2], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        using var referenz = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        var outputCount = 0;
        session.OutputChunk += (_, _) => outputCount++;

        referenz.SchrittVor();
        session.SchrittVor();
        session.SchrittVor();
        ZeilenText(session.Buffer, 0).TrimEnd().Should().BeEmpty(
            "der zweite Chunk (Bildschirm löschen) wurde angewendet");

        session.SchrittZurueck().Should().BeTrue();

        session.AktuellerChunkIndex.Should().Be(1);
        BufferAlsText(session.Buffer).Should().Be(BufferAlsText(referenz.Buffer),
            "der Rebuild aus dem Präfix muss denselben Buffer ergeben wie ein Schritt-Lauf bis dahin");
        ZeilenText(session.Buffer, 0).TrimEnd().Should().Be("TEXT",
            "die Bildschirm-Löschung des zurückgenommenen Chunks darf keine Restwirkung haben");
        outputCount.Should().Be(2, "der Rückwärtsschritt wendet keinen Chunk an — kein OutputChunk-Event");
    }

    /// <summary>Der Parser-Zustand wird beim Rückwärtsschritt auf den Präfix-Zustand zurückgesetzt:
    /// bei einer über die Chunk-Grenze geteilten Escape-Sequenz ergibt Vor → Zurück → Vor denselben
    /// Buffer wie ein ununterbrochener Durchlauf (statt die Restbytes als Literaltext zu rendern).</summary>
    [Fact]
    public void SchrittZurueck_SetztParserZustandZurueck()
    {
        // "\x1b[31" + "mROT" — die SGR-Sequenz ist über die Chunk-Grenze geteilt.
        var chunk1 = Encoding.UTF8.GetBytes("\x1b[31");
        var chunk2 = Encoding.UTF8.GetBytes("mROT");
        var aufzeichnung = CreateAufzeichnung([chunk1, chunk2], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        using var referenz = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());

        referenz.SchrittVor();
        referenz.SchrittVor();

        session.SchrittVor();
        session.SchrittVor();
        session.SchrittZurueck();
        session.SchrittVor();

        ZeilenText(session.Buffer, 0).TrimEnd().Should().Be("ROT",
            "nach dem Rebuild muss der Parser die geteilte Sequenz korrekt fortsetzen");
        BufferAlsText(session.Buffer).Should().Be(BufferAlsText(referenz.Buffer));
    }

    /// <summary><see cref="TerminalReplaySession.SchrittZurueck"/> an Position 0 ist ein No-Op:
    /// <c>false</c>, kein <see cref="ITerminalSession.BufferChanged"/>, Buffer unverändert.</summary>
    [Fact]
    public void SchrittZurueck_BeiPosition0_IstNoOp()
    {
        var aufzeichnung = CreateAufzeichnung([Encoding.UTF8.GetBytes("A")], TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        var bufferChangedCount = 0;
        session.BufferChanged += (_, _) => bufferChangedCount++;

        session.SchrittZurueck().Should().BeFalse();

        bufferChangedCount.Should().Be(0);
        session.AktuellerChunkIndex.Should().Be(0);
        ZeilenText(session.Buffer, 0).TrimEnd().Should().BeEmpty();
    }

    /// <summary>Kernszenario „pausiert → Schritte → fortsetzen": nach einem Rückwärtsschritt im
    /// Pausiert-Zustand setzt die Wiedergabe-Schleife an der Schrittposition fort, wartet die
    /// aufgezeichnete Pause des zurückgenommenen Chunks erneut ab und wendet ihn erneut an.</summary>
    [Fact]
    public async Task Pausiert_Schritte_Fortsetzen_SetztAnSchrittpositionFort()
    {
        var timeProvider = new FakeTimeProvider();
        var chunkA = Encoding.UTF8.GetBytes("A");
        var chunkB = Encoding.UTF8.GetBytes("B");
        var chunkC = Encoding.UTF8.GetBytes("C");
        var aufzeichnung = CreateAufzeichnung([chunkA, chunkB, chunkC], TimeSpan.FromSeconds(30));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var empfangen = new List<byte[]>();
        var beendet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OutputChunk += (_, e) => empfangen.Add(e.Data.ToArray());
        session.Exited += (_, _) => beendet.TrySetResult();

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        // Der Schleife Zeit geben, im Delay des zweiten Chunks zu parken (30 s auf der
        // Fake-Zeit) — danach ist der Parkpunkt deterministisch.
        await Task.Delay(150);

        session.Pausieren();
        session.SchrittZurueck().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(0);

        session.Fortsetzen();
        // Der bereits angelegte Delay der alten Position läuft erst noch ab — danach erkennt
        // der Positions-Re-Check den Rückwärtsschritt und die Iteration beginnt neu: Chunk A
        // (Offset 0) wird ohne weiteres Delay erneut angewendet.
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        empfangen.Should().HaveCount(2);
        empfangen[0].Should().Equal(chunkA);
        empfangen[1].Should().Equal(chunkA, "der zurückgenommene Chunk wird erneut angewendet");

        // Die Folge-Delays werden sequenziell neu angelegt — wiederholte Advances decken die
        // Registrierungsreihenfolge ab.
        await WarteBisAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(30));
            return session.AktuellerChunkIndex >= 2;
        });
        await WarteBisAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(30));
            return beendet.Task.IsCompleted;
        });

        empfangen.Should().HaveCount(4);
        empfangen[2].Should().Equal(chunkB);
        empfangen[3].Should().Equal(chunkC);
        session.AktuellerChunkIndex.Should().Be(3);
    }

    /// <summary>Erreicht <see cref="TerminalReplaySession.SchrittVor"/> bei einer pausiert parkenden
    /// Wiedergabe-Schleife das Ende, feuert die Session <see cref="ITerminalSession.Exited"/> und die
    /// Schleife terminiert sauber — das Lauf-Flag ist danach frei (Re-Armierung möglich).</summary>
    [Fact]
    public async Task SchrittVor_BisEndeBeiPausierterSchleife_TerminiertSauber()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.FromSeconds(30));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var exitedCount = 0;
        session.Exited += (_, _) => Interlocked.Increment(ref exitedCount);

        // Vor dem Start pausieren: die Schleife parkt dadurch deterministisch am Pause-Gate
        // (Position 0, kein anhängiger Delay) — kein Raten des Parkpunkts nötig.
        session.Pausieren();
        session.WiedergabeStarten();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Laeuft);

        session.SchrittVor().Should().BeTrue();
        session.SchrittVor().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(2);
        await WarteBisAsync(() => exitedCount == 1);

        // Die vom Ende-Schritt geweckte Schleife sieht Position == Count und terminiert ohne
        // weitere Chunk-Anwendung (Exited ist bereits signalisiert — kein zweites Event);
        // mit dem Schleifen-Task ist auch das Lauf-Flag freigegeben.
        await GetPlaybackTask(session)!.WaitAsync(TimeSpan.FromSeconds(5));
        exitedCount.Should().Be(1);

        // Rückwärtsschritt + erneutes Starten: ein neuer Durchlauf setzt an der
        // Schrittposition fort und feuert am Ende erneut Exited.
        session.SchrittZurueck().Should().BeTrue();
        session.WiedergabeStarten();
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Laeuft,
            "die terminierte Schleife muss das Lauf-Flag freigegeben haben");
        // Der Delay der neuen Schleife wird erst nach dem Task-Start angelegt — wiederholte
        // Advances decken die Registrierungsreihenfolge ab.
        await WarteBisAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(60));
            return exitedCount == 2;
        });
        session.AktuellerChunkIndex.Should().Be(2);
    }

    /// <summary>Race-Schutz: erreicht <see cref="TerminalReplaySession.SchrittVor"/> das Ende,
    /// während eine Wiedergabe-Schleife pausiert am Gate parkt, muss die aufgeweckte Schleife
    /// auch dann terminieren, wenn ein <see cref="TerminalReplaySession.SchrittZurueck"/> die
    /// Position vor dem Positions-Lesen der Schleife wieder senkt — sonst liefe eine
    /// zeitgesteuerte „Geister-Wiedergabe" weiter, obwohl <see cref="ITerminalSession.Exited"/>
    /// bereits gefeuert hat. Der Rückwärtsschritt läuft hier deterministisch im synchronen
    /// Exited-Handler, der im SchrittVor-Endpfad vor dem Öffnen des Pause-Gates ausgeführt wird.</summary>
    [Fact]
    public async Task SchrittZurueck_ImExitedHandler_TerminiertAufgeweckteSchleife()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.FromSeconds(30));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var empfangen = new List<byte[]>();
        var exitedCount = 0;
        session.OutputChunk += (_, e) => empfangen.Add(e.Data.ToArray());
        session.Exited += (_, _) =>
        {
            Interlocked.Increment(ref exitedCount);
            // Senkt die Position deterministisch vor dem Fortsetzen()-Aufruf des
            // SchrittVor-Endpfads — danach liest die aufgeweckte Schleife Position < Count.
            session.SchrittZurueck();
        };

        // Vor dem Start pausieren: die Schleife parkt deterministisch am Pause-Gate.
        session.Pausieren();
        session.WiedergabeStarten();

        session.SchrittVor().Should().BeTrue();
        session.SchrittVor().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(1,
            "der Rückwärtsschritt im Exited-Handler hat den letzten Chunk zurückgenommen");
        exitedCount.Should().Be(1);

        // Die aufgeweckte Schleife muss am Terminations-Flag enden, statt an Position 1
        // die zeitgesteuerte Wiedergabe fortzusetzen (dieser Test würde sonst im
        // FakeTimeProvider-Delay parken und hier in den Timeout laufen).
        await GetPlaybackTask(session)!.WaitAsync(TimeSpan.FromSeconds(5));

        // Auch nach großzügigem Zeitsprung darf nichts mehr angewendet oder signalisiert werden.
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(150);
        session.AktuellerChunkIndex.Should().Be(1,
            "die terminierte Schleife darf keine Chunks mehr anwenden");
        empfangen.Should().HaveCount(2, "nur die beiden Schritt-Chunks wurden angewendet");
        exitedCount.Should().Be(1, "eine Geister-Wiedergabe würde am Ende erneut Exited feuern");
    }

    /// <summary>Nach einem beendeten Durchlauf und einem Rückwärtsschritt startet
    /// <see cref="TerminalReplaySession.WiedergabeStarten"/> eine neue Schleife, die nur die
    /// fehlenden Chunks ab der Schrittposition anwendet und erneut Exited feuert.</summary>
    [Fact]
    public async Task WiedergabeStarten_NachEndeUndSchrittZurueck_SetztAnPositionFort()
    {
        var timeProvider = new FakeTimeProvider();
        var chunkC = Encoding.UTF8.GetBytes("C");
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B"), chunkC],
            TimeSpan.Zero);
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider)
        {
            ZeitrafferSchwelle = TimeSpan.Zero,
        };
        var empfangen = new List<byte[]>();
        var exitedCount = 0;
        session.OutputChunk += (_, e) => empfangen.Add(e.Data.ToArray());
        session.Exited += (_, _) => Interlocked.Increment(ref exitedCount);

        session.WiedergabeStarten();
        await WarteBisAsync(() => exitedCount == 1);
        session.AktuellerChunkIndex.Should().Be(3);
        // Das Lauf-Flag wird erst im finally der Schleife freigegeben — der Task-Abschluss
        // ist der deterministische Wartepunkt für die Re-Armierung.
        await GetPlaybackTask(session)!.WaitAsync(TimeSpan.FromSeconds(5));

        session.SchrittZurueck().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(2);

        session.WiedergabeStarten();
        await WarteBisAsync(() => exitedCount == 2);

        session.AktuellerChunkIndex.Should().Be(3);
        empfangen.Should().HaveCount(4, "nur der fehlende letzte Chunk wird erneut angewendet");
        empfangen[3].Should().Equal(chunkC);
    }

    /// <summary>Rest-Race-Regression: trifft <see cref="TerminalReplaySession.WiedergabeStarten"/>
    /// auf eine noch lebende, aber vom SchrittVor-Endpfad zum Abbruch verurteilte Schleife (CAS
    /// schlägt fehl, Terminations-Flag gesetzt), darf der Aufruf nicht wirkungslos returnen —
    /// die sterbende Schleife feuert bei Position &lt; Count kein Exited und der Aufrufer bliebe
    /// dauerhaft im Glauben einer laufenden Wiedergabe (UI-Sequenz: pausiert → SchrittVor bis
    /// Ende → SchrittZurueck → „Abspielen" → dauerhaft „Wiedergabe läuft." ohne Fortschritt).
    /// Hier parkt die Schleife garantiert im FakeTimeProvider-Delay, kann das Flag also vor dem
    /// CAS-Fail nicht konsumieren — der Start widerruft die Termination und die Wiedergabe
    /// setzt an der Schrittposition fort.</summary>
    [Fact]
    public async Task WiedergabeStarten_GegenVerurteilteSchleife_SetztAnSchrittpositionFort()
    {
        var timeProvider = new FakeTimeProvider();
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B"), Encoding.UTF8.GetBytes("C")],
            TimeSpan.FromSeconds(30));
        using var session = new TerminalReplaySession(aufzeichnung, timeProvider);
        var exitedCount = 0;
        session.Exited += (_, _) => Interlocked.Increment(ref exitedCount);

        session.WiedergabeStarten();
        await WarteBisAsync(() => session.AktuellerChunkIndex >= 1);
        // Der Schleife Zeit geben, im 30-s-Fake-Delay des zweiten Chunks zu parken — bis zum
        // nächsten Advance kann sie das Terminations-Flag nicht lesen (Delays sind nicht
        // ge-gatet; das Fenster entspricht dem realen bis zur ZeitrafferSchwelle).
        await Task.Delay(150);
        session.Pausieren();

        // Pausiert bis ans Ende schreiten: setzt das Terminations-Flag und feuert Exited;
        // danach ein Schritt zurück — Position < Count bei noch gesetztem Flag.
        session.SchrittVor().Should().BeTrue();
        session.SchrittVor().Should().BeTrue();
        await WarteBisAsync(() => exitedCount == 1);
        session.SchrittZurueck().Should().BeTrue();
        session.AktuellerChunkIndex.Should().Be(2);

        // „Abspielen": die Schleife schläft noch im Fake-Delay — der CAS schlägt garantiert
        // fehl. Ohne Widerruf/Neustart stürbe die Schleife beim nächsten Advance lautlos.
        session.WiedergabeStarten();
        await WarteBisAsync(() => session.RuntimeStatus == CliRuntimeStatus.Laeuft);

        // Nach Zeitsprüngen muss die Wiedergabe an der Schrittposition fortsetzen — über die
        // widerrufene Schleife oder einen ans Task-Ende gehängten Neustart: der letzte Chunk
        // wird angewendet und Exited feuert für diesen Durchlauf erneut.
        await WarteBisAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(30));
            return exitedCount == 2;
        });
        session.AktuellerChunkIndex.Should().Be(3);
        session.RuntimeStatus.Should().Be(CliRuntimeStatus.Inaktiv);
    }

    /// <summary>Nach <see cref="TerminalReplaySession.Dispose"/> sind beide Schritt-Methoden
    /// No-Ops (<c>false</c>, keine Events, kein Wurf).</summary>
    [Fact]
    public void Schritte_NachDispose_SindNoOp()
    {
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("B")],
            TimeSpan.Zero);
        var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());
        var events = 0;
        session.OutputChunk += (_, _) => events++;
        session.BufferChanged += (_, _) => events++;
        session.Exited += (_, _) => events++;

        session.SchrittVor();
        events.Should().Be(2, "der angewendete Chunk feuert OutputChunk und BufferChanged");
        session.Dispose();
        events = 0;

        session.SchrittVor().Should().BeFalse();
        session.SchrittZurueck().Should().BeFalse();
        session.AktuellerChunkIndex.Should().Be(1);
        events.Should().Be(0, "nach Dispose dürfen keine Events mehr feuern");
    }

    /// <summary>Ein aufgezeichnetes CSI-8-Resize ändert die Replay-Geometrie. Der Rückwärtsschritt
    /// baut den Header-Zustand vor dem Resize wieder auf, ohne dass die WPF-Fenstergröße beteiligt ist.</summary>
    [Fact]
    public void Geometrie_Csi8Resize_WirdWiedergegebenUndBeimRueckwaertsschrittZurueckgesetzt()
    {
        var aufzeichnung = CreateAufzeichnung(
            [Encoding.UTF8.GetBytes("A"), Encoding.UTF8.GetBytes("\x1b[8;73;142tB")],
            TimeSpan.Zero,
            cols: 220,
            rows: 50);
        using var session = new TerminalReplaySession(aufzeichnung, new FakeTimeProvider());

        session.Buffer.Cols.Should().Be(220);
        session.Buffer.Rows.Should().Be(50);

        session.SchrittVor();
        session.Buffer.Cols.Should().Be(220);
        session.Buffer.Rows.Should().Be(50);

        session.SchrittVor();
        session.Buffer.Cols.Should().Be(142, "CSI 8;73;142t setzt die Textfläche auf 142 Spalten");
        session.Buffer.Rows.Should().Be(73);
        ZeilenText(session.Buffer, 0).StartsWith("AB").Should().BeTrue();

        session.SchrittZurueck();
        session.Buffer.Cols.Should().Be(220, "der Präfix-Rebuild stellt die Header-Geometrie vor dem Resize wieder her");
        session.Buffer.Rows.Should().Be(50);

        session.SchrittVor();
        session.RebuildBufferFromReplay();
        session.Buffer.Cols.Should().Be(142, "der Rebuild reproduziert auch CSI-8-Resizes aus dem Präfix");
        session.Buffer.Rows.Should().Be(73);
    }

    private static Task GetReadLoopTask(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_readLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)field.GetValue(session)!;
    }

    /// <summary>Bewusster Trade-off: koppelt die Tests an das private Feld
    /// <c>_playbackTask</c> (bricht bei Umbenennung ohne Funktionsänderung). Öffentliches
    /// Verhalten bietet hier keinen gleichwertigen Wartepunkt — <c>Exited</c> feuert vor
    /// der Lauf-Flag-Freigabe im <c>finally</c>, und ein poller-<see cref="TerminalReplaySession.WiedergabeStarten"/>
    /// kann erst durchgreifen, wenn die alte Schleife wirklich terminiert ist; davor darf
    /// die Position nicht verändert werden, sonst setzt die alte Schleife selbst an der
    /// Schrittposition fort statt zu enden. Der Task-Abschluss ist der einzige
    /// deterministische Nachweis der Re-Armierung.</summary>
    private static Task? GetPlaybackTask(TerminalReplaySession session)
    {
        var field = typeof(TerminalReplaySession).GetField("_playbackTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task?)field.GetValue(session);
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

    private static string ZeilenText(Softwareschmiede.Domain.Terminal.TerminalBuffer buffer, int row)
        => new(buffer.GetRow(row).Select(c => c.Character).ToArray());

    private static string BufferAlsText(Softwareschmiede.Domain.Terminal.TerminalBuffer buffer)
    {
        var sb = new StringBuilder();
        var snapshot = buffer.GetSnapshot();
        for (var row = 0; row < snapshot.Rows; row++)
        {
            for (var col = 0; col < snapshot.Cols; col++)
                sb.Append(snapshot.Grid[row, col].Character);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static CliOutputAufzeichnung CreateAufzeichnung(IReadOnlyList<byte[]> chunks, TimeSpan intervall, int cols = 80, int rows = 24)
    {
        var records = chunks
            .Select((data, i) => new CliOutputChunkRecord(intervall * i, data))
            .ToArray();
        return new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "TestPlugin",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = cols,
            Rows = rows,
            IstVollstaendig = true,
            Chunks = records,
        };
    }

    /// <summary>Stream, der pro Lesevorgang genau einen Chunk liefert und danach das Ende meldet.</summary>
    private sealed class ChunkedStream : Stream
    {
        private readonly IReadOnlyList<byte[]> _chunks;
        private int _index;

        public ChunkedStream(IReadOnlyList<byte[]> chunks)
        {
            _chunks = chunks;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_index >= _chunks.Count)
                return new ValueTask<int>(0);

            var chunk = _chunks[_index++];
            chunk.CopyTo(buffer);
            return new ValueTask<int>(chunk.Length);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
