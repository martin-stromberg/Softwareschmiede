using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für TerminalControl: Exception-Behandlung im Terminal-Lesevorgang (F12-F14).</summary>
public sealed class TerminalControlTests
{
    /// <summary>
    /// ReadLoopAsync darf keine unbehandelte Exception werfen, wenn außerhalb von ReadAsync
    /// (z. B. beim Anwenden eines Events auf den Buffer) ein Fehler auftritt; der Fehler muss geloggt werden.
    /// </summary>
    [Fact]
    public void ReadLoopAsync_WithException_LogsAndDoesNotThrow()
    {
        Exception? caught = null;
        var loggerMock = new Mock<ILogger<TerminalControl>>();

        RunOnSta(() =>
        {
            var control = new TerminalControl();
            SetLogger(control, loggerMock.Object);

            using var session = CreateSession(new SingleByteStream());

            var method = typeof(TerminalControl).GetMethod("ReadLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            // buffer=null erzwingt eine Exception beim Anwenden des geparsten Events (außerhalb von ReadAsync).
            var task = (Task)method.Invoke(control, new object?[] { session, null, CancellationToken.None })!;

            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        caught.Should().BeNull("ReadLoopAsync muss Exceptions außerhalb von ReadAsync intern fangen statt sie zu propagieren");
        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    /// <summary>OnSessionChanged (ausgelöst über den Session-Setter) speichert die Task-Referenz des Lesevorgangs in _readLoopTask.</summary>
    [Fact]
    public void OnSessionChanged_StoresReadLoopTask()
    {
        Task? readLoopTask = null;

        RunOnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            var control = new TerminalControl();
            using var session = CreateSession(new ImmediateEofStream());

            control.Session = session;

            var field = typeof(TerminalControl).GetField("_readLoopTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
            readLoopTask = (Task?)field.GetValue(control);
        });

        readLoopTask.Should().NotBeNull("OnSessionChanged muss die Task-Referenz des Lesevorgangs in _readLoopTask speichern");
    }

    /// <summary>
    /// Setzt man <see cref="TerminalControl.Session"/> mit einer Session, deren Output-Stream beim Lesen
    /// eine Exception wirft, muss der dadurch gestartete Lesevorgang den Fehler über den injizierten Logger
    /// protokollieren, ohne die Anwendung zum Absturz zu bringen (Verhaltenstest über den öffentlichen
    /// Session-Setter statt direktem Reflection-Aufruf von ReadLoopAsync).
    /// </summary>
    [Fact]
    public void OnSessionChanged_ReadLoopThrows_LogsErrorViaInjectedLogger()
    {
        var loggerMock = new Mock<ILogger<TerminalControl>>();
        var errorLogged = new TaskCompletionSource();
        loggerMock
            .Setup(l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => errorLogged.TrySetResult());

        RunOnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            var control = new TerminalControl();
            SetLogger(control, loggerMock.Object);

            using var session = CreateSession(new ThrowingStream());

            control.Session = session;

            var finished = Task.WhenAny(errorLogged.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            finished.Should().Be(errorLogged.Task, "der über Session gestartete Lesevorgang muss Lesefehler des Output-Streams über den Logger protokollieren");
        });
    }

    /// <summary>
    /// Schreibt der Anwender Text, während die Pipe zum CLI-Prozess bereits geschlossen ist (z. B. weil der
    /// Prozess gerade beendet wurde), darf OnTextInput die dabei auftretende Exception nicht stillschweigend
    /// verwerfen, sondern muss sie über den injizierten Logger protokollieren.
    /// </summary>
    [Fact]
    public void OnTextInput_WriteThrows_LogsWarning()
    {
        var loggerMock = new Mock<ILogger<TerminalControl>>();

        RunOnSta(() =>
        {
            var control = new TerminalControl();
            SetLogger(control, loggerMock.Object);

            using var session = CreateSession(new WriteThrowingStream(), new ImmediateEofStream());
            control.Session = session;

            var textComposition = new TextComposition(InputManager.Current, control, "a");
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, textComposition)
            {
                RoutedEvent = TextCompositionManager.TextInputEvent,
            };

            var method = typeof(TerminalControl).GetMethod("OnTextInput", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var act = () => method.Invoke(control, new object[] { args });

            act.Should().NotThrow("ein Schreibfehler auf dem Terminal-Input-Stream darf nicht propagieren");
        });

        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(lvl => lvl == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce(),
            "ein Schreibfehler beim Terminal-Input muss geloggt werden statt still verworfen zu werden");
    }

    private static void SetLogger(TerminalControl control, ILogger<TerminalControl> logger)
    {
        var field = typeof(TerminalControl).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(control, logger);
    }

    private static PseudoConsoleSession CreateSession(Stream outputStream)
        => CreateSession(new MemoryStream(), outputStream);

    private static PseudoConsoleSession CreateSession(Stream inputStream, Stream outputStream)
    {
        var pseudoConsole = PseudoConsole.Create(1, 1);
        return new PseudoConsoleSession(pseudoConsole, System.Diagnostics.Process.GetCurrentProcess(), inputStream, outputStream);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            throw exception;
    }

    /// <summary>Stream, der beim ersten Lesevorgang ein Byte liefert und danach abbricht.</summary>
    private sealed class SingleByteStream : Stream
    {
        private bool _served;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_served)
                throw new OperationCanceledException();

            _served = true;
            buffer.Span[0] = (byte)'A';
            return new ValueTask<int>(1);
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Stream, der beim Lesevorgang sofort 0 Bytes liefert (simuliertes Stream-Ende), ohne die Dispatcher-Pumpe zu benötigen.</summary>
    private sealed class ImmediateEofStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(0);

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

    /// <summary>Stream, der beim Schreibvorgang eine Exception wirft (z. B. bereits geschlossene Pipe zum CLI-Prozess).</summary>
    private sealed class WriteThrowingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new IOException("Simulierter Schreibfehler des Terminal-Input-Streams");

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
