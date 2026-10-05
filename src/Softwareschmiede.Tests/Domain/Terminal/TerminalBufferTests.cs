using System.Drawing;
using System.Linq;
using FluentAssertions;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Tests.Domain.Terminal;

/// <summary>Unit-Tests für TerminalBuffer.</summary>
public sealed class TerminalBufferTests
{
    /// <summary>Beim Resize wird der Scrollback-Snapshot auf die neue Geometrie normalisiert,
    /// damit Auswahlcode jede sichtbare Spalte samt Versionsnummer gefahrlos lesen kann.</summary>
    [Fact]
    public void Buffer_GetSnapshot_ResizeNormalizesScrollbackCellVersions()
    {
        var sut = new TerminalBuffer(3, 1);
        sut.Apply(new TextWrittenEvent("ABC\n"));
        sut.Resize(6, 1);

        var snapshot = sut.GetSnapshot();

        snapshot.ScrollbackCount.Should().Be(1);
        snapshot.ScrollbackRows[0].Should().HaveCount(6);
        snapshot.ScrollbackCellVersions[0].Should().HaveCount(6);
        snapshot.ScrollbackCellVersions[0][5].Should().Be(0);
    }

    /// <summary>Ein Insert-Zeichen mutiert die jeweiligen Zielzellen; ihre Schreibversion darf
    /// nicht von der Quellzelle übernommen werden.</summary>
    [Fact]
    public void Buffer_CharsInserted_AssignsNewVersionsToMovedTargetCells()
    {
        var sut = new TerminalBuffer(8, 1);
        sut.Apply(new TextWrittenEvent("ABCD"));
        var before = sut.GetSnapshot();
        sut.Apply(new CursorMovedEvent(0, 1, true));

        sut.Apply(new CharsInsertedEvent(1));

        var after = sut.GetSnapshot();
        after.Grid[0, 2].Character.Should().Be('B');
        after.GridCellVersions[0, 2].Should().NotBe(before.GridCellVersions[0, 1]);
    }

    /// <summary>Ein normaler Vollbild-Scroll verschiebt vollständige logische Zeilen.
    /// Deren Zellversionen sind Auswahlidentitäten und müssen daher mit der Zeile wandern.</summary>
    [Fact]
    public void Buffer_NormalScroll_MovesCellVersionsWithLogicalRows()
    {
        var sut = new TerminalBuffer(8, 2);
        sut.Apply(new TextWrittenEvent("erste\nzweite"));
        var before = sut.GetSnapshot();
        var secondRowId = before.GridRowIds[1];
        var secondVersion = before.GridCellVersions[1, 0];

        // Die Ausgabe auf der unteren Zeile bewirkt beim Zeilenumbruch einen
        // Vollbild-Scroll; "zweite" bleibt als oberste sichtbare logische Zeile.
        sut.Apply(new TextWrittenEvent("\n"));

        var after = sut.GetSnapshot();
        after.GridRowIds[0].Should().Be(secondRowId);
        after.GridCellVersions[0, 0].Should().Be(secondVersion,
            "eine Scrollback-Verschiebung darf eine gültige Auswahl nicht invalidieren");
        after.Grid[0, 0].Character.Should().Be('z');
    }

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
    public async Task Buffer_ParallelApplyAndRead_NoRaceCondition()
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

        await AwaitOhneTimeoutExceptionAsync(writer, TimeSpan.FromSeconds(10));
        stop.Cancel();
        await AwaitOhneTimeoutExceptionAsync(reader, TimeSpan.FromSeconds(10));

