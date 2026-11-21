using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="CliReplayRecordingStore"/> (.clireplay-Binärformat).</summary>
public sealed class CliReplayRecordingStoreTests
{
    /// <summary>Schreiben und Laden erhalten alle Header-Felder und jeden Chunk-Record
    /// (OffsetTicks, Länge, Bytes) exakt.</summary>
    [Fact]
    public async Task WriteAndLoad_Roundtrip_ReceivesHeaderAndChunks()
    {
        var store = new CliReplayRecordingStore();
        var source = new CliOutputRecording
        {
            WorkItemId = Guid.NewGuid(),
            PluginName = "Devin CLI (äöü)",
            StartUtc = new DateTimeOffset(2025, 3, 4, 10, 20, 30, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2025, 3, 4, 11, 0, 0, TimeSpan.Zero),
            Cols = 220,
            Rows = 50,
            IsComplete = false,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("chunk-0")),
                new CliOutputChunkRecord(TimeSpan.FromMilliseconds(1234), "\x1b[31m"u8.ToArray()),
                new CliOutputChunkRecord(TimeSpan.FromSeconds(2), []),
            ],
        };

        using var stream = new MemoryStream();
        await store.SaveAsync(stream, source, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream, TestContext.Current.CancellationToken);

        loaded.WorkItemId.Should().Be(source.WorkItemId);
        loaded.PluginName.Should().Be(source.PluginName);
        loaded.StartUtc.Should().Be(source.StartUtc);
        loaded.EndUtc.Should().Be(source.EndUtc);
        loaded.Cols.Should().Be(220);
        loaded.Rows.Should().Be(50);
        loaded.IsComplete.Should().BeFalse();
        loaded.Chunks.Should().HaveCount(3);
        loaded.Chunks[0].Offset.Should().Be(TimeSpan.Zero);
        loaded.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("chunk-0"));
        loaded.Chunks[1].Offset.Should().Be(TimeSpan.FromMilliseconds(1234));
        loaded.Chunks[1].Data.Should().Equal("\x1b[31m"u8.ToArray());
        loaded.Chunks[2].Data.Should().BeEmpty();
    }

    /// <summary>Ein <c>EndUtc</c> von <c>null</c> überlebt den Roundtrip (laufende Aufzeichnung).</summary>
    [Fact]
    public async Task Roundtrip_EndUtcNull_RemainsNull()
    {
        var store = new CliReplayRecordingStore();
        var source = CreateRecording();
        source = new CliOutputRecording
        {
            WorkItemId = source.WorkItemId,
            PluginName = source.PluginName,
            StartUtc = source.StartUtc,
            EndUtc = null,
            Cols = source.Cols,
            Rows = source.Rows,
            IsComplete = true,
            Chunks = source.Chunks,
        };

        using var stream = new MemoryStream();
        await store.SaveAsync(stream, source, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream, TestContext.Current.CancellationToken);

        loaded.EndUtc.Should().BeNull();
    }

    /// <summary>Ungültiges Magic → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_InvalidMagic_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOMAGIC!!xxxxx"));

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Nicht unterstützte Version → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_UnknownVersion_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream();
        await using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("SWCLRPLY"));
            writer.Write(99);
            writer.Write(new byte[16 + 8 + 8 + 4 + 4 + 1 + 4]);
        }
        stream.Position = 0;

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgebrochener Header → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_TruncatedHeader_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("SWCLRPLY"));

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Ungültige Terminal-Geometrie (Cols/Rows ≤ 0) → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_InvalidGeometry_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream();
        await store.SaveAsync(stream, new CliOutputRecording
        {
            WorkItemId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 0,
            Rows = -5,
            IsComplete = true,
            Chunks = [],
        }, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgeschnittener Chunk-Record (Länge übersteigt Restdaten) → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_TruncatedChunkRecord_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream();
        await store.SaveAsync(stream, new CliOutputRecording
        {
            WorkItemId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IsComplete = true,
            Chunks = [new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("abcdef"))],
        }, TestContext.Current.CancellationToken);

        // Die letzten 3 Bytes des einzigen Chunks abschneiden.
        stream.SetLength(stream.Length - 3);
        stream.Position = 0;

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgebrochener Record-Header mitten in der Datei → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LoadAsync_TruncatedRecordHeader_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream();
        await store.SaveAsync(stream, CreateRecording(), TestContext.Current.CancellationToken);

        // 5 Bytes hinter dem regulären Dateiende würden als leeres Ende erkannt werden —
        // ein abgeschnittener 12-Byte-Record-Header (1..11 Bytes) muss dagegen fehlschlagen.
        using var shortened = new MemoryStream();
        shortened.Write(stream.ToArray());
        shortened.Write(new byte[5]);
        shortened.Position = 0;

        var act = () => store.LoadAsync(shortened);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Eine PluginName-Länge, die über die Restdaten der Datei hinausweist (z. B.
    /// <see cref="int.MaxValue"/> in einer korrupten Datei), muss als Formatfehler erkannt werden —
    /// nicht als gigantische Allokation.</summary>
    [Fact]
    public async Task LoadAsync_OversizePluginNameLength_ThrowsInvalidDataException()
    {
        var store = new CliReplayRecordingStore();
        using var stream = new MemoryStream();
        await using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("SWCLRPLY"));
            writer.Write(1);
            writer.Write(Guid.NewGuid().ToByteArray());
            writer.Write(DateTimeOffset.UtcNow.UtcTicks);
            writer.Write(0L);
            writer.Write(80);
            writer.Write(24);
            writer.Write(true);
            writer.Write(int.MaxValue);
        }
        stream.Position = 0;

        var act = () => store.LoadAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary><c>StartUtc</c>/<c>EndUtc</c> werden UTC-normalisiert gespeichert: ein Zeitstempel
    /// mit lokalem Offset landet nach dem Roundtrip als identischer Zeitpunkt mit Offset 0.</summary>
    [Fact]
    public async Task Roundtrip_TimestampWithOffset_BeUtcNormalizes()
    {
        var store = new CliReplayRecordingStore();
        var source = new CliOutputRecording
        {
            WorkItemId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = new DateTimeOffset(2025, 3, 4, 10, 20, 30, TimeSpan.FromHours(2)),
            EndUtc = new DateTimeOffset(2025, 3, 4, 11, 0, 0, TimeSpan.FromHours(2)),
            Cols = 80,
            Rows = 24,
            IsComplete = true,
            Chunks = [],
        };

        using var stream = new MemoryStream();
        await store.SaveAsync(stream, source, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream, TestContext.Current.CancellationToken);

        loaded.StartUtc.Should().Be(source.StartUtc, "derselbe Zeitpunkt muss erhalten bleiben");
        loaded.StartUtc.Offset.Should().Be(TimeSpan.Zero, "die Speicherung erfolgt UTC-normalisiert");
        loaded.EndUtc.Should().Be(source.EndUtc);
        loaded.EndUtc!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>Die Datei-Wrapper <see cref="CliReplayRecordingStore.SaveAsync(string, CliOutputRecording, CancellationToken)"/>
    /// und <see cref="CliReplayRecordingStore.LoadAsync(string, CancellationToken)"/> runden denselben
    /// Inhalt wie die Stream-Kernmethoden ab.</summary>
    [Fact]
    public async Task FileWrapper_SaveAndLoad_Roundtrip()
    {
        var store = new CliReplayRecordingStore();
        var path = Path.Combine(Path.GetTempPath(), $"clireplay-test-{Guid.NewGuid():N}.clireplay");
        try
        {
            var source = CreateRecording();
            await store.SaveAsync(path, source, TestContext.Current.CancellationToken);

            File.Exists(path).Should().BeTrue();
            var loaded = await store.LoadAsync(path, TestContext.Current.CancellationToken);
            loaded.WorkItemId.Should().Be(source.WorkItemId);
            loaded.Chunks.Should().HaveCount(1);
            loaded.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("payload"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static CliOutputRecording CreateRecording() => new()
    {
        WorkItemId = Guid.NewGuid(),
        PluginName = "TestPlugin",
        StartUtc = new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero),
        EndUtc = new DateTimeOffset(2025, 1, 1, 8, 30, 0, TimeSpan.Zero),
        Cols = 80,
        Rows = 24,
        IsComplete = true,
        Chunks = [new CliOutputChunkRecord(TimeSpan.FromMilliseconds(42), Encoding.UTF8.GetBytes("payload"))],
    };
}
