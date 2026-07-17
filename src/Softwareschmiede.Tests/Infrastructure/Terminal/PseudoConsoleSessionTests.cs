using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="PseudoConsoleSession"/>: Die Leseschleife läuft ab Konstruktion bis
/// <see cref="PseudoConsoleSession.Dispose"/> unabhängig vom Lebenszyklus eines anzeigenden Controls
/// (Issue-86, parallele CLI-Ausführungen).</summary>
public sealed class PseudoConsoleSessionTests
{
    /// <summary>Wirft der Output-Stream beim Lesen eine Exception, muss die Leseschleife dies über den
    /// injizierten Logger protokollieren und danach sauber beendet werden, statt die Anwendung zum Absturz
    /// zu bringen.</summary>
    [Fact]
    public async Task ReadLoopAsync_WithException_LogsAndContinues()
    {
        var loggerMock = new Mock<ILogger>();

        using var session = CreateSession(new ThrowingStream(), loggerMock.Object);

        var readLoopTask = GetReadLoopTask(session);
        var completed = await Task.WhenAny(readLoopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(readLoopTask, "die Leseschleife muss nach einem Lesefehler sauber beendet werden");
        readLoopTask.IsFaulted.Should().BeFalse("ein Lesefehler darf die Leseschleife nicht mit einer unbehandelten Exception beenden");
        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "ein Lesefehler des Output-Streams muss über den Logger protokolliert werden");
    }

    /// <summary>Wird das Abbruch-Token der Leseschleife ausgelöst (via <see cref="PseudoConsoleSession.Dispose"/>),
    /// muss die Leseschleife sauber beendet werden, ohne eine unbehandelte Exception zu werfen.</summary>
    [Fact]
    public async Task ReadLoopAsync_CancellationToken_GracefulShutdown()
    {
        var session = CreateSession(new BlockingUntilCancelledStream());
        var readLoopTask = GetReadLoopTask(session);

        session.Dispose();

        var completed = await Task.WhenAny(readLoopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(readLoopTask, "die Leseschleife muss nach Abbruch des CancellationTokens zeitnah enden");
        readLoopTask.IsFaulted.Should().BeFalse("der Abbruch der Leseschleife darf keine unbehandelte Exception verursachen");
    }

    /// <summary>Ruft man <see cref="PseudoConsoleSession.Dispose"/> auf, muss die Leseschleife abgebrochen und
    /// die Ein-/Ausgabe-Streams geschlossen werden, damit keine verwaisten Leseschleifen zurückbleiben.</summary>
    [Fact]
    public void SessionDispose_CancelsReadLoop()
    {
        var outputStream = new BlockingUntilCancelledStream();
        var session = CreateSession(outputStream);
        var readLoopTask = GetReadLoopTask(session);

        session.Dispose();

        outputStream.WasDisposed.Should().BeTrue("Dispose() muss den Output-Stream der Sitzung schließen");
        var finished = readLoopTask.IsCompleted || readLoopTask.Wait(TimeSpan.FromSeconds(5));
        finished.Should().BeTrue("die Leseschleife muss nach Dispose() sauber beendet sein");
    }

    /// <summary>Blockiert der native Read bereits (z. B. weil ein isAsync:false-FileStream einen laufenden
    /// ReadFile-Syscall wrappt), unterbricht <see cref="CancellationTokenSource.Cancel"/> ihn nicht, da das
    /// Token nur zwischen zwei Lesevorgängen ausgewertet wird. <see cref="PseudoConsoleSession.Dispose"/> muss
    /// deshalb zusätzlich den Output-Stream schließen, damit ein solcher Read sofort mit einem I/O-Fehler
    /// zurückkehrt, statt den vollen Leseschleifen-Timeout ergebnislos ablaufen zu lassen.</summary>
    [Fact]
    public void Dispose_ClosesOutputStreamImmediately_UnblocksNonCancelableRead()
    {
        var outputStream = new NonCancelableBlockingStream();
        var session = CreateSession(outputStream);
        var readLoopTask = GetReadLoopTask(session);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        session.Dispose();
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(2),
            "Dispose() darf nicht auf den vollen Leseschleifen-Timeout warten, wenn ein blockierter, nicht " +
            "kooperativ abbrechbarer Read durch das Schließen des Output-Streams sofort unterbrochen werden kann");
        outputStream.WasDisposed.Should().BeTrue("Dispose() muss den Output-Stream der Sitzung schließen");
        var finished = readLoopTask.IsCompleted || readLoopTask.Wait(TimeSpan.FromSeconds(2));
        finished.Should().BeTrue("die Leseschleife muss nach Dispose() sauber beendet sein");
    }

