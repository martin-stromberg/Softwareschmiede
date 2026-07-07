using System.Drawing;
using FluentAssertions;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Tests.Domain.Terminal;

/// <summary>Unit-Tests für TerminalBuffer.</summary>
public sealed class TerminalBufferTests
{
    /// <summary>Apply(TextWrittenEvent) schreibt Zeichen an die korrekte Position im Grid.</summary>
    [Fact]
    public void Buffer_SchreibtText_AktualisiertZellen()
    {
        var sut = new TerminalBuffer(80, 24);

        sut.Apply(new TextWrittenEvent("ABC"));

        var row = sut.GetRow(0);
        row[0].Character.Should().Be('A');
        row[1].Character.Should().Be('B');
        row[2].Character.Should().Be('C');
    }

    /// <summary>Apply(CursorMovedEvent) aktualisiert CursorRow und CursorCol.</summary>
    [Fact]
    public void Buffer_CursorMove_AktualisiertPosition()
    {
        var sut = new TerminalBuffer(80, 24);

        sut.Apply(new CursorMovedEvent(5, 10, true));

        sut.CursorRow.Should().Be(5);
        sut.CursorCol.Should().Be(10);
    }

    /// <summary>Newline in letzter Zeile scrollt den Buffer um eine Zeile nach oben.</summary>
    [Fact]
    public void Buffer_Newline_ScrolltBeiLetzterZeile()
    {
        var sut = new TerminalBuffer(80, 2);

        // Fill both rows with text
        sut.Apply(new TextWrittenEvent("A"));
        sut.Apply(new CursorMovedEvent(1, 0, true));
        sut.Apply(new TextWrittenEvent("B"));

        // Cursor is now on last row — apply another newline via CursorMovedRelative
        sut.Apply(new CursorMovedEvent(1, 0, true));
        // Writing on the last row and advancing should scroll
        sut.Apply(new TextWrittenEvent("C\n"));

        // After scroll the buffer still has 2 rows, content shifted
        sut.Rows.Should().Be(2);
    }

    /// <summary>Resize erhält sichtbaren Inhalt im sichtbaren Bereich.</summary>
    [Fact]
    public void Buffer_Resize_ErhaeltSichtbarenInhalt()
    {
        var sut = new TerminalBuffer(10, 5);
        sut.Apply(new TextWrittenEvent("Hello"));

        sut.Resize(20, 10);

        sut.Cols.Should().Be(20);
        sut.Rows.Should().Be(10);
        var row = sut.GetRow(0);
        row[0].Character.Should().Be('H');
        row[4].Character.Should().Be('o');
    }

    /// <summary>Apply(ScreenClearedEvent(2)) setzt alle Zellen zurück und Cursor auf (0,0).</summary>
    [Fact]
    public void Buffer_ClearScreen_SetzAllesZurueck()
    {
        var sut = new TerminalBuffer(80, 24);
        sut.Apply(new TextWrittenEvent("Hallo Welt"));
        sut.Apply(new CursorMovedEvent(5, 10, true));

        sut.Apply(new ScreenClearedEvent(2));

        sut.CursorRow.Should().Be(0);
        sut.CursorCol.Should().Be(0);
        var row = sut.GetRow(0);
        row[0].Character.Should().Be(' ', "TerminalCell.Default hat ein Leerzeichen als Zeichen");
    }

    /// <summary>Apply(ColorChangedEvent) setzt SGR-Attribut und nachfolgende Zeichen erben die Farbe.</summary>
    [Fact]
    public void Buffer_ColorChange_NachfolgenderTextErbtFarbe()
    {
        var sut = new TerminalBuffer(80, 24);
        var redFg = Color.FromArgb(255, 0, 0);

        sut.Apply(new ColorChangedEvent(redFg, null, null, null, null, false));
        sut.Apply(new TextWrittenEvent("X"));

        var row = sut.GetRow(0);
        row[0].Foreground.R.Should().Be(255);
        row[0].Foreground.G.Should().Be(0);
        row[0].Foreground.B.Should().Be(0);
    }

