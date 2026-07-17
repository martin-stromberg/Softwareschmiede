using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Tests.Helpers;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für TerminalControl: Tastatureingabe-Fehlerbehandlung und BufferChanged-Bindung an
/// die Session (Issue-86, parallele CLI-Ausführungen — die Leseschleife läuft in <see cref="PseudoConsoleSession"/>,
/// TerminalControl ist reiner Renderer).</summary>
public sealed partial class TerminalControlTests
{
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

    /// <summary>Setzt man <see cref="TerminalControl.Session"/>, muss ein Handler für
    /// <see cref="PseudoConsoleSession.BufferChanged"/> auf der neuen Session registriert werden, damit das
    /// Control auf neu eintreffende Ausgabe mit einer Neuzeichnung reagiert (beobachtet über die dabei am
    /// UI-Dispatcher angestoßene Operation).</summary>
    [Fact]
    public void OnSessionChanged_RegistersBufferChangedHandler()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var stream = new ControllableStream();
            using var session = CreateSession(stream);

            control.Session = session;

            var operationsPosted = RaiseOutputAndCountDispatcherOperations(session, stream, "X");

            operationsPosted.Should().BeGreaterThan(
                0,
                "OnSessionChanged muss einen BufferChanged-Handler auf der neuen Session registrieren, der bei neuer Ausgabe eine Neuzeichnung des Controls anstößt");
        });
    }

    /// <summary>Wechselt das Control von Session A zu Session B, muss der Handler auf A deregistriert und auf
    /// B registriert werden — sonst würde A das Control dauerhaft referenzieren (Memory-Leak) und weiterhin
    /// Neuzeichnungen anstoßen, obwohl B angezeigt wird.</summary>
    [Fact]
    public void OnSessionChanged_ToNewSession_DeregistersOldHandler()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var streamA = new ControllableStream();
            using var streamB = new ControllableStream();
            using var sessionA = CreateSession(streamA);
            using var sessionB = CreateSession(streamB);

            control.Session = sessionA;
            control.Session = sessionB;

            var operationsOnOldSession = RaiseOutputAndCountDispatcherOperations(sessionA, streamA, "A");
            operationsOnOldSession.Should().Be(
                0,
                "beim Wechsel zu einer neuen Session darf neue Ausgabe der alten Session keine Neuzeichnung des Controls mehr anstoßen");

            var operationsOnNewSession = RaiseOutputAndCountDispatcherOperations(sessionB, streamB, "B");
            operationsOnNewSession.Should().BeGreaterThan(
                0,
                "die neue Session muss weiterhin Neuzeichnungen des Controls anstoßen");
        });
    }

    /// <summary>Wird die Session auf <c>null</c> gesetzt (z. B. weil der CLI-Prozess gestoppt wurde), darf
    /// spätere Ausgabe der zuvor gebundenen Session das Control nicht mehr zu einer Neuzeichnung veranlassen.</summary>
    [Fact]
    public void OnSessionChanged_ToNull_DeregistersAllHandlers()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var stream = new ControllableStream();
            using var session = CreateSession(stream);

            control.Session = session;
            control.Session = null;

            var operationsPosted = RaiseOutputAndCountDispatcherOperations(session, stream, "X");

            operationsPosted.Should().Be(
                0,
                "wird die Session auf null gesetzt, darf spätere Ausgabe der alten Session keine Neuzeichnung des Controls mehr anstoßen");
        });
    }

    /// <summary>Zwei parallele Sessions mit unterschiedlicher Ausgabe dürfen sich nicht gegenseitig beeinflussen:
    /// Jede Sitzung besitzt ihren eigenen Buffer, der ausschließlich durch die eigene Leseschleife befüllt wird.</summary>
    [Fact]
    public void ParallelSessions_NoBufferInterference()
    {
        RunOnSta(() =>
        {
            var controlA = new TerminalControl();
            var controlB = new TerminalControl();
            using var sessionA = CreateSession(new FixedContentStream("AAA"));
            using var sessionB = CreateSession(new FixedContentStream("BBB"));

            var doneA = new TaskCompletionSource();
            var doneB = new TaskCompletionSource();
            sessionA.BufferChanged += (_, _) => doneA.TrySetResult();
            sessionB.BufferChanged += (_, _) => doneB.TrySetResult();

            controlA.Session = sessionA;
            controlB.Session = sessionB;

            Task.WhenAny(doneA.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            Task.WhenAny(doneB.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();

            sessionA.Buffer.GetRow(0)[0].Character.Should().Be('A', "Session A darf nur ihre eigene Ausgabe im Buffer haben");
            sessionB.Buffer.GetRow(0)[0].Character.Should().Be('B', "Session B darf nur ihre eigene Ausgabe im Buffer haben");
        });
    }

    /// <summary>Wechselt das Control von Session A zu Session B und zurück zu A, muss der Bufferinhalt von A
    /// unverändert erhalten geblieben sein — die Leseschleife von A lief währenddessen unabhängig weiter im
    /// Hintergrund und puffert Ausgabe, statt sie zu verlieren.</summary>
    [Fact]
    public void SessionSwitch_BackToPreviousSession_PreservesBuffer()
    {
        RunOnSta(() =>
        {
            var control = new TerminalControl();
            using var sessionA = CreateSession(new FixedContentStream("HELLO"));
            using var sessionB = CreateSession(new ImmediateEofStream());

            var doneA = new TaskCompletionSource();
            sessionA.BufferChanged += (_, _) => doneA.TrySetResult();

            control.Session = sessionA;
            Task.WhenAny(doneA.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();

            var zeichenNachErstemRender = sessionA.Buffer.GetRow(0)[0].Character;

            control.Session = sessionB;
            control.Session = sessionA;

            sessionA.Buffer.GetRow(0)[0].Character.Should().Be(
                zeichenNachErstemRender,
                "der Bufferinhalt von Session A muss nach dem Wechsel zu B und zurück erhalten bleiben");
        });
    }

    /// <summary>Schiebt <paramref name="content"/> in <paramref name="stream"/>, wartet auf die vollständige
    /// Verarbeitung durch die Leseschleife von <paramref name="session"/> und zählt dabei, wie viele Operationen
    /// währenddessen am aktuellen UI-Dispatcher angestoßen wurden. Ein gebundenes <c>TerminalControl</c> stößt bei
    /// jeder Verarbeitung eine Neuzeichnung über <c>Dispatcher.InvokeAsync</c> an; ohne registrierten Handler
    /// bleibt die Operationszahl bei 0.</summary>
    /// <param name="session">Die Sitzung, deren Ausgabeverarbeitung abgewartet wird.</param>
    /// <param name="stream">Der steuerbare Output-Stream der Sitzung, in den <paramref name="content"/> geschoben wird.</param>
    /// <param name="content">Der zu schiebende Ausgabeinhalt.</param>
    /// <returns>Die Anzahl der während der Verarbeitung am UI-Dispatcher angestoßenen Operationen.</returns>
    private static int RaiseOutputAndCountDispatcherOperations(PseudoConsoleSession session, ControllableStream stream, string content)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var operationsPosted = 0;
        void OnOperationPosted(object? sender, DispatcherHookEventArgs e) => operationsPosted++;

        var processed = new TaskCompletionSource();
        void OnBufferChanged(object? sender, EventArgs e) => processed.TrySetResult();
        session.BufferChanged += OnBufferChanged;

        dispatcher.Hooks.OperationPosted += OnOperationPosted;
        try
        {
            stream.Push(content);
            Task.WhenAny(processed.Task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        }
        finally
        {
            dispatcher.Hooks.OperationPosted -= OnOperationPosted;
            session.BufferChanged -= OnBufferChanged;
        }

        return operationsPosted;
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
        return TestPseudoConsoleSessionFactory.Create(inputStream, outputStream);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                action();
            }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            throw exception;
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

    /// <summary>Stream, der beim ersten Lesevorgang einen festen Inhalt liefert und danach das Stream-Ende (0 Bytes) meldet.</summary>
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

    /// <summary>Stream, dessen Lesevorgang blockiert, bis über <see cref="Push"/> Inhalt bereitgestellt wird.
    /// Simuliert einen CLI-Prozess, der zu einem beliebigen späteren Zeitpunkt neue Ausgabe produziert — auch
    /// nachdem ein Control die gebundene Session bereits gewechselt hat.</summary>
    private sealed class ControllableStream : Stream
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        /// <summary>Stellt <paramref name="content"/> für den nächsten Lesevorgang bereit.</summary>
        /// <param name="content">Der bereitzustellende Ausgabeinhalt.</param>
        public void Push(string content)
        {
            _queue.Enqueue(System.Text.Encoding.ASCII.GetBytes(content));
            _signal.Release();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _signal.WaitAsync(cancellationToken);
            if (_queue.TryDequeue(out var data))
            {
                data.CopyTo(buffer);
                return data.Length;
            }

            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _signal.Dispose();
            base.Dispose(disposing);
        }
    }
}