    /// <summary>Ruft man <see cref="PseudoConsoleSession.Dispose"/> gleichzeitig von zwei Threads auf (z. B. weil
    /// der asynchrone <c>Process.Exited</c>-Handler und ein expliziter Aufrufer wie
    /// <c>KiAusfuehrungsService.Dispose()</c> parallel laufen — genau das Szenario, das die parallele
    /// CLI-Ausführung aus Issue-86 ermöglicht), darf der Aufräum-Code nur genau einmal ausgeführt werden.
    /// Der ungeschützte <c>bool _disposed</c>-Guard erlaubt beiden Threads, die Prüfung gleichzeitig zu
    /// bestehen, wodurch native Handles (ConPTY, Pipes) doppelt freigegeben werden — dies führt in der
    /// echten Anwendung zu einem nicht abfangbaren Absturz statt einer verwalteten Exception.</summary>
    [Fact]
    public void Dispose_CalledConcurrently_RunsCleanupExactlyOnce()
    {
        for (var i = 0; i < 5000; i++)
        {
            var outputStream = new DisposeCountingStream();
            var session = CreateSession(outputStream);

            // Statt eines Barriers (Kernel-Wartezustand, dessen Aufwach-Jitter das enge Zeitfenster
            // des ungeschützten "if (_disposed) return; _disposed = true;"-Checks leicht überdecken kann)
            // spinnen beide Threads aktiv auf ein gemeinsames volatiles Flag. Da beide Threads dabei bereits
            // auf demselben Prozessorkern-Zustand "heiß" laufen, propagiert das Flag über die Cache-Kohärenz
            // innerhalb weniger Nanosekunden – genau in der Größenordnung des zu testenden Race-Fensters.
            var ready1 = 0;
            var ready2 = 0;
            var go = 0;

            void DisposeViaSpin(ref int readyFlag)
            {
                Volatile.Write(ref readyFlag, 1);
                var spin = new SpinWait();
                while (Volatile.Read(ref go) == 0)
                    spin.SpinOnce();
                session.Dispose();
            }

            var t1 = new Thread(() => DisposeViaSpin(ref ready1));
            var t2 = new Thread(() => DisposeViaSpin(ref ready2));
            t1.Start();
            t2.Start();

            var readySpin = new SpinWait();
            while (Volatile.Read(ref ready1) == 0 || Volatile.Read(ref ready2) == 0)
                readySpin.SpinOnce();
            Volatile.Write(ref go, 1);

            t1.Join();
            t2.Join();

            outputStream.DisposeCount.Should().Be(
                1,
                "Dispose() darf den Output-Stream (und damit auch die nativen ConPTY-Handles) auch bei " +
                "gleichzeitigem Aufruf aus mehreren Threads nur genau einmal freigeben");
        }
    }

