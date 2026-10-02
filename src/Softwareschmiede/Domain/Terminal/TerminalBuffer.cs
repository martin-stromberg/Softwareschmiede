using System.Drawing;

namespace Softwareschmiede.Domain.Terminal;

/// <summary>Zustandsbehafteter Terminal-Buffer: verwaltet ein 2D-Grid aus <see cref="TerminalCell"/>-Werten,
/// Cursor-Position, SGR-Attribut-Zustand und einen Scrollback-Ringpuffer.</summary>
public sealed class TerminalBuffer
{
    private readonly object _lock = new();
    private TerminalCell[,] _grid;
    private int _cols;
    private int _rows;
    private int _cursorRow;
    private int _cursorCol;
    private readonly Queue<TerminalCell[]> _scrollback = new();
    private const int MaxScrollbackLines = 1000;

    private int _scrollTop;
    private int _scrollBottom;
    private int _savedCursorRow;
    private int _savedCursorCol;
    private bool _isAlternateScreen;
    private TerminalCell[,]? _mainScreenGrid;

    private Color _currentForeground = Color.FromArgb(229, 229, 229);
    private Color _currentBackground = Color.Black;
    private bool _currentBold;
    private bool _currentDim;
    private bool _currentUnderline;

    /// <summary>Erstellt einen neuen Terminal-Buffer mit der angegebenen Größe.</summary>
    /// <param name="cols">Spaltenanzahl.</param>
    /// <param name="rows">Zeilenanzahl.</param>
    public TerminalBuffer(int cols, int rows)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        _grid = new TerminalCell[_rows, _cols];
        _scrollBottom = _rows - 1;
        FillGrid(_grid, _rows, _cols);
    }

    /// <summary>Gibt an, ob der Alternate Screen (Vollbild-TUI, DECSET 47/1047/1049) aktiv ist.</summary>
    public bool IsAlternateScreenActive
    {
        get { lock (_lock) return _isAlternateScreen; }
    }

    /// <summary>Aktuelle Zeilenanzahl des Buffers.</summary>
    public int Rows
    {
        get { lock (_lock) return _rows; }
    }

    /// <summary>Aktuelle Spaltenanzahl des Buffers.</summary>
    public int Cols
    {
        get { lock (_lock) return _cols; }
    }

    /// <summary>Aktuelle Cursor-Zeile (0-basiert).</summary>
    public int CursorRow
    {
        get { lock (_lock) return _cursorRow; }
    }

    /// <summary>Aktuelle Cursor-Spalte (0-basiert).</summary>
    public int CursorCol
    {
        get { lock (_lock) return _cursorCol; }
    }

    /// <summary>Anzahl der aktuell im Scrollback-Ringpuffer gehaltenen Zeilen. Nur für Tests sichtbar.</summary>
    internal int ScrollbackCount
    {
        get { lock (_lock) return _scrollback.Count; }
    }

    /// <summary>Wendet ein Terminal-Ereignis auf den Buffer an.</summary>
    /// <param name="evt">Das anzuwendende Ereignis.</param>
    public void Apply(TerminalEvent evt)
    {
        lock (_lock)
        {
            switch (evt)
            {
                case TextWrittenEvent e:
                    ApplyText(e.Text);
                    break;
                case CursorMovedEvent e:
                    if (e.IsAbsolute)
                    {
                        if (e.Row >= 0) _cursorRow = Clamp(e.Row, 0, _rows - 1);
                        if (e.Col >= 0) _cursorCol = Clamp(e.Col, 0, _cols - 1);
                    }
                    else
                    {
                        _cursorRow = Clamp(_cursorRow + e.Row, 0, _rows - 1);
                        _cursorCol = Clamp(_cursorCol + e.Col, 0, _cols - 1);
                    }
                    break;
                case CursorMovedRelativeEvent e:
                    _cursorRow = Clamp(_cursorRow + e.DeltaRow, 0, _rows - 1);
                    _cursorCol = Clamp(_cursorCol + e.DeltaCol, 0, _cols - 1);
                    break;
                case ColorChangedEvent e:
                    ApplyColor(e);
                    break;
                case ScreenClearedEvent e:
                    ApplyClearScreen(e.Mode);
                    break;
                case LineErasedEvent e:
                    ApplyEraseLine(e.Mode);
                    break;
                case CursorVisibilityChangedEvent:
                    break;
                case AlternateScreenChangedEvent e:
                    ApplyAlternateScreen(e.Enabled);
                    break;
                case LinesInsertedEvent e:
                    ApplyInsertLines(e.Count);
                    break;
                case LinesDeletedEvent e:
                    ApplyDeleteLines(e.Count);
                    break;
                case CharsInsertedEvent e:
                    ApplyInsertChars(e.Count);
                    break;
                case CharsDeletedEvent e:
                    ApplyDeleteChars(e.Count);
                    break;
                case CharsErasedEvent e:
                    ApplyEraseChars(e.Count);
                    break;
                case ScrollRegionChangedEvent e:
                    ApplyScrollRegion(e.Top, e.Bottom);
                    break;
                case ScreenScrolledEvent e:
                    // CSI S (SU) gehört nach xterm-Semantik grundsätzlich nicht in den Scrollback —
                    // anders als der Newline-Scroll am Regionsrand (siehe AdvanceLine) wandern die
                    // herausfallenden Zeilen hier nicht in den Hauptscreen-Scrollback.
                    if (e.DeltaRows > 0)
                        ScrollRangeUp(_scrollTop, _scrollBottom, e.DeltaRows, pushToScrollback: false);
                    else if (e.DeltaRows < 0)
                        ScrollRangeDown(_scrollTop, _scrollBottom, -e.DeltaRows);
                    break;
                case CursorSavedEvent e:
                    ApplyCursorSaved(e.Restored);
                    break;
                case TerminalResetEvent:
                    Reset();
                    break;
            }
        }
    }

    /// <summary>Setzt den Buffer vollständig zurück: leeres Grid, leerer Scrollback, Cursor an Home,
    /// SGR-Attribute auf Standard, volle Scroll-Region, Alternate Screen deaktiviert.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _isAlternateScreen = false;
            _mainScreenGrid = null;
            _cursorRow = 0;
            _cursorCol = 0;
            _savedCursorRow = 0;
            _savedCursorCol = 0;
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            _currentForeground = TerminalCell.Default.Foreground;
            _currentBackground = TerminalCell.Default.Background;
            _currentBold = false;
            _currentDim = false;
            _currentUnderline = false;
            ClearAllCells();
        }
    }

    /// <summary>Passt die Buffer-Größe an und erhält den sichtbaren Inhalt soweit möglich.</summary>
    /// <param name="cols">Neue Spaltenanzahl.</param>
    /// <param name="rows">Neue Zeilenanzahl.</param>
    public void Resize(int cols, int rows)
    {
        lock (_lock)
        {
            cols = Math.Max(1, cols);
            rows = Math.Max(1, rows);

            var newGrid = new TerminalCell[rows, cols];
            FillGrid(newGrid, rows, cols);
            var copyCols = Math.Min(_cols, cols);

            if (rows < _rows)
            {
                var offset = _rows - rows;
                // Im Alternate Screen haben die wegfallenden Zeilen keinen Scrollback-Anspruch —
                // sie gehören nicht zum Hauptscreen und dürfen dessen Scrollback nicht füllen.
                if (!_isAlternateScreen)
                    for (var r = 0; r < offset; r++)
                        PushToScrollback(CaptureRow(r));

                for (var r = 0; r < rows; r++)
                    for (var c = 0; c < copyCols; c++)
                        newGrid[r, c] = _grid[r + offset, c];

                _cursorRow = Clamp(_cursorRow - offset, 0, rows - 1);
            }
            else
            {
                var copyRows = Math.Min(_rows, rows);
                for (var r = 0; r < copyRows; r++)
                    for (var c = 0; c < copyCols; c++)
                        newGrid[r, c] = _grid[r, c];

                _cursorRow = Clamp(_cursorRow, 0, rows - 1);
            }

            _grid = newGrid;
            _cols = cols;
            _rows = rows;
            _cursorCol = Clamp(_cursorCol, 0, _cols - 1);
            _scrollTop = Clamp(_scrollTop, 0, _rows - 1);
            _scrollBottom = Clamp(_scrollBottom, 0, _rows - 1);
            if (_scrollBottom <= _scrollTop)
            {
                _scrollTop = 0;
                _scrollBottom = _rows - 1;
            }
        }
    }

    /// <summary>Gibt eine Kopie der Zellen einer Zeile zurück.</summary>
    /// <param name="rowIndex">Zeilenindex (0-basiert).</param>
    /// <returns>Ein Array der Zellen in der angegebenen Zeile.</returns>
    public TerminalCell[] GetRow(int rowIndex)
    {
        lock (_lock)
        {
            if (rowIndex < 0 || rowIndex >= _rows)
                return Array.Empty<TerminalCell>();

            var result = new TerminalCell[_cols];
            for (var c = 0; c < _cols; c++)
                result[c] = _grid[rowIndex, c];
            return result;
        }
    }

    private void ApplyText(string text)
    {
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\r':
                    _cursorCol = 0;
                    break;
                case '\n':
                    NewLine();
                    break;
                case '\x08':
                    if (_cursorCol > 0) _cursorCol--;
                    break;
                case '\t':
                    _cursorCol = Math.Min(_cursorCol + (8 - _cursorCol % 8), _cols - 1);
                    break;
                case '\x07':
                    break;
                default:
                    if (_cursorCol >= _cols)
                        NewLine();
                    _grid[_cursorRow, _cursorCol] = new TerminalCell
                    {
                        Character = ch,
                        Foreground = _currentForeground,
                        Background = _currentBackground,
                        Bold = _currentBold,
                        Dim = _currentDim,
                        Underline = _currentUnderline,
                    };
                    _cursorCol++;
                    break;
            }
        }
    }

    private void NewLine()
    {
        AdvanceLine();
        _cursorCol = 0;
    }

    private void AdvanceLine()
    {
        if (_cursorRow == _scrollBottom)
        {
            // Zeilenumbruch am unteren Rand der Scroll-Region: Inhalt innerhalb der Region
            // nach oben scrollen. Nur bei voller Region (Hauptscreen) landen die herausfallenden
            // Zeilen im Scrollback — im Alternate Screen und bei eingeschränkter Region nicht.
            ScrollRangeUp(_scrollTop, _scrollBottom, 1, IsFullScrollRegion() && !_isAlternateScreen);
        }
        else if (_cursorRow < _rows - 1)
        {
            _cursorRow++;
        }
    }

    private bool IsFullScrollRegion() => _scrollTop == 0 && _scrollBottom == _rows - 1;

    private void ScrollRangeUp(int rangeTop, int rangeBottom, int count, bool pushToScrollback)
    {
        if (rangeBottom < rangeTop)
            return;

        count = Math.Min(count, rangeBottom - rangeTop + 1);

        if (pushToScrollback)
            for (var i = 0; i < count; i++)
                PushToScrollback(CaptureRow(rangeTop + i));

        for (var r = rangeTop; r <= rangeBottom - count; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r, c] = _grid[r + count, c];

        for (var r = rangeBottom - count + 1; r <= rangeBottom; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r, c] = TerminalCell.Default;
    }

    private void ScrollRangeDown(int rangeTop, int rangeBottom, int count)
    {
        if (rangeBottom < rangeTop)
            return;

        count = Math.Min(count, rangeBottom - rangeTop + 1);

        for (var r = rangeBottom; r >= rangeTop + count; r--)
            for (var c = 0; c < _cols; c++)
                _grid[r, c] = _grid[r - count, c];

        for (var r = rangeTop; r < rangeTop + count; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r, c] = TerminalCell.Default;
    }

    private void ApplyInsertLines(int count)
    {
        // IL wirkt nur, wenn der Cursor innerhalb der Scroll-Region steht.
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        ScrollRangeDown(_cursorRow, _scrollBottom, count);
    }

    private void ApplyDeleteLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        ScrollRangeUp(_cursorRow, _scrollBottom, count, pushToScrollback: false);
    }

    private void ApplyInsertChars(int count)
    {
        count = Math.Min(Math.Max(1, count), _cols - _cursorCol);
        for (var c = _cols - 1; c >= _cursorCol + count; c--)
            _grid[_cursorRow, c] = _grid[_cursorRow, c - count];
        for (var c = _cursorCol; c < _cursorCol + count; c++)
            _grid[_cursorRow, c] = TerminalCell.Default;
    }

    private void ApplyDeleteChars(int count)
    {
        count = Math.Min(Math.Max(1, count), _cols - _cursorCol);
        for (var c = _cursorCol; c <= _cols - 1 - count; c++)
            _grid[_cursorRow, c] = _grid[_cursorRow, c + count];
        for (var c = _cols - count; c < _cols; c++)
            _grid[_cursorRow, c] = TerminalCell.Default;
    }

    private void ApplyEraseChars(int count)
    {
        count = Math.Min(Math.Max(1, count), _cols - _cursorCol);
        for (var c = _cursorCol; c < _cursorCol + count; c++)
            _grid[_cursorRow, c] = TerminalCell.Default;
    }

    private void ApplyScrollRegion(int top, int bottom)
    {
        var newTop = Clamp(top, 0, _rows - 1);
        var newBottom = bottom < 0 || bottom >= _rows ? _rows - 1 : Clamp(bottom, 0, _rows - 1);
        _scrollTop = newBottom > newTop ? newTop : 0;
        _scrollBottom = newBottom > newTop ? newBottom : _rows - 1;
        // DECSTBM setzt den Cursor auf Home.
        _cursorRow = 0;
        _cursorCol = 0;
    }

    private void ApplyCursorSaved(bool restored)
    {
        if (restored)
        {
            _cursorRow = Clamp(_savedCursorRow, 0, _rows - 1);
            _cursorCol = Clamp(_savedCursorCol, 0, _cols - 1);
        }
        else
        {
            _savedCursorRow = _cursorRow;
            _savedCursorCol = _cursorCol;
        }
    }

    private void ApplyAlternateScreen(bool enabled)
    {
        if (enabled == _isAlternateScreen)
            return;

        if (enabled)
        {
            _mainScreenGrid = _grid;
            _grid = new TerminalCell[_rows, _cols];
            FillGrid(_grid, _rows, _cols);
            _isAlternateScreen = true;
        }
        else
        {
            // Hauptscreen wiederherstellen, sofern die Buffer-Größe seitdem nicht geändert wurde.
            if (_mainScreenGrid is not null
                && _mainScreenGrid.GetLength(0) == _rows
                && _mainScreenGrid.GetLength(1) == _cols)
            {
                _grid = _mainScreenGrid;
            }
            _mainScreenGrid = null;
            _isAlternateScreen = false;
        }

        _scrollTop = 0;
        _scrollBottom = _rows - 1;
        _cursorRow = 0;
        _cursorCol = 0;
    }

    private TerminalCell[] CaptureRow(int rowIndex)
    {
        var row = new TerminalCell[_cols];
        for (var c = 0; c < _cols; c++)
            row[c] = _grid[rowIndex, c];
        return row;
    }

    private void PushToScrollback(TerminalCell[] row)
    {
        if (_scrollback.Count >= MaxScrollbackLines)
            _scrollback.Dequeue();
        _scrollback.Enqueue(row);
    }

    private void ApplyColor(ColorChangedEvent e)
    {
        if (e.Reset)
        {
            _currentForeground = TerminalCell.Default.Foreground;
            _currentBackground = TerminalCell.Default.Background;
            _currentBold = false;
            _currentDim = false;
            _currentUnderline = false;
            return;
        }

        if (e.Foreground.HasValue) _currentForeground = e.Foreground.Value;
        if (e.Background.HasValue) _currentBackground = e.Background.Value;
        if (e.Bold.HasValue) _currentBold = e.Bold.Value;
        if (e.Dim.HasValue) _currentDim = e.Dim.Value;
        if (e.Underline.HasValue) _currentUnderline = e.Underline.Value;
    }

    private void ApplyClearScreen(int mode)
    {
        switch (mode)
        {
            case 0:
                for (var c = _cursorCol; c < _cols; c++)
                    _grid[_cursorRow, c] = TerminalCell.Default;
                for (var r = _cursorRow + 1; r < _rows; r++)
                    for (var c = 0; c < _cols; c++)
                        _grid[r, c] = TerminalCell.Default;
                break;
            case 1:
                for (var r = 0; r < _cursorRow; r++)
                    for (var c = 0; c < _cols; c++)
                        _grid[r, c] = TerminalCell.Default;
                for (var c = 0; c <= _cursorCol; c++)
                    _grid[_cursorRow, c] = TerminalCell.Default;
                break;
            default:
                ClearAllCells();
                _cursorRow = 0;
                _cursorCol = 0;
                break;
        }
    }

    private void ApplyEraseLine(int mode)
    {
        switch (mode)
        {
            case 0:
                for (var c = _cursorCol; c < _cols; c++)
                    _grid[_cursorRow, c] = TerminalCell.Default;
                break;
            case 1:
                for (var c = 0; c <= _cursorCol; c++)
                    _grid[_cursorRow, c] = TerminalCell.Default;
                break;
            default:
                for (var c = 0; c < _cols; c++)
                    _grid[_cursorRow, c] = TerminalCell.Default;
                break;
        }
    }

    private static void FillGrid(TerminalCell[,] grid, int rows, int cols)
    {
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                grid[r, c] = TerminalCell.Default;
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    /// <summary>Gibt einen unter einem einzigen Lock erstellten, konsistenten Snapshot des aktuellen
    /// Buffer-Zustands zurück. Wird von Render-Operationen genutzt, um Race Conditions zwischen paralleler
    /// Buffer-Aktualisierung und Lesezugriffen über mehrere Einzelaufrufe hinweg zu vermeiden.</summary>
    /// <returns>Ein konsistenter <see cref="TerminalBufferSnapshot"/> des aktuellen Buffer-Zustands.</returns>
    public TerminalBufferSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var gridCopy = new TerminalCell[_rows, _cols];
            Array.Copy(_grid, gridCopy, _grid.Length);

            // Bei aktivem Alternate Screen bleibt der Scrollback der Hauptscreen-Ebene erhalten,
            // wird aber für Render-Zwecke ausgeblendet (Vollbild-TUIs haben keinen Scrollback).
            var scrollbackCount = _isAlternateScreen ? 0 : _scrollback.Count;
            var scrollbackRows = new TerminalCell[scrollbackCount][];
            var index = 0;
            foreach (var row in _scrollback)
            {
                if (index >= scrollbackCount)
                    break;
                var rowCopy = new TerminalCell[_cols];
                Array.Copy(row, rowCopy, Math.Min(row.Length, _cols));
                if (row.Length < _cols)
                    Array.Fill(rowCopy, TerminalCell.Default, row.Length, _cols - row.Length);
                scrollbackRows[index++] = rowCopy;
            }

            return new TerminalBufferSnapshot(gridCopy, _rows, _cols, _cursorRow, _cursorCol, scrollbackRows);
        }
    }

    private void ClearAllCells()
    {
        FillGrid(_grid, _rows, _cols);
        _scrollback.Clear();
    }
}

