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
        // Parkte die Schleife vor dem Delay am Gate, wird der Delay erst jetzt angelegt — ein
        // weiterer Advance deckt beide Reihenfolgen ab.
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await beendet.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    /// Null-Streams, No-op-Eingaben und immer erfolgreiche Resize-/Drain-Aufrufe (Voraussetzung für
    /// <c>TerminalControl.OnSessionChanged</c>).</summary>
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

    private static Task GetReadLoopTask(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_readLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)field.GetValue(session)!;
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

    private static CliOutputAufzeichnung CreateAufzeichnung(IReadOnlyList<byte[]> chunks, TimeSpan intervall)
    {
        var records = chunks
            .Select((data, i) => new CliOutputChunkRecord(intervall * i, data))
            .ToArray();
        return new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "TestPlugin",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
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
