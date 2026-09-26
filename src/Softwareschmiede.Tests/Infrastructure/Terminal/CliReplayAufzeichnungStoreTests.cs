using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="CliReplayAufzeichnungStore"/> (.clireplay-Binärformat).</summary>
public sealed class CliReplayAufzeichnungStoreTests
{
    /// <summary>Schreiben und Laden erhalten alle Header-Felder und jeden Chunk-Record
    /// (OffsetTicks, Länge, Bytes) exakt.</summary>
    [Fact]
    public async Task SchreibeUndLade_Roundtrip_ErhaeltHeaderUndChunks()
    {
        var store = new CliReplayAufzeichnungStore();
        var quelle = new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "Devin CLI (äöü)",
            StartUtc = new DateTimeOffset(2025, 3, 4, 10, 20, 30, TimeSpan.Zero),
            EndeUtc = new DateTimeOffset(2025, 3, 4, 11, 0, 0, TimeSpan.Zero),
            Cols = 220,
            Rows = 50,
            IstVollstaendig = false,
            Chunks =
            [
                new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("chunk-0")),
                new CliOutputChunkRecord(TimeSpan.FromMilliseconds(1234), "\x1b[31m"u8.ToArray()),
                new CliOutputChunkRecord(TimeSpan.FromSeconds(2), []),
            ],
        };

        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, quelle);
        stream.Position = 0;
        var geladen = await store.LadeAsync(stream);

        geladen.AufgabeId.Should().Be(quelle.AufgabeId);
        geladen.PluginName.Should().Be(quelle.PluginName);
        geladen.StartUtc.Should().Be(quelle.StartUtc);
        geladen.EndeUtc.Should().Be(quelle.EndeUtc);
        geladen.Cols.Should().Be(220);
        geladen.Rows.Should().Be(50);
        geladen.IstVollstaendig.Should().BeFalse();
        geladen.Chunks.Should().HaveCount(3);
        geladen.Chunks[0].Offset.Should().Be(TimeSpan.Zero);
        geladen.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("chunk-0"));
        geladen.Chunks[1].Offset.Should().Be(TimeSpan.FromMilliseconds(1234));
        geladen.Chunks[1].Data.Should().Equal("\x1b[31m"u8.ToArray());
        geladen.Chunks[2].Data.Should().BeEmpty();
    }

    /// <summary>Ein <c>EndeUtc</c> von <c>null</c> überlebt den Roundtrip (laufende Aufzeichnung).</summary>
    [Fact]
    public async Task Roundtrip_EndeUtcNull_BleibtNull()
    {
        var store = new CliReplayAufzeichnungStore();
        var quelle = CreateAufzeichnung();
        quelle = new CliOutputAufzeichnung
        {
            AufgabeId = quelle.AufgabeId,
            PluginName = quelle.PluginName,
            StartUtc = quelle.StartUtc,
            EndeUtc = null,
            Cols = quelle.Cols,
            Rows = quelle.Rows,
            IstVollstaendig = true,
            Chunks = quelle.Chunks,
        };

        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, quelle);
        stream.Position = 0;
        var geladen = await store.LadeAsync(stream);

        geladen.EndeUtc.Should().BeNull();
    }

    /// <summary>Ungültiges Magic → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_UngueltigesMagic_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("NOMAGIC!!xxxxx"));

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Nicht unterstützte Version → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_UnbekannteVersion_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream();
        await using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("SWCLRPLY"));
            writer.Write(99);
            writer.Write(new byte[16 + 8 + 8 + 4 + 4 + 1 + 4]);
        }
        stream.Position = 0;

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgebrochener Header → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_AbgeschnittenerHeader_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("SWCLRPLY"));

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Ungültige Terminal-Geometrie (Cols/Rows ≤ 0) → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_UngueltigeGeometrie_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 0,
            Rows = -5,
            IstVollstaendig = true,
            Chunks = [],
        });
        stream.Position = 0;

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgeschnittener Chunk-Record (Länge übersteigt Restdaten) → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_AbgeschnittenerChunkRecord_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = DateTimeOffset.UtcNow,
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks = [new CliOutputChunkRecord(TimeSpan.Zero, Encoding.UTF8.GetBytes("abcdef"))],
        });

        // Die letzten 3 Bytes des einzigen Chunks abschneiden.
        stream.SetLength(stream.Length - 3);
        stream.Position = 0;

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Abgebrochener Record-Header mitten in der Datei → <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public async Task LadeAsync_AbgeschnittenerRecordHeader_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, CreateAufzeichnung());

        // 5 Bytes hinter dem regulären Dateiende würden als leeres Ende erkannt werden —
        // ein abgeschnittener 12-Byte-Record-Header (1..11 Bytes) muss dagegen fehlschlagen.
        using var verkuerzt = new MemoryStream();
        verkuerzt.Write(stream.ToArray());
        verkuerzt.Write(new byte[5]);
        verkuerzt.Position = 0;

        var act = () => store.LadeAsync(verkuerzt);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Eine PluginName-Länge, die über die Restdaten der Datei hinausweist (z. B.
    /// <see cref="int.MaxValue"/> in einer korrupten Datei), muss als Formatfehler erkannt werden —
    /// nicht als gigantische Allokation.</summary>
    [Fact]
    public async Task LadeAsync_UebergrossePluginNameLaenge_WirftInvalidDataException()
    {
        var store = new CliReplayAufzeichnungStore();
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

        var act = () => store.LadeAsync(stream);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary><c>StartUtc</c>/<c>EndeUtc</c> werden UTC-normalisiert gespeichert: ein Zeitstempel
    /// mit lokalem Offset landet nach dem Roundtrip als identischer Zeitpunkt mit Offset 0.</summary>
    [Fact]
    public async Task Roundtrip_ZeitstempelMitOffset_WerdenUtcNormalisiert()
    {
        var store = new CliReplayAufzeichnungStore();
        var quelle = new CliOutputAufzeichnung
        {
            AufgabeId = Guid.NewGuid(),
            PluginName = "P",
            StartUtc = new DateTimeOffset(2025, 3, 4, 10, 20, 30, TimeSpan.FromHours(2)),
            EndeUtc = new DateTimeOffset(2025, 3, 4, 11, 0, 0, TimeSpan.FromHours(2)),
            Cols = 80,
            Rows = 24,
            IstVollstaendig = true,
            Chunks = [],
        };

        using var stream = new MemoryStream();
        await store.SpeichernAsync(stream, quelle);
        stream.Position = 0;
        var geladen = await store.LadeAsync(stream);

        geladen.StartUtc.Should().Be(quelle.StartUtc, "derselbe Zeitpunkt muss erhalten bleiben");
        geladen.StartUtc.Offset.Should().Be(TimeSpan.Zero, "die Speicherung erfolgt UTC-normalisiert");
        geladen.EndeUtc.Should().Be(quelle.EndeUtc);
        geladen.EndeUtc!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>Die Datei-Wrapper <see cref="CliReplayAufzeichnungStore.SpeichernAsync(string, CliOutputAufzeichnung, CancellationToken)"/>
    /// und <see cref="CliReplayAufzeichnungStore.LadeAsync(string, CancellationToken)"/> runden denselben
    /// Inhalt wie die Stream-Kernmethoden ab.</summary>
    [Fact]
    public async Task DateiWrapper_SpeichernUndLaden_Roundtrip()
    {
        var store = new CliReplayAufzeichnungStore();
        var pfad = Path.Combine(Path.GetTempPath(), $"clireplay-test-{Guid.NewGuid():N}.clireplay");
        try
        {
            var quelle = CreateAufzeichnung();
            await store.SpeichernAsync(pfad, quelle);

            File.Exists(pfad).Should().BeTrue();
            var geladen = await store.LadeAsync(pfad);
            geladen.AufgabeId.Should().Be(quelle.AufgabeId);
            geladen.Chunks.Should().HaveCount(1);
            geladen.Chunks[0].Data.Should().Equal(Encoding.UTF8.GetBytes("payload"));
        }
        finally
        {
            if (File.Exists(pfad))
                File.Delete(pfad);
        }
    }

    private static CliOutputAufzeichnung CreateAufzeichnung() => new()
    {
        AufgabeId = Guid.NewGuid(),
        PluginName = "TestPlugin",
        StartUtc = new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero),
        EndeUtc = new DateTimeOffset(2025, 1, 1, 8, 30, 0, TimeSpan.Zero),
        Cols = 80,
        Rows = 24,
        IstVollstaendig = true,
        Chunks = [new CliOutputChunkRecord(TimeSpan.FromMilliseconds(42), Encoding.UTF8.GetBytes("payload"))],
    };
}
