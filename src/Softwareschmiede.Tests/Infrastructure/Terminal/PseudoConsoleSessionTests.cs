using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Infrastructure.Terminal;

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

    private static Task GetReadLoopTask(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_readLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)field.GetValue(session)!;
    }

    private static PseudoConsoleSession CreateSession(Stream outputStream, ILogger? logger = null)
    {
        var pseudoConsole = PseudoConsole.Create(1, 1);
        return new PseudoConsoleSession(
            pseudoConsole,
            System.Diagnostics.Process.GetCurrentProcess(),
            new MemoryStream(),
            outputStream,
            logger);
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
}
