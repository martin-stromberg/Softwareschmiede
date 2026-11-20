using System.Text;
using FluentAssertions;
using Softwareschmiede.App.Services;

namespace Softwareschmiede.Tests.App.Services;

/// <summary>Unit-Tests für <see cref="CliChunkSourceTextFormatter"/>.</summary>
public sealed class CliChunkSourcetextFormatterTests
{
    /// <summary>ESC wird als ␛, CR als \r, LF als \n, TAB als \t sichtbar gemacht.</summary>
    [Fact]
    public void Format_Controlchars_BeVisible()
    {
        var input = Encoding.UTF8.GetBytes("a\x1b[31mb\rc\nd\te");

        var result = CliChunkSourceTextFormatter.Format(input);

        result.Should().Be("a␛[31mb\\rc\\nd\\te");
    }

    /// <summary>Sonstige Steuerbytes werden als \xNN (hex, Großbuchstaben) dargestellt.</summary>
    [Fact]
    public void Format_OtherControlbytes_BeAsHexEscaped()
    {
        var input = new byte[] { 0x00, 0x07, 0x7F };

        var result = CliChunkSourceTextFormatter.Format(input);

        result.Should().Be("\\x00\\x07\\x7F");
    }

    /// <summary>UTF-8-Mehrbyte-Sequenzen bleiben als Zeichen lesbar.</summary>
    [Fact]
    public void Format_Utf8Multibyte_RemainsReadable()
    {
        var input = Encoding.UTF8.GetBytes("äöü€✓");

        var result = CliChunkSourceTextFormatter.Format(input);

        result.Should().Be("äöü€✓");
    }

    /// <summary>Leere und reine Text-Chunks bleiben unverändert.</summary>
    [Fact]
    public void Format_EmptyAndSimpleChunks()
    {
        CliChunkSourceTextFormatter.Format(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
        CliChunkSourceTextFormatter.Format(Encoding.UTF8.GetBytes("plain text 123")).Should().Be("plain text 123");
    }
}
