using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Controls;

/// <summary>WPF-Control, das eine <see cref="ITerminalSession"/> rendert und Tastatureingaben weiterleitet.
/// Reiner Renderer: Die Leseschleife läuft unabhängig vom Control-Lebenszyklus in der <see cref="ITerminalSession"/>
/// selbst; das Control abonniert lediglich deren <see cref="ITerminalSession.BufferChanged"/>-Event.</summary>
public sealed class TerminalControl : FrameworkElement, IScrollInfo
{
    private readonly ILogger<TerminalControl> _logger =
        App.Services?.GetService<ILogger<TerminalControl>>() ?? NullLogger<TerminalControl>.Instance;
    private TerminalBuffer? _buffer;
    private ITerminalSession? _currentSession;
    private static readonly Typeface ConsolasTypeface = new("Consolas");
    private const double FontSize = 13.0;
    private const double ScrollEndEpsilon = 0.001;
    private const int MouseWheelScrollLines = 3;
    private double _cellWidth;
    private double _cellHeight;
    private double _extentHeight;
    private double _viewportHeight;
    private double _verticalOffset;
    private double _horizontalOffset;
    private bool _canHorizontallyScroll;
    private bool _isFollowingEnd = true;
    private TerminalSelection? _selection;
    private bool _isSelecting;

    private static readonly SolidColorBrush BlackBrush = CreateFrozenBrush(Colors.Black);
    private static readonly SolidColorBrush CursorBrush = CreateFrozenBrush(Color.FromArgb(180, 255, 255, 255));
    private static readonly SolidColorBrush SelectionBrush = CreateFrozenBrush(Color.FromArgb(150, 51, 153, 255));
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();

