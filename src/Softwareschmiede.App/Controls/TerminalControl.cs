using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Controls;

/// <summary>WPF-Control, das eine <see cref="PseudoConsoleSession"/> rendert und Tastatureingaben weiterleitet.</summary>
public sealed class TerminalControl : FrameworkElement
{
    private TerminalBuffer? _buffer;
    private CancellationTokenSource? _readCts;
    private volatile AnsiSequenceParser _parser = new();
    private static readonly Typeface ConsolasTypeface = new("Consolas");
    private const double FontSize = 13.0;
    private double _cellWidth;
    private double _cellHeight;

    private static readonly SolidColorBrush BlackBrush = CreateFrozenBrush(Colors.Black);
    private static readonly SolidColorBrush CursorBrush = CreateFrozenBrush(Color.FromArgb(180, 255, 255, 255));
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();

    /// <summary>Dependency Property für die aktive <see cref="PseudoConsoleSession"/>.</summary>
    /// <value>Das registrierte <see cref="DependencyProperty"/> für die Session-Eigenschaft.</value>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session),
        typeof(PseudoConsoleSession),
        typeof(TerminalControl),
        new PropertyMetadata(null, OnSessionChanged));

    /// <summary>Die aktive Terminal-Sitzung.</summary>
    public PseudoConsoleSession? Session
    {
        get => (PseudoConsoleSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>Erstellt eine neue <see cref="TerminalControl"/>-Instanz.</summary>
    public TerminalControl()
    {
        Focusable = true;
        MeasureCellSize();
        Unloaded += (_, _) =>
        {
            _readCts?.Cancel();
            _readCts?.Dispose();
            _readCts = null;
        };
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TerminalControl control)
            control.OnSessionChanged((PseudoConsoleSession?)e.NewValue);
    }

    private void OnSessionChanged(PseudoConsoleSession? session)
    {
        _readCts?.Cancel();
        _readCts?.Dispose();
        _readCts = null;

        // Parser zurücksetzen, damit keine Zustandsreste der alten Session die neue Session beeinflussen.
        _parser = new AnsiSequenceParser();

        if (session == null)
            return;

        var cols = CalculateCols();
        var rows = CalculateRows();
        _buffer = new TerminalBuffer(cols, rows);

        var cts = new CancellationTokenSource();
        _readCts = cts;
        _ = Task.Run(() => ReadLoopAsync(session, _buffer, cts.Token));
    }

    private async Task ReadLoopAsync(PseudoConsoleSession session, TerminalBuffer buffer, CancellationToken ct)
    {
        var data = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await session.OutputStream.ReadAsync(data, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (bytesRead == 0)
                    break;

                var events = _parser.Parse(data.AsSpan(0, bytesRead));
                foreach (var evt in events)
                    buffer.Apply(evt);

                await Dispatcher.InvokeAsync(InvalidateVisual);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(BlackBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var buffer = _buffer;
        if (buffer == null)
            return;

        MeasureCellSize();

        var rows = buffer.Rows;
        var cols = buffer.Cols;
        var cursorRow = buffer.CursorRow;
        var cursorCol = buffer.CursorCol;

        for (var r = 0; r < rows; r++)
        {
            var cells = buffer.GetRow(r);
            var y = r * _cellHeight;

            var bgStart = 0;
            while (bgStart < cells.Length)
            {
                var bgColor = cells[bgStart].Background;
                var bgEnd = bgStart + 1;
                while (bgEnd < cells.Length && cells[bgEnd].Background == bgColor)
                    bgEnd++;

                if (bgColor != System.Drawing.Color.Black)
                {
                    var brush = GetBrush(Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
                    dc.DrawRectangle(brush, null, new Rect(bgStart * _cellWidth, y, (bgEnd - bgStart) * _cellWidth, _cellHeight));
                }

                bgStart = bgEnd;
            }

            for (var c = 0; c < cells.Length; c++)
            {
                var cell = cells[c];
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
        }

        var cursorX = cursorCol * _cellWidth;
        var cursorY = cursorRow * _cellHeight;
        dc.DrawRectangle(CursorBrush, null, new Rect(cursorX, cursorY, _cellWidth, _cellHeight));
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
        var bytes = KeyToVt100Encoder.Encode(e);
        if (bytes != null && Session?.InputStream != null)
        {
            try { Session.InputStream.Write(bytes); }
            catch { }
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
            try { Session.InputStream.Write(bytes); }
            catch { }
            e.Handled = true;
        }

        base.OnTextInput(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Keyboard.Focus(this);
        base.OnMouseDown(e);
    }

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        var session = Session;
        var buffer = _buffer;
        if (session != null && buffer != null)
        {
            MeasureCellSize();
            var cols = CalculateCols();
            var rows = CalculateRows();
            buffer.Resize(cols, rows);
            session.Resize(cols, rows);
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
}
