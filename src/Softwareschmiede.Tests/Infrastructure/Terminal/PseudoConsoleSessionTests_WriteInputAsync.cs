using System.Text;
using FluentAssertions;
using Softwareschmiede.App.Controls;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Tests fuer die zentrale, serialisierte Eingabeschreiblogik der Pseudokonsole.</summary>
public sealed class PseudoConsoleSessionTests_WriteInputAsync
{
    /// <summary>Ein langer Stacktrace-artiger Clipboard-Inhalt muss vollständig und bytegenau geschrieben werden.</summary>
    [Fact]
    public async Task WriteInputAsync_LangerMehrzeiligerText_SchreibtVollstaendigeBytesInReihenfolge()
    {
        var inputStream = new RecordingInputStream();
        using var session = CreateSession(inputStream);
        var text = CreateStacktraceLikeText(lineCount: 180);
        var expected = KeyToVt100Encoder.EncodeClipboardText(text);

        await session.WriteInputAsync(expected, CancellationToken.None);

        inputStream.WrittenBytes.Should().Equal(expected);
        inputStream.FlushAsyncCount.Should().Be(1);
    }

    /// <summary>Große Eingaben werden in stabile Chunks geteilt, deren Konkatenation der Originaleingabe entspricht.</summary>
    [Fact]
    public async Task WriteInputAsync_GrosseEingabe_SchreibtChunksSequentiellUndFlushtEinmal()
    {
        var inputStream = new RecordingInputStream();
        using var session = CreateSession(inputStream);
        var chunkSize = GetInputWriteChunkSize();
        var bytes = Enumerable.Range(0, chunkSize * 2 + 123)
            .Select(i => unchecked((byte)(i % 251)))
            .ToArray();

        await session.WriteInputAsync(bytes, CancellationToken.None);

        inputStream.WriteLengths.Should().Equal(chunkSize, chunkSize, 123);
        inputStream.WrittenBytes.Should().Equal(bytes);
        inputStream.FlushAsyncCount.Should().Be(1);
    }

    /// <summary>Parallele längere Eingaben auf dieselbe Session dürfen sich nicht im Input-Stream verschachteln.</summary>
    [Fact]
    public async Task WriteInputAsync_ParalleleWrites_WerdenNichtVerschachtelt()
    {
        var inputStream = new BlockingFirstWriteStream();
        using var session = CreateSession(inputStream);
        var first = Encoding.UTF8.GetBytes(new string('A', GetInputWriteChunkSize() + 5));
        var second = Encoding.UTF8.GetBytes("BBBB");

        var firstTask = session.WriteInputAsync(first, CancellationToken.None);
        inputStream.FirstWriteStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var secondTask = session.WriteInputAsync(second, CancellationToken.None);
        await Task.Delay(100);

        inputStream.WriteCalls.Should().Be(1, "der zweite Write darf erst nach Abschluss des ersten serialisierten Writes starten");

        inputStream.ReleaseFirstWrite();
        await Task.WhenAll(firstTask, secondTask);

        inputStream.WrittenBytes.Should().Equal(first.Concat(second));
    }

    /// <summary>Dispose waehrend eines laufenden Writes darf keine Semaphore-Release-Exception maskieren.</summary>
    [Fact]
    public async Task Dispose_WaehrendWriteInputAsync_MaskiertNichtReleaseUndHaengtNicht()
    {
        var inputStream = new BlockingFirstWriteStream();
        var session = CreateSession(inputStream);
        var bytes = Encoding.UTF8.GetBytes("laufender paste");

        var writeTask = session.WriteInputAsync(bytes, CancellationToken.None);
        inputStream.FirstWriteStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        session.Dispose();
        inputStream.ReleaseFirstWrite();

        var completed = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(writeTask, "ein laufender Input-Write darf durch Dispose() nicht haengen bleiben");
        await writeTask;
    }

    /// <summary>Bei einem Schreibfehler darf keine erfolgreiche Eingabeaktivität markiert werden.</summary>
    [Fact]
    public async Task WriteInputAsync_WriteFehler_MarkiertKeineInputAktivitaet()
    {
        using var session = CreateSession(new WriteThrowingInputStream());

        var act = () => session.WriteInputAsync(Encoding.UTF8.GetBytes("x"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        GetLastInputUtc(session).Should().BeNull();
    }

    private static PseudoConsoleSession CreateSession(Stream inputStream)
        => TestPseudoConsoleSessionFactory.Create(inputStream, new ImmediateEofStream());

    private static int GetInputWriteChunkSize()
    {
        var field = typeof(PseudoConsoleSession).GetField("InputWriteChunkSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    private static DateTimeOffset? GetLastInputUtc(PseudoConsoleSession session)
    {
        var field = typeof(PseudoConsoleSession).GetField("_lastInputUtc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (DateTimeOffset?)field.GetValue(session);
    }

    private static string CreateStacktraceLikeText(int lineCount)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < lineCount; i++)
        {
            builder.Append("   at Softwareschmiede.Namespace.Generic`1[[System.String]].RenderIntoBatch(RenderBatchBuilder batchBuilder, RenderFragment renderFragment, Exception& renderFragmentException) in C:\\Repos\\Projekt\\Komponente");
            builder.Append(i);
            builder.Append(".cs:line ");
            builder.Append(100 + i);
            builder.Append("\r\n");
        }

        builder.Append("ÄÖÜ äöü [] <> () {} ` end");
        return builder.ToString();
    }

    private sealed class RecordingInputStream : Stream
    {
        private readonly MemoryStream _inner = new();

        public byte[] WrittenBytes => _inner.ToArray();
        public List<int> WriteLengths { get; } = [];
        public int FlushAsyncCount { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteLengths.Add(buffer.Length);
            _inner.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushAsyncCount++;
            return Task.CompletedTask;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }

    private sealed class BlockingFirstWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly ManualResetEventSlim _firstWriteStarted = new();
        private readonly ManualResetEventSlim _releaseFirstWrite = new();
        private int _writeCalls;

        public ManualResetEventSlim FirstWriteStarted => _firstWriteStarted;
        public int WriteCalls => Volatile.Read(ref _writeCalls);
        public byte[] WrittenBytes => _inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public void ReleaseFirstWrite() => _releaseFirstWrite.Set();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _writeCalls) == 1)
            {
                _firstWriteStarted.Set();
                await Task.Run(() => _releaseFirstWrite.Wait(cancellationToken), cancellationToken);
            }

            _inner.Write(buffer.Span);
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }

    private sealed class WriteThrowingInputStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("Simulierter Schreibfehler");

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Simulierter Schreibfehler");
    }

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
}
