using System.Text;
using FluentAssertions;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests fuer <see cref="CliOutputLineAccumulator"/>.</summary>
public sealed class CliOutputLineAccumulatorTests
{
    /// <summary>Mehrere LF-Zeilen in einem Chunk werden in Reihenfolge geliefert.</summary>
    [Fact]
    public void Chunks_MitMehrerenLfZeilen_LiefertZeilenInReihenfolge()
    {
        var sut = new CliOutputLineAccumulator();

        var lines = sut.Append(Encoding.UTF8.GetBytes("eins\nzwei\n"));

        lines.Should().Equal("eins", "zwei");
        sut.Flush().Should().BeEmpty();
    }

    /// <summary>Geteilte Zeilen werden ueber Chunk-Grenzen zusammengefuehrt.</summary>
    [Fact]
    public void Chunks_MitGeteilterZeile_UeberChunkGrenze_LiefertEineZeile()
    {
        var sut = new CliOutputLineAccumulator();

        sut.Append(Encoding.UTF8.GetBytes("hal")).Should().BeEmpty();
        var lines = sut.Append(Encoding.UTF8.GetBytes("lo\n"));

        lines.Should().Equal("hallo");
    }

    /// <summary>CRLF erzeugt genau einen Protokolleintrag.</summary>
    [Fact]
    public void Chunks_MitCrLf_ZaehltNichtDoppelt()
    {
        var sut = new CliOutputLineAccumulator();

        var lines = sut.Append(Encoding.UTF8.GetBytes("eins\r\nzwei\r\n"));

        lines.Should().Equal("eins", "zwei");
    }

    /// <summary>Einzelnes CR flusht Progress-Ausgaben.</summary>
    [Fact]
    public void Chunks_MitEinzelnemCr_FlushtProgressZeile()
    {
        var sut = new CliOutputLineAccumulator();

        var lines = sut.Append(Encoding.UTF8.GetBytes("10%\r20%\r"));

        lines.Should().Equal("10%", "20%");
    }

    /// <summary>UTF-8-Multibyte-Zeichen bleiben ueber Chunk-Grenzen korrekt.</summary>
    [Fact]
    public void Chunks_MitUtf8MultibyteGrenze_DekodiertKorrekt()
    {
        var sut = new CliOutputLineAccumulator();
        var bytes = Encoding.UTF8.GetBytes("Preis: 10 EUR\n".Replace("EUR", "€", StringComparison.Ordinal));

        sut.Append(bytes.AsSpan(0, 11)).Should().BeEmpty();
        var lines = sut.Append(bytes.AsSpan(11));

        lines.Should().Equal("Preis: 10 €");
    }

    /// <summary>Eine Restzeile ohne Zeilentrenner wird beim Flush geliefert.</summary>
    [Fact]
    public void Flush_MitRestzeile_SpeichertUnvollstaendigeLetzteZeile()
    {
        var sut = new CliOutputLineAccumulator();

        sut.Append(Encoding.UTF8.GetBytes("ohne ende")).Should().BeEmpty();

        sut.Flush().Should().Equal("ohne ende");
    }
}