    /// <summary>Kann die Leseschleife nicht zeitnah beenden (z. B. weil ein nativer Read partout nicht auf das
    /// Schließen des Streams reagiert), darf <see cref="PseudoConsoleSession.Dispose"/> trotzdem nicht darauf
    /// warten. Ein früheres <c>_readLoopTask.Wait(timeout)</c> hätte hier bis zu 5 Sekunden blockiert — und zwar
    /// auf dem aufrufenden Thread, der bei paralleler CLI-Ausführung oft ein ThreadPool-Thread aus dem
    /// <c>Process.Exited</c>-Handler ist. Bei vielen gleichzeitig beendeten Sitzungen summieren sich solche
    /// blockierenden Waits zu ThreadPool-Erschöpfung und verzögern z. B. das Loggen einer Exited-Handler-Exception
    /// um mehrere Sekunden bis hin zum Timeout (siehe KiAusfuehrungsServiceTests.ConPtyProcessExited_...).</summary>
    [Fact]
    public void Dispose_ReadLoopNeverCompletes_ReturnsPromptlyWithoutWaiting()
    {
        var outputStream = new HangingForeverStream();
        var session = CreateSession(outputStream);
        var readLoopTask = GetReadLoopTask(session);

        // Sicherstellen, dass die Leseschleife den Read tatsächlich schon erreicht hat, bevor Dispose() aufgerufen
        // wird — sonst könnte Cancel() bereits vor dem ersten ReadAsync-Aufruf greifen und die Schleife würde die
        // while-Bedingung nie mehr betreten, statt (wie hier beabsichtigt) mitten im Read hängen zu bleiben.
        outputStream.ReadStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue(
            "die Leseschleife muss den ersten Read erreicht haben, damit dieser Test das gewünschte Szenario prüft");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        session.Dispose();
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(1),
            "Dispose() darf nicht synchron auf die Leseschleife warten, selbst wenn diese (wie hier absichtlich " +
            "simuliert) partout nicht zeitnah beendet werden kann — ein solches Warten blockiert unter paralleler " +
            "CLI-Ausführung ThreadPool-Threads, die für den Fortschritt anderer Sitzungen benötigt werden");
        readLoopTask.IsCompleted.Should().BeFalse(
            "dieser Test simuliert absichtlich eine Leseschleife, die nicht beendet werden kann, um zu zeigen, " +
            "dass Dispose() trotzdem sofort zurückkehrt statt darauf zu warten");
    }

    /// <summary><see cref="PseudoConsoleSession.BufferChanged"/> wird erst gefeuert, nachdem die Leseschleife
    /// alle Events des gelesenen Chunks bereits auf <see cref="PseudoConsoleSession.Buffer"/> angewendet hat
    /// (kritischer Synchronisierungs-Fix): Zum Zeitpunkt des Events muss der neue Bufferinhalt bereits
    /// sichtbar sein, damit ein lauschendes <c>TerminalControl</c> beim Neuzeichnen keinen veralteten
    /// Zustand liest.</summary>
    [Fact]
    public void ReadLoopAsync_BufferChangedFiredAfterBufferUpdated()
    {
        using var session = CreateSession(new FixedContentStream("HELLO"));
        char? characterAtEventTime = null;
        var bufferChanged = new ManualResetEventSlim(false);

        session.BufferChanged += (_, _) =>
        {
            characterAtEventTime = session.Buffer.GetRow(0)[0].Character;
            bufferChanged.Set();
        };

        bufferChanged.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue(
            "BufferChanged muss nach Verarbeitung der gelesenen Ausgabe gefeuert werden");
        characterAtEventTime.Should().Be(
            'H',
            "der Bufferinhalt muss zum Zeitpunkt des BufferChanged-Events bereits vollständig angewendet sein");
    }

    private static Task GetReadLoopTask(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_readLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)field.GetValue(session)!;
    }

    private static PseudoConsoleSession CreateSession(Stream outputStream, ILogger? logger = null)
    {
        return TestPseudoConsoleSessionFactory.Create(new MemoryStream(), outputStream, logger);
    }

    /// <summary>Stream, der beim Lesevorgang eine Exception wirft (z. B. simulierter Pipe-Fehler).</summary>
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("Simulierter Lesefehler des Terminal-Output-Streams");

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, dessen Lesevorgang erst zurückkehrt, wenn das übergebene CancellationToken abgebrochen
    /// wird (simuliert einen Prozess, der aktuell keine Ausgabe produziert). Erfasst, ob <see cref="Dispose(bool)"/>
    /// aufgerufen wurde.</summary>
    private sealed class BlockingUntilCancelledStream : Stream
    {
        public bool WasDisposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Stream, der zählt, wie oft <see cref="Dispose(bool)"/> aufgerufen wurde — dient zum Nachweis,
    /// dass <see cref="PseudoConsoleSession.Dispose"/> bei gleichzeitigem Aufruf aus mehreren Threads seinen
    /// Aufräum-Code nicht mehrfach ausführt.</summary>
    private sealed class DisposeCountingStream : Stream
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _disposeCount);
            base.Dispose(disposing);
        }
    }

    /// <summary>Stream, dessen Lesevorgang das übergebene <see cref="CancellationToken"/> absichtlich ignoriert und
    /// erst zurückkehrt, wenn der Stream selbst disposed wird — simuliert einen bereits blockierten nativen Read
    /// (isAsync:false-FileStream), der durch <see cref="CancellationTokenSource.Cancel"/> allein nicht unterbrochen
    /// werden kann, sondern nur durch Schließen des Streams.</summary>
    private sealed class NonCancelableBlockingStream : Stream
    {
        private readonly TaskCompletionSource<int> _unblockSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WasDisposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(_unblockSignal.Task);

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            _unblockSignal.TrySetException(new IOException("Simulierter Pipe-Fehler durch Schließen des Handles."));
            base.Dispose(disposing);
        }
    }

    /// <summary>Stream, der beim ersten Lesevorgang einen festen Inhalt liefert und danach das Stream-Ende
    /// (0 Bytes) meldet.</summary>
    private sealed class FixedContentStream : Stream
    {
        private readonly byte[] _content;
        private bool _served;

        public FixedContentStream(string content)
        {
            _content = System.Text.Encoding.ASCII.GetBytes(content);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_served)
                return new ValueTask<int>(0);

            _served = true;
            _content.CopyTo(buffer);
            return new ValueTask<int>(_content.Length);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, dessen Lesevorgang niemals zurückkehrt — auch nicht nach <see cref="Dispose(bool)"/> —
    /// um den Worst Case einer Leseschleife zu simulieren, die partout nicht zeitnah beendet werden kann.</summary>
    private sealed class HangingForeverStream : Stream
    {
        private readonly ManualResetEventSlim _readStarted = new();

        /// <summary>Signalisiert, sobald der erste Lesevorgang tatsächlich aufgerufen wurde.</summary>
        public ManualResetEventSlim ReadStarted => _readStarted;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _readStarted.Set();
            return new(new TaskCompletionSource<int>().Task);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