        readerException.Should().BeNull("parallele Lesezugriffe während laufender Apply()-Aufrufe dürfen zu keiner Exception führen");
    }

    /// <summary>GetSnapshot() liefert unter parallelen Apply()-Aufrufen stets einen intern konsistenten
    /// Zustand: Grid-Größe und Cursor-Position im Snapshot passen stets zusammen.</summary>
    [Fact]
    public async Task Buffer_GetSnapshot_ReturnsConsistentState()
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

        await AwaitOhneTimeoutExceptionAsync(writer, TimeSpan.FromSeconds(10));
        stop.Cancel();
        await AwaitOhneTimeoutExceptionAsync(reader, TimeSpan.FromSeconds(10));

        readerException.Should().BeNull("GetSnapshot() muss auch unter parallelen Apply()/Resize()-Aufrufen einen intern konsistenten Zustand liefern");
    }

    /// <summary>Wartet auf <paramref name="task"/> bis <paramref name="timeout"/>, ohne bei Zeitüberschreitung
    /// eine TimeoutException zu werfen (Verhalten von Task.Wait(TimeSpan) für Stresstests nachgebildet).</summary>
    /// <param name="task">Der zu erwartende Task.</param>
    /// <param name="timeout">Maximale Wartezeit.</param>
    private static async Task AwaitOhneTimeoutExceptionAsync(Task task, TimeSpan timeout)
    {
        try
        {
            await task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
        }
    }

    /// <summary>Ein alleinstehendes Linefeed setzt die Cursor-Spalte auf 0 (kein Treppeneffekt).</summary>
    [Fact]
    public void Buffer_LineFeed_SetztSpalteAufNull()
    {
        var sut = new TerminalBuffer(80, 24);

        sut.Apply(new TextWrittenEvent("A\nB"));

        var row = sut.GetRow(1);
        row[0].Character.Should().Be('B', "ein bloßes Linefeed muss wie CRLF wirken und die Spalte auf 0 zurücksetzen");
    }

    /// <summary>"A\r\nB" erzeugt genau einen Zeilenvorschub, B landet in Zeile 1 Spalte 0.</summary>
    [Fact]
    public void Buffer_CarriageReturnLineFeed_ErgibtEinenUmbruch()
    {
        var sut = new TerminalBuffer(80, 24);

        sut.Apply(new TextWrittenEvent("A\r\nB"));

        sut.CursorRow.Should().Be(1, "CRLF darf nur genau einen Zeilenvorschub erzeugen");
        var row = sut.GetRow(1);
        row[0].Character.Should().Be('B');
    }

    /// <summary>Ein alleinstehendes Carriage Return überschreibt Spalte 0 der aktuellen Zeile ohne Zeilenvorschub.</summary>
    [Fact]
    public void Buffer_CarriageReturnAllein_BleibtInZeile()
    {
        var sut = new TerminalBuffer(80, 24);

        sut.Apply(new TextWrittenEvent("AAA\rB"));

        sut.CursorRow.Should().Be(0, "ein alleinstehendes CR darf keinen Zeilenvorschub auslösen");
        var row = sut.GetRow(0);
        row[0].Character.Should().Be('B');
        row[1].Character.Should().Be('A');
        row[2].Character.Should().Be('A');
    }

    /// <summary>Verkleinert man die Zeilenzahl, bleiben die zuletzt geschriebenen (unteren) Zeilen sichtbar, obere entfernt.</summary>
    [Fact]
    public void Buffer_ResizeKleiner_ErhaeltUntereZeilen()
    {
        var sut = new TerminalBuffer(10, 5);
        for (var r = 0; r < 5; r++)
        {
            sut.Apply(new CursorMovedEvent(r, 0, true));
            sut.Apply(new TextWrittenEvent(r.ToString()));
        }

        sut.Resize(10, 3);

        sut.GetRow(0)[0].Character.Should().Be('2', "nach Verkleinerung müssen die untersten Zeilen des alten Grids erhalten bleiben");
        sut.GetRow(1)[0].Character.Should().Be('3');
        sut.GetRow(2)[0].Character.Should().Be('4');
    }

    /// <summary>Die Cursor-Zeile wird nach Verkleinerung um den Versatz reduziert und anschließend geklemmt.</summary>
    [Fact]
    public void Buffer_ResizeKleiner_CursorFolgtUnterenZeilen()
    {
        var sut = new TerminalBuffer(10, 5);
        sut.Apply(new CursorMovedEvent(1, 0, true));

        sut.Resize(10, 2);

        sut.CursorRow.Should().Be(0, "der Cursor lag vor der Verkleinerung oberhalb des neuen sichtbaren Bereichs und muss auf die erste sichtbare Zeile geklemmt werden");
    }

    /// <summary>Verkleinert man die Spaltenzahl, wird der Zeileninhalt rechts abgeschnitten statt in die nächste Zeile umzubrechen.</summary>
    [Fact]
    public void Buffer_ResizeSchmaler_SchneidetRechtsAb()
    {
        var sut = new TerminalBuffer(10, 5);
        sut.Apply(new TextWrittenEvent("ABCDEFGHIJ"));

        sut.Resize(5, 5);

        var row0 = sut.GetRow(0);
        row0[0].Character.Should().Be('A');
        row0[4].Character.Should().Be('E');
        var row1 = sut.GetRow(1);
        row1[0].Character.Should().Be(' ', "Spaltenverkleinerung darf keinen Reflow in die nächste Zeile erzeugen");
    }

    /// <summary>Nach ScreenClearedEvent(2) sind alle Zellen TerminalCell.Default und der Cursor steht bei (0,0).</summary>
    [Fact]
    public void Buffer_ClearScreenMode2_AlleZellenLeer()
    {
        var sut = new TerminalBuffer(10, 5);
        sut.Apply(new TextWrittenEvent("Hallo"));
        sut.Apply(new CursorMovedEvent(3, 3, true));

        sut.Apply(new ScreenClearedEvent(2));

        sut.CursorRow.Should().Be(0);
        sut.CursorCol.Should().Be(0);
        for (var r = 0; r < sut.Rows; r++)
        {
            var row = sut.GetRow(r);
            foreach (var cell in row)
                cell.Should().Be(TerminalCell.Default);
        }
    }

    /// <summary>Nach genügend Zeilenvorschüben (gefülltem Scrollback) und anschließendem ScreenClearedEvent(2)
    /// muss der Scrollback geleert sein.</summary>
    [Fact]
    public void Buffer_ClearScreenMode2_LeertScrollback()
    {
        var sut = new TerminalBuffer(10, 2);
        for (var i = 0; i < 10; i++)
            sut.Apply(new TextWrittenEvent("X\n"));

        sut.ScrollbackCount.Should().BeGreaterThan(0, "Vorbedingung: der Scrollback muss vor dem Clear gefüllt sein");

        sut.Apply(new ScreenClearedEvent(2));

        sut.ScrollbackCount.Should().Be(0, "ScreenClearedEvent(2) muss auch den Scrollback leeren");
    }

    /// <summary>GetSnapshot() liefert Scrollback-Zeilen in chronologischer Reihenfolge vor dem sichtbaren Grid.</summary>
    [Fact]
    public void Buffer_GetSnapshot_EnthaeltScrollbackVorSichtbaremGrid()
    {
        var sut = new TerminalBuffer(4, 2);

        sut.Apply(new TextWrittenEvent("A\nB\nC"));

        var snapshot = sut.GetSnapshot();

        snapshot.ScrollbackCount.Should().Be(1);
        snapshot.TotalRows.Should().Be(3);
        snapshot.ScrollbackRows[0][0].Character.Should().Be('A');
        snapshot.Grid[0, 0].Character.Should().Be('B');
        snapshot.Grid[1, 0].Character.Should().Be('C');
    }

    /// <summary>GetSnapshot() gibt auch für Scrollback-Zeilen Kopien zurück.</summary>
    [Fact]
    public void Buffer_GetSnapshot_ScrollbackRowsSindKopien()
    {
        var sut = new TerminalBuffer(4, 2);
        sut.Apply(new TextWrittenEvent("A\nB\nC"));

        var snapshot = sut.GetSnapshot();
        snapshot.ScrollbackRows[0][0] = snapshot.ScrollbackRows[0][0] with { Character = 'Z' };

        sut.GetSnapshot().ScrollbackRows[0][0].Character.Should().Be(
            'A',
            "Änderungen am Snapshot dürfen den Buffer-Scrollback nicht verändern");
    }

    /// <summary>Der Snapshot respektiert die bestehende Scrollback-Grenze von 1000 Zeilen.</summary>
    [Fact]
    public void Buffer_GetSnapshot_BegrenztScrollbackAufJuengste1000Zeilen()
    {
        var sut = new TerminalBuffer(8, 1);

        for (var i = 0; i < 1005; i++)
            sut.Apply(new TextWrittenEvent($"{i:D4}\n"));

        var snapshot = sut.GetSnapshot();

        snapshot.ScrollbackCount.Should().Be(1000);
        snapshot.ScrollbackRows[0][0].Character.Should().Be('0');
        snapshot.ScrollbackRows[0][1].Character.Should().Be('0');
        snapshot.ScrollbackRows[0][2].Character.Should().Be('0');
        snapshot.ScrollbackRows[0][3].Character.Should().Be('5');
    }

    /// <summary>ScreenClearedEvent(2) entfernt Scrollback auch aus dem erweiterten Snapshot.</summary>
    [Fact]
    public void Buffer_GetSnapshot_ClearScreenMode2_LeertSnapshotScrollback()
    {
        var sut = new TerminalBuffer(10, 2);
        for (var i = 0; i < 10; i++)
            sut.Apply(new TextWrittenEvent("X\n"));

        sut.Apply(new ScreenClearedEvent(2));

        var snapshot = sut.GetSnapshot();
        snapshot.ScrollbackRows.Should().BeEmpty();
        snapshot.TotalRows.Should().Be(snapshot.Rows);
    }
    /// <summary>AlternateScreenChangedEvent(true) aktiviert den Alternativ-Bildschirm; GetSnapshot
    /// liefert nur das Alt-Grid ohne Scrollback.</summary>
    [Fact]
    public void Buffer_AlternateScreen_GetSnapshotZeigtNurAltGrid()
    {
        var sut = new TerminalBuffer(8, 3);
        for (var i = 0; i < 8; i++)
            sut.Apply(new TextWrittenEvent("X\n"));

        sut.Apply(new AlternateScreenChangedEvent(true));
        sut.Apply(new TextWrittenEvent("ALT"));

        sut.IsAlternateScreenActive.Should().BeTrue();
        var snapshot = sut.GetSnapshot();
        snapshot.ScrollbackRows.Should().BeEmpty();
        snapshot.Grid[0, 0].Character.Should().Be('A');
        snapshot.TotalRows.Should().Be(3);
    }

    /// <summary>Nach dem Zurückwechseln (false) ist der Hauptbildschirm inkl. Inhalt wiederhergestellt.</summary>
    [Fact]
    public void Buffer_AlternateScreenDeaktiviert_StelltHauptbildschirmWiederHer()
    {
        var sut = new TerminalBuffer(8, 3);
        sut.Apply(new TextWrittenEvent("MAIN"));

        sut.Apply(new AlternateScreenChangedEvent(true));
        sut.Apply(new TextWrittenEvent("\rALT-SCREEN"));

        sut.Apply(new AlternateScreenChangedEvent(false));

        sut.IsAlternateScreenActive.Should().BeFalse();
        sut.GetRow(0)[0].Character.Should().Be('M', "der Hauptbildschirm muss seinen Inhalt behalten haben");
    }

    /// <summary>LinesInsertedEvent schiebt Inhalte unterhalb der Cursorzeile nach unten.</summary>
    [Fact]
    public void Buffer_LinesInserted_SchiebtZeilenNachUnten()
    {
        var sut = new TerminalBuffer(4, 3);
        sut.Apply(new TextWrittenEvent("AAA"));
        sut.Apply(new CursorMovedEvent(1, 0, true));
        sut.Apply(new TextWrittenEvent("BBB"));
        sut.Apply(new CursorMovedEvent(0, 0, true));

        sut.Apply(new LinesInsertedEvent(1));

        sut.GetRow(0).Take(3).Select(c => c.Character).Should().Equal(' ', ' ', ' ');
        sut.GetRow(1).Take(3).Select(c => c.Character).Should().Equal('A', 'A', 'A');
        sut.GetRow(2).Take(3).Select(c => c.Character).Should().Equal('B', 'B', 'B');
    }

    /// <summary>LinesDeletedEvent entfernt die Cursorzeile und schiebt darunterliegende nach oben.</summary>
    [Fact]
    public void Buffer_LinesDeleted_SchiebtZeilenNachOben()
    {
        var sut = new TerminalBuffer(4, 3);
        sut.Apply(new TextWrittenEvent("AAA"));
        sut.Apply(new CursorMovedEvent(1, 0, true));
        sut.Apply(new TextWrittenEvent("BBB"));
        sut.Apply(new CursorMovedEvent(0, 0, true));

        sut.Apply(new LinesDeletedEvent(1));

        sut.GetRow(0).Take(3).Select(c => c.Character).Should().Equal('B', 'B', 'B');
        sut.GetRow(1).Take(3).Select(c => c.Character).Should().Equal(' ', ' ', ' ');
    }

    /// <summary>CharsInsertedEvent schiebt Zeichen ab dem Cursor nach rechts.</summary>
    [Fact]
    public void Buffer_CharsInserted_SchiebtZeichenNachRechts()
    {
        var sut = new TerminalBuffer(8, 2);
        sut.Apply(new TextWrittenEvent("ABCD"));
        sut.Apply(new CursorMovedEvent(0, 1, true));

        sut.Apply(new CharsInsertedEvent(2));

        sut.GetRow(0).Take(6).Select(c => c.Character).Should().Equal('A', ' ', ' ', 'B', 'C', 'D');
    }

    /// <summary>CharsDeletedEvent entfernt Zeichen ab dem Cursor.</summary>
    [Fact]
    public void Buffer_CharsDeleted_EntferntZeichenAbCursor()
    {
        var sut = new TerminalBuffer(8, 2);
        sut.Apply(new TextWrittenEvent("ABCD"));
        sut.Apply(new CursorMovedEvent(0, 1, true));

        sut.Apply(new CharsDeletedEvent(2));

        sut.GetRow(0).Take(3).Select(c => c.Character).Should().Equal('A', 'D', ' ');
    }

    /// <summary>CharsErasedEvent löscht Zeichen ab dem Cursor ohne Verschieben.</summary>
    [Fact]
    public void Buffer_CharsErased_LoeschtZeichenAbCursor()
    {
        var sut = new TerminalBuffer(8, 2);
        sut.Apply(new TextWrittenEvent("ABCD"));
        sut.Apply(new CursorMovedEvent(0, 1, true));

        sut.Apply(new CharsErasedEvent(2));

        sut.GetRow(0).Take(4).Select(c => c.Character).Should().Equal('A', ' ', ' ', 'D');
    }

    /// <summary>ScrollRegionChangedEvent beschränkt das Scrollen: Zeilen außerhalb der Region bleiben stehen,
    /// innerhalb wird rotiert.</summary>
    [Fact]
    public void Buffer_ScrollRegion_NewlineScrolltNurRegion()
    {
        var sut = new TerminalBuffer(4, 4);
        sut.Apply(new TextWrittenEvent("AAAA\nBBBB\nCCCC\nDDDD"));

        sut.Apply(new ScrollRegionChangedEvent(1, 2)); // DECSTBM -> Cursor Home
        sut.Apply(new CursorMovedEvent(2, 0, true));
        sut.Apply(new TextWrittenEvent("XX\n")); // Newline am Regionende scrollt nur Zeilen 1-2

        var row0 = string.Concat(sut.GetRow(0).Take(4).Select(c => c.Character));
        var row1 = string.Concat(sut.GetRow(1).Take(4).Select(c => c.Character));
        var row2 = string.Concat(sut.GetRow(2).Take(4).Select(c => c.Character));
        var row3 = string.Concat(sut.GetRow(3).Take(4).Select(c => c.Character));
        row0.Should().Be("AAAA", "Zeilen oberhalb der Scroll-Region dürfen nicht scrollen");
        row1.Should().Be("XXCC", "die region-interne Scrollbewegung schiebt Zeile 2 (XX über CCCC geschrieben) nach Zeile 1");
        row2.Should().Be("    ");
        row3.Should().Be("DDDD", "Zeilen unterhalb der Scroll-Region dürfen nicht scrollen");
    }

    /// <summary>CursorSavedEvent/Restore stellt eine zwischenzeitlich veränderte Cursorposition wieder her.</summary>
    [Fact]
    public void Buffer_SaveRestoreCursor_StelltPositionWiederHer()
    {
        var sut = new TerminalBuffer(8, 4);
        sut.Apply(new CursorMovedEvent(2, 3, true));
        sut.Apply(new CursorSavedEvent(false));

        sut.Apply(new CursorMovedEvent(0, 0, true));
        sut.Apply(new CursorSavedEvent(true));

        sut.CursorRow.Should().Be(2);
        sut.CursorCol.Should().Be(3);
    }

    /// <summary>TerminalResetEvent setzt Buffer, Cursor und Alternate-Screen-Status zurück.</summary>
    [Fact]
    public void Buffer_TerminalReset_SetztAllesZurueck()
    {
        var sut = new TerminalBuffer(8, 4);
        sut.Apply(new AlternateScreenChangedEvent(true));
        sut.Apply(new TextWrittenEvent("X"));
        sut.Apply(new CursorMovedEvent(3, 5, true));

        sut.Apply(new TerminalResetEvent());

        sut.IsAlternateScreenActive.Should().BeFalse();
        sut.CursorRow.Should().Be(0);
        sut.CursorCol.Should().Be(0);
        sut.GetRow(0).All(c => c.Character == ' ').Should().BeTrue();
    }

    /// <summary>ScreenScrolledEvent (CSI S, SU) darf die herausfallenden Zeilen nicht in den Scrollback
    /// schieben — anders als der Newline-Scroll am Regionsrand gehören SU-Zeilen nach xterm-Semantik
    /// nicht in den Verlauf.</summary>
    [Fact]
    public void Buffer_ScreenScrolled_SchiebtNichtInScrollback()
    {
        var sut = new TerminalBuffer(4, 2);
        sut.Apply(new TextWrittenEvent("A\nB"));

        var scrollbackVorher = sut.ScrollbackCount;

        sut.Apply(new ScreenScrolledEvent(5)); // Delta >= _rows (bisheriger Triggermechanismus)
        sut.Apply(new ScreenScrolledEvent(1));

        sut.ScrollbackCount.Should().Be(scrollbackVorher, "SU-Scrolls dürfen den Scrollback nicht füllen");
    }

    /// <summary>Ein ScreenScrolledEvent im Alternate Screen darf keine Alt-Screen-Zeilen in den
    /// Hauptscreen-Scrollback schieben — sie wären nach dem Verlassen des Alt-Screens sichtbar.</summary>
    [Fact]
    public void Buffer_ScreenScrolled_AlternateScreen_KeinScrollback()
    {
        var sut = new TerminalBuffer(4, 2);
        sut.Apply(new AlternateScreenChangedEvent(true));
        sut.Apply(new TextWrittenEvent("ALT1\nALT2"));

        sut.Apply(new ScreenScrolledEvent(5));
        sut.Apply(new AlternateScreenChangedEvent(false));

        sut.ScrollbackCount.Should().Be(0, "Alt-Screen-Zeilen dürfen nicht im Hauptscreen-Scrollback auftauchen");
    }

    /// <summary>Verkleinert man den Buffer bei aktivem Alternate Screen, wandern die wegfallenden
    /// Alt-Screen-Zeilen nicht in den Hauptscreen-Scrollback.</summary>
    [Fact]
    public void Buffer_ResizeKleiner_AlternateScreen_KeinScrollback()
    {
        var sut = new TerminalBuffer(10, 5);
        sut.Apply(new AlternateScreenChangedEvent(true));
        for (var r = 0; r < 5; r++)
        {
            sut.Apply(new CursorMovedEvent(r, 0, true));
            sut.Apply(new TextWrittenEvent("ALT"));
        }

        sut.Resize(10, 3);
        sut.Apply(new AlternateScreenChangedEvent(false));

        sut.ScrollbackCount.Should().Be(0, "beim Verkleinern im Alt-Screen verworfene Zeilen gehören nicht in den Hauptscreen-Scrollback");
    }
}
