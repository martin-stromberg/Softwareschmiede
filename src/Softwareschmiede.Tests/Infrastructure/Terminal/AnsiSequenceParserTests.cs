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
    /// <summary>Ein chunk-übergreifendes UTF-8-Multibyte-Zeichen wird über die Decoder-Pufferung
    /// korrekt zusammengesetzt, statt in zwei Ersatzzeichen zu zerfallen.</summary>
    [Fact]
    public void Parse_Utf8UeberChunkGrenze_WirdKorrektDekodiert()
    {
        var sut = new AnsiSequenceParser();
        var bytes = Encode("€"); // E2 82 AC

        var e1 = sut.Parse(bytes[..2]).ToList();
        var e2 = sut.Parse(bytes[2..]).ToList();

        var text = string.Concat(
            e1.OfType<TextWrittenEvent>().Select(e => e.Text)
                .Concat(e2.OfType<TextWrittenEvent>().Select(e => e.Text)));
        text.Should().Be("€");
    }

    /// <summary>CSI L ergibt LinesInsertedEvent.</summary>
    [Fact]
    public void Parse_CsiL_ErgibtLinesInsertedEvent()
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode("\x1b[3L")).ToList();
        events.Should().ContainSingle(e => e is LinesInsertedEvent);
        ((LinesInsertedEvent)events[0]).Count.Should().Be(3);
    }

    /// <summary>CSI M ergibt LinesDeletedEvent.</summary>
    [Fact]
    public void Parse_CsiM_ErgibtLinesDeletedEvent()
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode("\x1b[2M")).ToList();
        events.Should().ContainSingle(e => e is LinesDeletedEvent);
        ((LinesDeletedEvent)events[0]).Count.Should().Be(2);
    }

    /// <summary>CSI @ ergibt CharsInsertedEvent, CSI P CharsDeletedEvent, CSI X CharsErasedEvent.</summary>
    [Theory]
    [InlineData("\x1b[4@", typeof(CharsInsertedEvent), 4)]
    [InlineData("\x1b[5P", typeof(CharsDeletedEvent), 5)]
    [InlineData("\x1b[6X", typeof(CharsErasedEvent), 6)]
    public void Parse_CsiZeichenOperationen_ErgebenKorrekteEvents(string seq, Type expectedType, int expectedCount)
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode(seq)).ToList();
        var matches = events.Where(e => e.GetType() == expectedType).ToList();
        matches.Should().ContainSingle();
        matches[0].GetType().GetProperty("Count")!.GetValue(matches[0]).Should().Be(expectedCount);
    }

    /// <summary>CSI r ergibt ScrollRegionChangedEvent mit den übergebenen Grenzen.</summary>
    [Fact]
    public void Parse_CsiR_ErgibtScrollRegionChangedEvent()
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode("\x1b[2;10r")).ToList();
        events.Should().ContainSingle(e => e is ScrollRegionChangedEvent);
        var evt = (ScrollRegionChangedEvent)events[0];
        evt.Top.Should().Be(1);
        evt.Bottom.Should().Be(9);
    }

    /// <summary>CSI ?1049h / ?1047h aktivieren den Alternate Screen, ?1049l kehrt zurück.</summary>
    [Theory]
    [InlineData("\x1b[?1049h", true)]
    [InlineData("\x1b[?1047h", true)]
    [InlineData("\x1b[?1049l", false)]
    [InlineData("\x1b[?1047l", false)]
    public void Parse_AltScreenSequenzen_ErgebenAlternateScreenChangedEvent(string seq, bool expectedActive)
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode(seq)).ToList();
        var altEvents = events.OfType<AlternateScreenChangedEvent>().ToList();
        altEvents.Should().ContainSingle();
        altEvents[0].Enabled.Should().Be(expectedActive);
    }

    /// <summary>ESC 7 / CSI s speichern, ESC 8 / CSI u stellen den Cursor wieder her.</summary>
    [Theory]
    [InlineData("\x1b" + "7", false)]
    [InlineData("\x1b[s", false)]
    [InlineData("\x1b" + "8", true)]
    [InlineData("\x1b[u", true)]
    public void Parse_SaveRestoreCursor_ErgibtCursorSavedEvent(string seq, bool expectedRestored)
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode(seq)).ToList();
        events.Should().ContainSingle(e => e is CursorSavedEvent);
        ((CursorSavedEvent)events[0]).Restored.Should().Be(expectedRestored);
    }

    /// <summary>ESC c ergibt TerminalResetEvent.</summary>
    [Fact]
    public void Parse_EscC_ErgibtTerminalResetEvent()
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode("\x1b" + "c")).ToList();
        events.Should().ContainSingle(e => e is TerminalResetEvent);
    }

    /// <summary>CSI S scrollt aufwärts, CSI T abwärts — als ScreenScrolledEvent mit vorzeichenbehafteter Delta.</summary>
    [Theory]
    [InlineData("\x1b[3S", 3)]
    [InlineData("\x1b[2T", -2)]
    public void Parse_ScrollUpDown_ErgebenScreenScrolledEvent(string seq, int expectedDelta)
    {
        var sut = new AnsiSequenceParser();
        var events = sut.Parse(Encode(seq)).ToList();
        events.Should().ContainSingle(e => e is ScreenScrolledEvent);
        ((ScreenScrolledEvent)events[0]).DeltaRows.Should().Be(expectedDelta);
    }
}
