using System.Text;
using FluentAssertions;
using Softwareschmiede.App.Controls;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für <see cref="KeyToVt100Encoder.EncodeClipboardText"/>: Kodierung von
/// Zwischenablage-Text zu UTF-8-Bytes mit Newline-Normalisierung für die CLI-Eingabe.</summary>
public sealed class KeyToVt100EncoderTests
{
    /// <summary>Einzeiliger Text wird unverändert als UTF-8-Bytes kodiert.</summary>
    [Fact]
    public void EncodeClipboardText_SingleLineText_ReturnsUtf8Bytes()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText("Hello");

        result.Should().Equal(Encoding.UTF8.GetBytes("Hello"));
    }

    /// <summary>Mehrzeiliger Text mit LF (<c>\n</c>) wird zu CR (<c>\r</c>) normalisiert.</summary>
    [Fact]
    public void EncodeClipboardText_MultiLineTextWithLF_ConvertsToCarriageReturn()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText("Line1\nLine2");

        result.Should().Equal(Encoding.UTF8.GetBytes("Line1\rLine2"));
    }

    /// <summary>Mehrzeiliger Text mit CRLF (<c>\r\n</c>) wird zu einem einzelnen CR (<c>\r</c>) normalisiert.</summary>
    [Fact]
    public void EncodeClipboardText_MultiLineTextWithCRLF_ConvertsToCarriageReturn()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText("Line1\r\nLine2");

        result.Should().Equal(Encoding.UTF8.GetBytes("Line1\rLine2"));
    }

    /// <summary>Unicode-Zeichen (Umlaute, Emojis) werden korrekt als UTF-8 kodiert.</summary>
    [Fact]
    public void EncodeClipboardText_UnicodeCharacters_ReturnsValidUtf8()
    {
        const string text = "Grüße 🚀";

        var result = KeyToVt100Encoder.EncodeClipboardText(text);

        result.Should().Equal(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Ein leerer String ergibt ein leeres Byte-Array.</summary>
    [Fact]
    public void EncodeClipboardText_EmptyString_ReturnsEmptyArray()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText(string.Empty);

        result.Should().BeEmpty();
    }

    /// <summary><see langword="null"/> ergibt ein leeres Byte-Array, statt eine Exception zu werfen.</summary>
    [Fact]
    public void EncodeClipboardText_Null_ReturnsEmptyArray()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText(null);

        result.Should().BeEmpty();
    }

    /// <summary>Tabs und sonstige Sonderzeichen bleiben in der UTF-8-Kodierung erhalten.</summary>
    [Fact]
    public void EncodeClipboardText_SpecialCharactersAndTabs_PreservedInUtf8()
    {
        const string text = "a\tb\tc";

        var result = KeyToVt100Encoder.EncodeClipboardText(text);

        result.Should().Equal(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Ein alleinstehendes CR (<c>\r</c>) ohne folgendes LF bleibt als einzelnes CR erhalten
    /// (kein Duplizieren, keine Umwandlung).</summary>
    [Fact]
    public void EncodeClipboardText_LoneCarriageReturn_StaysSingleCarriageReturn()
    {
        var result = KeyToVt100Encoder.EncodeClipboardText("Line1\rLine2");

        result.Should().Equal(Encoding.UTF8.GetBytes("Line1\rLine2"));
    }
}
