using System.Drawing;
using System.Text;
using FluentAssertions;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für AnsiSequenceParser.</summary>
public sealed class AnsiSequenceParserTests
{
    private static byte[] Encode(string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>Klartext ohne Escapes ergibt TextWrittenEvent mit korrektem Text.</summary>
    [Fact]
    public void Parse_PlainText_ErgibtTextWrittenEvent()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("Hallo")).ToList();

        events.Should().ContainSingle(e => e is TextWrittenEvent);
        ((TextWrittenEvent)events[0]).Text.Should().Be("Hallo");
    }

    /// <summary>SGR-Sequenz ESC[31m ergibt ColorChangedEvent mit roter Vordergrundfarbe.</summary>
    [Fact]
    public void Parse_SgrFarbe_ErgibtColorChangedEvent()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[31m")).ToList();

        events.Should().ContainSingle(e => e is ColorChangedEvent);
        var colorEvt = (ColorChangedEvent)events[0];
        colorEvt.Foreground.Should().NotBeNull();
        colorEvt.Foreground!.Value.R.Should().Be(205);
        colorEvt.Foreground.Value.G.Should().Be(0);
        colorEvt.Foreground.Value.B.Should().Be(0);
    }

    /// <summary>SGR-Reset ESC[0m ergibt ColorChangedEvent mit Reset=true.</summary>
    [Fact]
    public void Parse_SgrReset_ErgibtColorChangedEventMitStandardfarben()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[0m")).ToList();

        events.Should().ContainSingle(e => e is ColorChangedEvent);
        var colorEvt = (ColorChangedEvent)events[0];
        colorEvt.Reset.Should().BeTrue();
    }

    /// <summary>SGR 24-Bit-Farbe ESC[38;2;100;200;50m ergibt korrekte RGB-Vordergrundfarbe.</summary>
    [Fact]
    public void Parse_Sgr24BitFarbe_WirdKorrektParsiert()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[38;2;100;200;50m")).ToList();

        events.Should().ContainSingle(e => e is ColorChangedEvent);
        var colorEvt = (ColorChangedEvent)events[0];
        colorEvt.Foreground.Should().NotBeNull();
        colorEvt.Foreground!.Value.R.Should().Be(100);
        colorEvt.Foreground.Value.G.Should().Be(200);
        colorEvt.Foreground.Value.B.Should().Be(50);
    }

    /// <summary>ESC[5;10H ergibt CursorMovedEvent mit Row=4 und Col=9 (0-basiert).</summary>
    [Fact]
    public void Parse_CursorMove_ErgibtCursorMovedEvent()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[5;10H")).ToList();

        events.Should().ContainSingle(e => e is CursorMovedEvent);
        var cursorEvt = (CursorMovedEvent)events[0];
        cursorEvt.Row.Should().Be(4);
        cursorEvt.Col.Should().Be(9);
        cursorEvt.IsAbsolute.Should().BeTrue();
    }

    /// <summary>ESC[2J ergibt ScreenClearedEvent.</summary>
    [Fact]
    public void Parse_ClearScreen_ErgibtScreenClearedEvent()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[2J")).ToList();

        events.Should().ContainSingle(e => e is ScreenClearedEvent);
    }

    /// <summary>ESC[K ergibt LineErasedEvent.</summary>
    [Fact]
    public void Parse_EraseLine_ErgibtLineErasedEvent()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[K")).ToList();

        events.Should().ContainSingle(e => e is LineErasedEvent);
    }

    /// <summary>Escape-Sequenz über zwei Parse-Aufrufe aufgeteilt wird vollständig verarbeitet.</summary>
    [Fact]
    public void Parse_MehrteiligePakete_WerdenZusammengesetzt()
    {
        var sut = new AnsiSequenceParser();

        // Split "\x1b[31m" into two parts
        var part1 = new byte[] { 0x1b, (byte)'[' };
        var part2 = Encode("31m");

        var events1 = sut.Parse(part1).ToList();
        var events2 = sut.Parse(part2).ToList();

        events1.Should().BeEmpty("die Sequenz ist noch nicht vollständig");
        events2.Should().ContainSingle(e => e is ColorChangedEvent,
            "nach dem zweiten Teil ist die Sequenz vollständig");
    }

    /// <summary>SGR-Sequenz Bold ESC[1m setzt Bold=true im ColorChangedEvent.</summary>
    [Fact]
    public void Parse_SgrBold_SetzBoldTrue()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[1m")).ToList();

        events.Should().ContainSingle(e => e is ColorChangedEvent);
        ((ColorChangedEvent)events[0]).Bold.Should().BeTrue();
    }

    /// <summary>Cursor-Sichtbarkeit ESC[?25l ergibt CursorVisibilityChangedEvent mit Visible=false.</summary>
    [Fact]
    public void Parse_CursorHide_ErgibtCursorVisibilityChangedEventFalse()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[?25l")).ToList();

        events.Should().ContainSingle(e => e is CursorVisibilityChangedEvent);
        ((CursorVisibilityChangedEvent)events[0]).Visible.Should().BeFalse();
    }

    /// <summary>Cursor-Sichtbarkeit ESC[?25h ergibt CursorVisibilityChangedEvent mit Visible=true.</summary>
    [Fact]
    public void Parse_CursorShow_ErgibtCursorVisibilityChangedEventTrue()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("\x1b[?25h")).ToList();

        events.Should().ContainSingle(e => e is CursorVisibilityChangedEvent);
        ((CursorVisibilityChangedEvent)events[0]).Visible.Should().BeTrue();
    }

    /// <summary>Text mit CRLF wird unverändert im TextWrittenEvent belassen — die Zeilenvorschub-Semantik
    /// liegt bewusst im TerminalBuffer, nicht im Parser.</summary>
    [Fact]
    public void Parse_CrLfText_ErgibtTextMitCrLf()
    {
        var sut = new AnsiSequenceParser();

        var events = sut.Parse(Encode("A\r\nB")).ToList();

        events.Should().ContainSingle(e => e is TextWrittenEvent);
        ((TextWrittenEvent)events[0]).Text.Should().Be("A\r\nB");
    }
}