/// <summary>Konsistenter Snapshot des Zustands eines <see cref="TerminalBuffer"/>, unter einem einzigen Lock
/// erstellt über <see cref="TerminalBuffer.GetSnapshot"/>.</summary>
/// <param name="Grid">Kopie des Zellen-Grids zum Snapshot-Zeitpunkt.</param>
/// <param name="Rows">Zeilenanzahl des Buffers zum Snapshot-Zeitpunkt.</param>
/// <param name="Cols">Spaltenanzahl des Buffers zum Snapshot-Zeitpunkt.</param>
/// <param name="CursorRow">Cursor-Zeile zum Snapshot-Zeitpunkt.</param>
/// <param name="CursorCol">Cursor-Spalte zum Snapshot-Zeitpunkt.</param>
/// <param name="ScrollbackRows">Kopie der Scrollback-Zeilen, älteste Zeile zuerst.</param>
/// <returns>Eine neue <see cref="TerminalBufferSnapshot"/>-Instanz.</returns>
public sealed record TerminalBufferSnapshot(
    TerminalCell[,] Grid,
    int Rows,
    int Cols,
    int CursorRow,
    int CursorCol,
    TerminalCell[][] ScrollbackRows)
{
    /// <summary>Anzahl der Scrollback-Zeilen im Snapshot.</summary>
    public int ScrollbackCount => ScrollbackRows.Length;

    /// <summary>Gesamtzahl aus Scrollback-Zeilen plus sichtbarem Terminal-Grid.</summary>
    public int TotalRows => ScrollbackRows.Length + Rows;
}