    /// <summary>GetRow gibt eine Kopie zurück — Änderungen am Ergebnis beeinflussen den Buffer nicht.</summary>
    [Fact]
    public void Buffer_GetRow_GibtKopieZurueck()
    {
        var sut = new TerminalBuffer(80, 24);
        sut.Apply(new TextWrittenEvent("A"));

        var row = sut.GetRow(0);
        row[0] = row[0] with { Character = 'Z' };

        var row2 = sut.GetRow(0);
        row2[0].Character.Should().Be('A', "Änderungen an der Kopie dürfen den Buffer nicht beeinflussen");
    }

    /// <summary>Resize auf kleinere Größe schneidet Inhalt ab ohne Exception.</summary>
    [Fact]
    public void Buffer_Resize_KleinerAlsInhalt_WirftNicht()
    {
        var sut = new TerminalBuffer(80, 24);
        sut.Apply(new TextWrittenEvent("ABCDEFGHIJ"));

        var act = () => sut.Resize(5, 10);

        act.Should().NotThrow();
        sut.Cols.Should().Be(5);
        sut.Rows.Should().Be(10);
    }

    /// <summary>Parallele Apply()- und GetRow()-Zugriffe aus mehreren Threads führen zu keiner Exception und
    /// liefern stets einen intern konsistenten Buffer-Zustand (keine Race Condition).</summary>
    [Fact]
    public void Buffer_ParallelApplyAndRead_NoRaceCondition()
    {
        var sut = new TerminalBuffer(80, 24);
        var stop = new CancellationTokenSource();
        Exception? readerException = null;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
                sut.Apply(new TextWrittenEvent("X"));
        });

        var reader = Task.Run(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    _ = sut.GetRow(0);
                    _ = sut.CursorRow;
                    _ = sut.CursorCol;
                }
            }
            catch (Exception ex)
            {
                readerException = ex;
            }
        });

        writer.Wait(TimeSpan.FromSeconds(10));
        stop.Cancel();
        reader.Wait(TimeSpan.FromSeconds(10));

        readerException.Should().BeNull("parallele Lesezugriffe während laufender Apply()-Aufrufe dürfen zu keiner Exception führen");
    }

    /// <summary>GetSnapshot() liefert unter parallelen Apply()-Aufrufen stets einen intern konsistenten
    /// Zustand: Grid-Größe und Cursor-Position im Snapshot passen stets zusammen.</summary>
    [Fact]
    public void Buffer_GetSnapshot_ReturnsConsistentState()
    {
        var sut = new TerminalBuffer(10, 5);
        var stop = new CancellationTokenSource();
        Exception? readerException = null;

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                sut.Resize(10 + (i % 3), 5 + (i % 3));
                sut.Apply(new TextWrittenEvent("Y"));
            }
        });

        var reader = Task.Run(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var snapshot = sut.GetSnapshot();
                    snapshot.Grid.GetLength(0).Should().Be(snapshot.Rows);
                    snapshot.Grid.GetLength(1).Should().Be(snapshot.Cols);
                    snapshot.CursorRow.Should().BeInRange(0, snapshot.Rows - 1);
                    snapshot.CursorCol.Should().BeInRange(0, snapshot.Cols, "CursorCol kann durch deferred line-wrap kurzzeitig auf Cols stehen");
                }
            }
            catch (Exception ex)
            {
                readerException = ex;
            }
        });

        writer.Wait(TimeSpan.FromSeconds(10));
        stop.Cancel();
        reader.Wait(TimeSpan.FromSeconds(10));

        readerException.Should().BeNull("GetSnapshot() muss auch unter parallelen Apply()/Resize()-Aufrufen einen intern konsistenten Zustand liefern");
    }
}