    /// <summary>Dependency Property für die aktive <see cref="ITerminalSession"/>.</summary>
    /// <value>Das registrierte <see cref="DependencyProperty"/> für die Session-Eigenschaft.</value>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session),
        typeof(ITerminalSession),
        typeof(TerminalControl),
        new PropertyMetadata(null, OnSessionChanged));

    /// <summary>Die aktive Terminal-Sitzung.</summary>
    public ITerminalSession? Session
    {
        get => (ITerminalSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <inheritdoc/>
    public bool CanVerticallyScroll { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>Der Toggle ändert den meldebaren <see cref="ExtentWidth"/> (kollabiert auf
    /// <see cref="ViewportWidth"/>, wenn der Host das horizontale Scrollen abschaltet) — der
    /// horizontale Offset wird daher sofort gegen den neuen Scrollbereich re-geklemmt, statt bis
    /// zum nächsten <c>UpdateScrollInfo</c> verschoben zu bleiben.</remarks>
    public bool CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set
        {
            if (_canHorizontallyScroll == value)
                return;

            _canHorizontallyScroll = value;
            SetHorizontalOffset(_horizontalOffset);
        }
    }

    /// <inheritdoc/>
    public double ExtentWidth => CanHorizontallyScroll && _buffer != null
        ? Math.Max(ViewportWidth, _buffer.Cols * _cellWidth)
        : ViewportWidth;

    /// <inheritdoc/>
    public double ExtentHeight => _extentHeight;

    /// <inheritdoc/>
    public double ViewportWidth => Math.Max(0, ActualWidth);

    /// <inheritdoc/>
    public double ViewportHeight => _viewportHeight;

    /// <inheritdoc/>
    public double HorizontalOffset => _horizontalOffset;

    /// <inheritdoc/>
    public double VerticalOffset => _verticalOffset;

    /// <inheritdoc/>
    public ScrollViewer? ScrollOwner { get; set; }

    /// <summary>Erstellt eine neue <see cref="TerminalControl"/>-Instanz.</summary>
    public TerminalControl()
    {
        Focusable = true;
        var copyItem = new MenuItem
        {
            Header = "Kopieren",
            InputGestureText = "Strg+Umschalt+C",
        };
        copyItem.Click += (_, _) => CopySelectionToClipboard();
        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(copyItem);
        contextMenu.Opened += (_, _) => copyItem.IsEnabled = HasValidSelection();
        ContextMenu = contextMenu;
        MeasureCellSize();
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TerminalControl control)
            control.OnSessionChanged((ITerminalSession?)e.NewValue);
    }

    private void OnSessionChanged(ITerminalSession? session)
    {
        if (_currentSession != null)
            _currentSession.BufferChanged -= OnBufferChanged;

        _currentSession = session;

        if (session == null)
        {
            ClearSelection();
            _buffer = null;
            _verticalOffset = 0;
            _horizontalOffset = 0;
            _extentHeight = 0;
            _viewportHeight = 0;
            _isFollowingEnd = true;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateVisual();
            return;
        }

        MeasureCellSize();
        ClearSelection();

        // Vor dem Rebuild subscribieren: Ein Output-Chunk, der die Leseschleife zwischen
        // RebuildBufferFromReplay und der Registrierung trifft, läge sonst zwar korrekt im Buffer,
        // löste aber kein InvalidateVisual aus (Anzeige bliebe einen Chunk zurück).
        session.BufferChanged += OnBufferChanged;

        // Buffer aus dem gespeicherten Rohdaten-Replay neu aufbauen: deterministisch derselbe
        // Endzustand wie beim Lösen der Bindung, ohne dass Live-Chunks doppelt erscheinen.
        session.RebuildBufferFromReplay();
        _buffer = session.Buffer;
        if (session.SupportsResize)
        {
            // Sessions mit fixierter Geometrie (SupportsResize == false, z. B. Replay) behalten
            // ihre Buffer-Größe; überschüssige Breite wird über den horizontalen Offset erreichbar.
            _buffer.Resize(CalculateCols(), CalculateRows());
        }

        _horizontalOffset = 0;
        _isFollowingEnd = true;
        UpdateScrollInfo(followEndIfNeeded: true);

        // Sofort rendern, damit vorhandener Bufferinhalt sichtbar wird ohne auf neue Ausgabe warten.
        _ = Dispatcher.InvokeAsync(InvalidateVisual);
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            ValidateSelection();
            UpdateScrollInfo(followEndIfNeeded: true);
            InvalidateVisual();
        });
    }

    /// <summary>TerminalControl leitet direkt von <see cref="FrameworkElement"/> ab, dessen Standard-Implementierung
    /// von <c>OnCreateAutomationPeer</c> <c>null</c> zurückgibt — ohne diesen Override erzeugt WPF keinen
    /// AutomationPeer, wodurch weder <c>AutomationProperties.Name</c> noch <c>HelpText</c> an UI-Automation-Clients
    /// (z. B. FlaUI in E2E-Tests) durchgereicht werden, obwohl beide in XAML/Code-behind gesetzt sind.</summary>
    /// <returns>Ein <see cref="FrameworkElementAutomationPeer"/>, der Name und HelpText aus den angehängten
    /// <c>AutomationProperties</c> liest.</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(BlackBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var buffer = _buffer;
        if (buffer == null)
            return;

        MeasureCellSize();

        var snapshot = buffer.GetSnapshot();
        var cols = snapshot.Cols;
        var cursorCol = snapshot.CursorCol;
        var visibleRows = CalculateRows();
        var totalRows = snapshot.TotalRows;
        var visibleStart = _isFollowingEnd
            ? Math.Max(0, totalRows - visibleRows)
            : Clamp((int)Math.Round(_verticalOffset), 0, Math.Max(0, totalRows - visibleRows));

        // Sichtbarer Spaltenbereich bei horizontalem Scroll-Offset (Pixel-Einheiten):
        // nur diese Spalten werden gezeichnet — bei breiten fixierten Buffern (Replay)
        // bleibt der FormattedText-Aufbau auf den sichtbaren Ausschnitt begrenzt.
        var firstVisibleCol = Clamp((int)(_horizontalOffset / _cellWidth), 0, cols);
        var lastVisibleCol = Clamp((int)Math.Ceiling((_horizontalOffset + ActualWidth) / _cellWidth), 0, cols);

        // Clip auf die Control-Bounds + Verschiebung des Inhalts um den horizontalen
        // Scroll-Offset: Bei _horizontalOffset == 0 ist die Transform eine Identität.
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.PushTransform(new TranslateTransform(-_horizontalOffset, 0));

        for (var r = 0; r < visibleRows; r++)
        {
            var y = r * _cellHeight;
            var logicalRow = visibleStart + r;

            var bgStart = firstVisibleCol;
            while (bgStart < lastVisibleCol)
            {
                var bgColor = GetSnapshotCell(snapshot, logicalRow, bgStart).Background;
                var bgEnd = bgStart + 1;
                while (bgEnd < lastVisibleCol && GetSnapshotCell(snapshot, logicalRow, bgEnd).Background == bgColor)
                    bgEnd++;

                if (bgColor != System.Drawing.Color.Black)
                {
                    var brush = GetBrush(Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
                    dc.DrawRectangle(brush, null, new Rect(bgStart * _cellWidth, y, (bgEnd - bgStart) * _cellWidth, _cellHeight));
                }

                bgStart = bgEnd;
            }

            for (var c = firstVisibleCol; c < lastVisibleCol; c++)
            {
                var cell = GetSnapshotCell(snapshot, logicalRow, c);
                if (cell.Character == ' ' || cell.Character == '\0')
                    continue;

                var fg = cell.Foreground;
                var fgBrush = GetBrush(Color.FromArgb(fg.A, fg.R, fg.G, fg.B));
                var ft = new FormattedText(
                    cell.Character.ToString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ConsolasTypeface,
                    FontSize,
                    fgBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(ft, new Point(c * _cellWidth, y));
            }

            DrawSelection(dc, snapshot, logicalRow, y, firstVisibleCol, lastVisibleCol);
        }

        var cursorLogicalRow = snapshot.ScrollbackCount + snapshot.CursorRow;
        var cursorRenderRow = cursorLogicalRow - visibleStart;
        if (cursorRenderRow >= 0 && cursorRenderRow < visibleRows
            && cursorCol >= firstVisibleCol && cursorCol < lastVisibleCol)
        {
            var cursorX = cursorCol * _cellWidth;
            var cursorY = cursorRenderRow * _cellHeight;
            dc.DrawRectangle(CursorBrush, null, new Rect(cursorX, cursorY, _cellWidth, _cellHeight));
        }

        dc.Pop();
        dc.Pop();
    }

    private SolidColorBrush GetBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            brush = CreateFrozenBrush(color);
            _brushCache[color] = brush;
        }

        return brush;
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var session = Session;
        var modifiers = e.KeyboardDevice.Modifiers;

        if (e.Key == Key.C && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (_selection is not null)
            {
                e.Handled = true;
                CopySelectionToClipboard();
            }
            return;
        }

        if ((modifiers & ModifierKeys.Shift) != 0 && IsSelectionNavigationKey(e.Key))
        {
            ExtendSelection(e.Key);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && (modifiers & ModifierKeys.Control) != 0)
        {
            if (session?.InputStream != null)
            {
                e.Handled = true;
                _ = ReadClipboardAndInsertAsync(session);
            }

            return;
        }

        // Sessions ohne realen Eingabekanal (Wiedergabe: InputStream == Stream.Null) können mit
        // Tastaturbytes nichts anfangen — Navigationstasten werden nicht kodiert und nicht als
        // behandelt markiert, damit sie zum umschließenden ScrollViewer bubbeln und dessen
        // Standard-Tastatur-Scrollen (Line*/Page*/Home/End) greift.
        if (!HasInputChannel(session) && IsNavigationKey(e.Key))
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        var bytes = KeyToVt100Encoder.Encode(e);
        if (bytes != null && session?.InputStream != null)
        {
            WriteToInputStream(bytes);
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc/>
    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && Session?.InputStream != null)
        {
            var bytes = KeyToVt100Encoder.EncodeText(e.Text);
            WriteToInputStream(bytes);
            e.Handled = true;
        }

        base.OnTextInput(e);
    }

    /// <summary>Schreibt Tastatur-Bytes über <see cref="ITerminalSession.WriteInputAsync"/> in die
    /// aktuelle Session — derselbe serialisierte Weg (Write-Lock + Chunking) wie
    /// <see cref="ITerminalSession.WritePromptAsync"/>, damit sich Tastatureingaben und zeitgesteuerte
    /// Prompts nicht byte-genau vermischen. Schreibfehler werden protokolliert statt verschluckt.</summary>
    /// <param name="bytes">Die zu schreibenden Bytes.</param>
    private void WriteToInputStream(byte[] bytes)
    {
        var session = Session;
        if (session?.InputStream is null)
            return;
        _ = WriteToInputStreamAsync(session, bytes, "Fehler beim Schreiben in den Terminal-Input-Stream");
    }

    /// <summary>Ob die Session einen realen Eingabekanal besitzt — Sessions mit fixierter Geometrie
    /// (Wiedergabe) melden <see cref="Stream.Null"/>: dorthin geschriebene Bytes gingen ins Leere.</summary>
    /// <param name="session">Die zu prüfende Session (oder <c>null</c>).</param>
    private static bool HasInputChannel(ITerminalSession? session)
        => session?.InputStream is { } input && !ReferenceEquals(input, Stream.Null);

    /// <summary>Ob die Taste eine reine Navigations-/Scrolltaste ist (Pfeile, Bild auf/ab, Pos1/Ende) —
    /// Sessions ohne Eingabekanal reichen sie zum Scrollen an den umschließenden ScrollViewer durch.</summary>
    /// <param name="key">Die gedrückte Taste.</param>
    private static bool IsNavigationKey(Key key) => key is
        Key.Left or Key.Right or Key.Up or Key.Down
        or Key.PageUp or Key.PageDown or Key.Home or Key.End;

    private static bool IsSelectionNavigationKey(Key key) => key is
        Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End;

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Keyboard.Focus(this);
        if (e.ChangedButton == MouseButton.Left && TryGetCellAt(e.GetPosition(this), out var point))
        {
            _selection = CreateSelection(point, point);
            _isSelecting = true;
            CaptureMouse();
            e.Handled = true;
            InvalidateVisual();
        }
        base.OnMouseDown(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isSelecting && _selection is not null && TryGetCellAt(e.GetPosition(this), out var point))
        {
            _selection = CreateSelection(_selection.Anchor, point);
            InvalidateVisual();
            e.Handled = true;
        }
        base.OnMouseMove(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isSelecting && e.ChangedButton == MouseButton.Left)
        {
            _isSelecting = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
        base.OnMouseUp(e);
    }

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        var session = Session;
        var buffer = _buffer;
        if (session != null && buffer != null)
        {
            MeasureCellSize();
            if (session.SupportsResize)
            {
                var cols = CalculateCols();
                var rows = CalculateRows();
                buffer.Resize(cols, rows);
                session.Resize(cols, rows);
                ClearSelection();
            }

            UpdateScrollInfo(followEndIfNeeded: true);
            InvalidateVisual();
        }

        base.OnRenderSizeChanged(sizeInfo);
    }

    private void MeasureCellSize()
    {
        var ft = new FormattedText(
            "W",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            ConsolasTypeface,
            FontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        _cellWidth = ft.Width;
        _cellHeight = ft.Height;

        if (_cellWidth <= 0) _cellWidth = 8;
        if (_cellHeight <= 0) _cellHeight = 16;
    }

    private int CalculateCols()
    {
        var w = ActualWidth > 0 ? ActualWidth : 220 * 8;
        return Math.Max(1, (int)(w / _cellWidth));
    }

    private int CalculateRows()
    {
        var h = ActualHeight > 0 ? ActualHeight : 50 * 16;
        return Math.Max(1, (int)(h / _cellHeight));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCellSize();
        // Bei unendlicher verfügbarer Breite (scrollender Host) ist die gewünschte Breite die
        // Buffer-Geometrie — bei fixierten Replay-Buffern bleibt der Inhalt so breiter als der
        // Viewport und der ScrollViewer kann horizontal scrollen.
        var width = double.IsInfinity(availableSize.Width)
            ? (_buffer?.Cols ?? CalculateCols()) * _cellWidth
            : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? CalculateRows() * _cellHeight : availableSize.Height;
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateScrollInfo(followEndIfNeeded: true);
        return size;
    }

    /// <inheritdoc/>
    public void LineUp() => SetVerticalOffset(_verticalOffset - 1);

    /// <inheritdoc/>
    public void LineDown() => SetVerticalOffset(_verticalOffset + 1);

    /// <inheritdoc/>
    public void LineLeft() => SetHorizontalOffset(_horizontalOffset - _cellWidth);

    /// <inheritdoc/>
    public void LineRight() => SetHorizontalOffset(_horizontalOffset + _cellWidth);

    /// <inheritdoc/>
    public void PageUp() => SetVerticalOffset(_verticalOffset - GetPageScrollRows());

    /// <inheritdoc/>
    public void PageDown() => SetVerticalOffset(_verticalOffset + GetPageScrollRows());

    /// <inheritdoc/>
    public void PageLeft() => SetHorizontalOffset(_horizontalOffset - GetPageScrollPixels());

    /// <inheritdoc/>
    public void PageRight() => SetHorizontalOffset(_horizontalOffset + GetPageScrollPixels());

    /// <inheritdoc/>
    public void MouseWheelUp() => SetVerticalOffset(_verticalOffset - MouseWheelScrollLines);

    /// <inheritdoc/>
    public void MouseWheelDown() => SetVerticalOffset(_verticalOffset + MouseWheelScrollLines);

    /// <inheritdoc/>
    public void MouseWheelLeft() => SetHorizontalOffset(_horizontalOffset - MouseWheelScrollLines * _cellWidth);

    /// <inheritdoc/>
    public void MouseWheelRight() => SetHorizontalOffset(_horizontalOffset + MouseWheelScrollLines * _cellWidth);

    /// <inheritdoc/>
    public void SetHorizontalOffset(double offset)
    {
        _horizontalOffset = ClampOffset(offset, ScrollableWidth);
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public void SetVerticalOffset(double offset)
    {
        // Bei aktivem Alternate Screen (Vollbild-TUI) gibt es keinen Scrollback — der vertikale
        // Offset bleibt auf 0 geklemmt und Line*/Page*/MouseWheel* werden zu No-Ops.
        if (_buffer?.IsAlternateScreenActive == true)
        {
            _verticalOffset = 0;
            _isFollowingEnd = true;
            ScrollOwner?.InvalidateScrollInfo();
            return;
        }

        var clamped = ClampOffset(offset, ScrollableHeight);
        _verticalOffset = clamped;
        _isFollowingEnd = clamped >= ScrollableHeight - ScrollEndEpsilon;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

    private double ScrollableHeight => Math.Max(0, _extentHeight - _viewportHeight);

    private double ScrollableWidth => Math.Max(0, ExtentWidth - ViewportWidth);

    private int GetPageScrollRows() => Math.Max(1, CalculateRows() - 1);

    /// <summary>Seiten-Scrollweite der horizontalen Achse (Viewport minus eine Zelle, analog
    /// <see cref="GetPageScrollRows"/>). Nach unten auf eine Zelle geklemmt: bei einem Viewport
    /// schmaler als eine Zelle (z. B. vor dem ersten Arrange) würde die Distanz sonst negativ
    /// und damit die Scrollrichtung invertieren.</summary>
    private double GetPageScrollPixels() => Math.Max(_cellWidth, ViewportWidth - _cellWidth);

    private void UpdateScrollInfo(bool followEndIfNeeded)
    {
        MeasureCellSize();
        var snapshot = _buffer?.GetSnapshot();
        var visibleRows = CalculateRows();
        // Im Alternate Screen existiert kein Scrollback-Bereich: Extent entspricht dem sichtbaren Grid.
        var totalRows = _buffer?.IsAlternateScreenActive == true
            ? (snapshot?.Rows ?? 0)
            : (snapshot?.TotalRows ?? 0);

        _viewportHeight = Math.Min(visibleRows, Math.Max(visibleRows, totalRows));
        _extentHeight = Math.Max(_viewportHeight, totalRows);

        var scrollableHeight = ScrollableHeight;
        _verticalOffset = followEndIfNeeded && _isFollowingEnd
            ? scrollableHeight
            : ClampOffset(_verticalOffset, scrollableHeight);
        _isFollowingEnd = _verticalOffset >= scrollableHeight - ScrollEndEpsilon;

        // Verbreitert sich der Viewport, wird der horizontale Offset auf den neuen
        // ScrollableWidth-Bereich zurückgeklemmt.
        _horizontalOffset = ClampOffset(_horizontalOffset, ScrollableWidth);

        ScrollOwner?.InvalidateScrollInfo();
    }

    private static TerminalCell GetSnapshotCell(TerminalBufferSnapshot snapshot, int logicalRow, int col)
    {
        if (logicalRow < 0 || logicalRow >= snapshot.TotalRows || col < 0 || col >= snapshot.Cols)
            return TerminalCell.Default;

        if (logicalRow < snapshot.ScrollbackCount)
            return snapshot.ScrollbackRows[logicalRow][col];

        return snapshot.Grid[logicalRow - snapshot.ScrollbackCount, col];
    }

    private static long GetRowId(TerminalBufferSnapshot snapshot, int logicalRow)
        => logicalRow < snapshot.ScrollbackCount
            ? snapshot.ScrollbackRowIds[logicalRow]
            : snapshot.GridRowIds[logicalRow - snapshot.ScrollbackCount];

    private static long GetCellVersion(TerminalBufferSnapshot snapshot, int logicalRow, int col)
        => logicalRow < snapshot.ScrollbackCount
            ? snapshot.ScrollbackCellVersions[logicalRow][col]
            : snapshot.GridCellVersions[logicalRow - snapshot.ScrollbackCount, col];

    private bool TryGetCellAt(Point position, out TerminalSelectionPoint point)
    {
        point = default;
        var buffer = _buffer;
        if (buffer is null || position.X < 0 || position.Y < 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
            return false;

        var snapshot = buffer.GetSnapshot();
        var visibleRows = CalculateRows();
        var visibleStart = _isFollowingEnd
            ? Math.Max(0, snapshot.TotalRows - visibleRows)
            : Clamp((int)Math.Round(_verticalOffset), 0, Math.Max(0, snapshot.TotalRows - visibleRows));
        var row = visibleStart + Clamp((int)(position.Y / _cellHeight), 0, visibleRows - 1);
        var col = Clamp((int)((position.X + _horizontalOffset) / _cellWidth), 0, snapshot.Cols - 1);
        if (row >= snapshot.TotalRows)
            return false;

        point = new TerminalSelectionPoint(GetRowId(snapshot, row), col, GetCellVersion(snapshot, row, col));
        return true;
    }

    private TerminalSelection CreateSelection(TerminalSelectionPoint anchor, TerminalSelectionPoint end)
    {
        var snapshot = _buffer?.GetSnapshot();
        return snapshot is null
            ? new TerminalSelection(-1, anchor, end, new Dictionary<(long RowId, int Column), long>())
            : new TerminalSelection(snapshot.Generation, anchor, end, CaptureCellVersions(snapshot, anchor, end));
    }

    private void ExtendSelection(Key key)
    {
        var snapshot = _buffer?.GetSnapshot();
        if (snapshot is null || snapshot.TotalRows == 0)
            return;

        var current = _selection?.End;
        var row = current is null ? snapshot.ScrollbackCount + snapshot.CursorRow : FindLogicalRow(snapshot, current.Value.RowId);
        // Nach dem Schreiben in die letzte Spalte kann der Cursor hinter dem Grid
        // stehen. Auswahlpunkte dürfen dagegen nur echte Zellen referenzieren.
        var col = Clamp(current?.Column ?? snapshot.CursorCol, 0, snapshot.Cols - 1);
        if (row < 0) return;

        // Bei der ersten Tastatur-Erweiterung ist die Cursorzelle der Anker, nicht
        // das bereits verschobene Ende. Dadurch entspricht Shift+Home/Up dem
        // etablierten Textauswahlverhalten.
        var anchor = _selection?.Anchor ?? new TerminalSelectionPoint(
            GetRowId(snapshot, row), col, GetCellVersion(snapshot, row, col));
        switch (key)
        {
            case Key.Left: col = Math.Max(0, col - 1); break;
            case Key.Right: col = Math.Min(snapshot.Cols - 1, col + 1); break;
            case Key.Up: row = Math.Max(0, row - 1); break;
            case Key.Down: row = Math.Min(snapshot.TotalRows - 1, row + 1); break;
            case Key.Home: col = 0; break;
            case Key.End: col = snapshot.Cols - 1; break;
        }
        var end = new TerminalSelectionPoint(GetRowId(snapshot, row), col, GetCellVersion(snapshot, row, col));
        _selection = CreateSelection(anchor, end);
        EnsureSelectionEndVisible(snapshot, row, col);
        InvalidateVisual();
    }

    private void EnsureSelectionEndVisible(TerminalBufferSnapshot snapshot, int row, int col)
    {
        UpdateScrollInfo(followEndIfNeeded: false);
        var visibleRows = CalculateRows();
        var visibleStart = _isFollowingEnd
            ? Math.Max(0, snapshot.TotalRows - visibleRows)
            : Clamp((int)Math.Round(_verticalOffset), 0, Math.Max(0, snapshot.TotalRows - visibleRows));
        if (row < visibleStart)
            SetVerticalOffset(row);
        else if (row >= visibleStart + visibleRows)
            SetVerticalOffset(row - visibleRows + 1);

        var colLeft = _horizontalOffset / _cellWidth;
        var visibleCols = Math.Max(1, ActualWidth / _cellWidth);
        if (col < colLeft)
            SetHorizontalOffset(col * _cellWidth);
        else if (col >= colLeft + visibleCols)
            SetHorizontalOffset((col - visibleCols + 1) * _cellWidth);
    }

    private bool HasValidSelection()
    {
        var snapshot = _buffer?.GetSnapshot();
        return snapshot is not null && _selection is not null &&
            TryNormalizeSelection(snapshot, _selection, out _, out _, out _, out _);
    }

    private void ValidateSelection()
    {
        if (_selection is null) return;
        var snapshot = _buffer?.GetSnapshot();
        if (snapshot is null || !TryNormalizeSelection(snapshot, _selection, out _, out _, out _, out _))
            ClearSelection();
    }

    private static bool TryNormalizeSelection(TerminalBufferSnapshot snapshot, TerminalSelection selection,
        out int firstRow, out int firstCol, out int lastRow, out int lastCol)
    {
        firstRow = FindLogicalRow(snapshot, selection.Anchor.RowId);
        lastRow = FindLogicalRow(snapshot, selection.End.RowId);
        firstCol = selection.Anchor.Column;
        lastCol = selection.End.Column;
        if (selection.Generation != snapshot.Generation || firstRow < 0 || lastRow < 0 ||
            GetCellVersion(snapshot, firstRow, firstCol) != selection.Anchor.CellVersion ||
            GetCellVersion(snapshot, lastRow, lastCol) != selection.End.CellVersion)
            return false;
        if (firstRow > lastRow || (firstRow == lastRow && firstCol > lastCol))
            (firstRow, lastRow, firstCol, lastCol) = (lastRow, firstRow, lastCol, firstCol);
        return selection.CellVersions.All(entry =>
        {
            var row = FindLogicalRow(snapshot, entry.Key.RowId);
            return row >= 0 && entry.Key.Column >= 0 && entry.Key.Column < snapshot.Cols &&
                GetCellVersion(snapshot, row, entry.Key.Column) == entry.Value;
        });
    }

    private static Dictionary<(long RowId, int Column), long> CaptureCellVersions(
        TerminalBufferSnapshot snapshot, TerminalSelectionPoint anchor, TerminalSelectionPoint end)
    {
        var firstRow = FindLogicalRow(snapshot, anchor.RowId);
        var lastRow = FindLogicalRow(snapshot, end.RowId);
        var firstCol = anchor.Column;
        var lastCol = end.Column;
        var result = new Dictionary<(long RowId, int Column), long>();
        if (firstRow < 0 || lastRow < 0) return result;
        if (firstRow > lastRow || (firstRow == lastRow && firstCol > lastCol))
            (firstRow, lastRow, firstCol, lastCol) = (lastRow, firstRow, lastCol, firstCol);
        for (var row = firstRow; row <= lastRow; row++)
            for (var col = row == firstRow ? firstCol : 0; col <= (row == lastRow ? lastCol : snapshot.Cols - 1); col++)
                result[(GetRowId(snapshot, row), col)] = GetCellVersion(snapshot, row, col);
        return result;
    }

    private static int FindLogicalRow(TerminalBufferSnapshot snapshot, long rowId)
    {
        for (var row = 0; row < snapshot.TotalRows; row++)
            if (GetRowId(snapshot, row) == rowId) return row;
        return -1;
    }

    private void DrawSelection(DrawingContext dc, TerminalBufferSnapshot snapshot, int logicalRow, double y, int firstVisibleCol, int lastVisibleCol)
    {
        if (_selection is null || !TryNormalizeSelection(snapshot, _selection, out var firstRow, out var firstCol, out var lastRow, out var lastCol) ||
            logicalRow < firstRow || logicalRow > lastRow)
            return;
        var start = logicalRow == firstRow ? firstCol : 0;
        var end = logicalRow == lastRow ? lastCol : snapshot.Cols - 1;
        start = Math.Max(start, firstVisibleCol);
        end = Math.Min(end, lastVisibleCol - 1);
        if (start <= end)
            dc.DrawRectangle(SelectionBrush, null, new Rect(start * _cellWidth, y, (end - start + 1) * _cellWidth, _cellHeight));
    }

    private void CopySelectionToClipboard()
    {
        var snapshot = _buffer?.GetSnapshot();
        if (snapshot is null || _selection is null || !TryNormalizeSelection(snapshot, _selection, out var firstRow, out var firstCol, out var lastRow, out var lastCol))
        {
            ClearSelection();
            return;
        }
        var lines = new List<string>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            var start = row == firstRow ? firstCol : 0;
            var end = row == lastRow ? lastCol : snapshot.Cols - 1;
            var characters = new char[end - start + 1];
            for (var col = start; col <= end; col++) characters[col - start] = GetSnapshotCell(snapshot, row, col).Character;
            lines.Add(new string(characters).TrimEnd(' ', '\0'));
        }
        try { Clipboard.SetText(string.Join(Environment.NewLine, lines)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Fehler beim Kopieren der Terminalauswahl in die Zwischenablage"); }
    }

    private void ClearSelection()
    {
        _selection = null;
        _isSelecting = false;
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    private static double ClampOffset(double value, double max)
    {
        if (double.IsNaN(value) || value < 0)
            return 0;
        return value > max ? max : value;
    }

    /// <summary>Liest den Text aus der Zwischenablage, kodiert ihn für die CLI und schreibt ihn in den
    /// Input-Stream der beim Paste-Start aktiven Session. Fehler beim Zwischenablage-Zugriff, Kodieren oder
    /// Schreiben werden abgefangen und protokolliert, statt das Control zu beeinträchtigen.</summary>
    /// <param name="session">Die beim Paste-Start snapshotete Zielsession.</param>
    private async Task ReadClipboardAndInsertAsync(ITerminalSession session)
    {
        var text = GetClipboardText();
        if (string.IsNullOrEmpty(text))
            return;

        var bytes = KeyToVt100Encoder.EncodeClipboardText(text);
        await WriteToInputStreamAsync(session, bytes, "Fehler beim Einfügen aus der Zwischenablage in den Terminal-Input-Stream").ConfigureAwait(false);
    }

    /// <summary>Schreibt Bytes asynchron in den Input-Stream der aktuellen Session und protokolliert Schreibfehler
    /// statt sie zu verschlucken. Wartet mit <c>ConfigureAwait(false)</c>, damit ein synchron blockierender Aufrufer
    /// (z. B. in Tests) nicht durch die Erfassung des UI-SynchronizationContext blockiert.</summary>
    /// <param name="session">Die Zielsession für den Schreibvorgang.</param>
    /// <param name="bytes">Die zu schreibenden Bytes.</param>
    /// <param name="errorMessage">Die Log-Nachricht bei einem Schreibfehler.</param>
    private async Task WriteToInputStreamAsync(ITerminalSession session, byte[] bytes, string errorMessage)
    {
        try
        {
            await session.WriteInputAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, errorMessage);
        }
    }

    /// <summary>Liest den aktuellen Text aus der Windows-Zwischenablage.</summary>
    /// <returns>Der gelesene Text, oder <see cref="string.Empty"/> wenn keine Textdaten vorhanden sind oder der Zugriff fehlschlägt.</returns>
    private string GetClipboardText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Lesen aus der Zwischenablage");
            return string.Empty;
        }
    }

    private readonly record struct TerminalSelectionPoint(long RowId, int Column, long CellVersion);

    private sealed record TerminalSelection(
        long Generation,
        TerminalSelectionPoint Anchor,
        TerminalSelectionPoint End,
        IReadOnlyDictionary<(long RowId, int Column), long> CellVersions);
}
