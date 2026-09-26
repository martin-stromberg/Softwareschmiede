using System.Text;
using FluentAssertions;
using Softwareschmiede.App.Services;

namespace Softwareschmiede.Tests.App.Services;

/// <summary>Unit-Tests für <see cref="CliChunkQuelltextFormatter"/>.</summary>
public sealed class CliChunkQuelltextFormatterTests
{
    /// <summary>ESC wird als ␛, CR als \r, LF als \n, TAB als \t sichtbar gemacht.</summary>
    [Fact]
    public void Formatiere_Steuerzeichen_WerdenSichtbar()
    {
        var input = Encoding.UTF8.GetBytes("a\x1b[31mb\rc\nd\te");

        var ergebnis = CliChunkQuelltextFormatter.Formatiere(input);

        ergebnis.Should().Be("a␛[31mb\\rc\\nd\\te");
    }

    /// <summary>Sonstige Steuerbytes werden als \xNN (hex, Großbuchstaben) dargestellt.</summary>
    [Fact]
    public void Formatiere_SonstigeSteuerbytes_WerdenAlsHexEscaped()
    {
        var input = new byte[] { 0x00, 0x07, 0x7F };

        var ergebnis = CliChunkQuelltextFormatter.Formatiere(input);

        ergebnis.Should().Be("\\x00\\x07\\x7F");
    }

    /// <summary>UTF-8-Mehrbyte-Sequenzen bleiben als Zeichen lesbar.</summary>
    [Fact]
    public void Formatiere_Utf8Mehrbyte_BleibtLesbar()
    {
        var input = Encoding.UTF8.GetBytes("äöü€✓");

        var ergebnis = CliChunkQuelltextFormatter.Formatiere(input);

        ergebnis.Should().Be("äöü€✓");
    }

    /// <summary>Leere und reine Text-Chunks bleiben unverändert.</summary>
    [Fact]
    public void Formatiere_LeereUndEinfacheChunks()
    {
        CliChunkQuelltextFormatter.Formatiere(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
        CliChunkQuelltextFormatter.Formatiere(Encoding.UTF8.GetBytes("plain text 123")).Should().Be("plain text 123");
    }
}
