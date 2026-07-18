using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.App.Controls;

/// <summary>WPF-Control, das eine <see cref="PseudoConsoleSession"/> rendert und Tastatureingaben weiterleitet.
/// Reiner Renderer: Die Leseschleife läuft unabhängig vom Control-Lebenszyklus in der <see cref="PseudoConsoleSession"/>
/// selbst; das Control abonniert lediglich deren <see cref="PseudoConsoleSession.BufferChanged"/>-Event.</summary>
public sealed class TerminalControl : FrameworkElement
{
    private readonly ILogger<TerminalControl> _logger =
        App.Services?.GetService<ILogger<TerminalControl>>() ?? NullLogger<TerminalControl>.Instance;
    private TerminalBuffer? _buffer;
    private PseudoConsoleSession? _currentSession;
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
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TerminalControl control)
            control.OnSessionChanged((PseudoConsoleSession?)e.NewValue);
    }

    private void OnSessionChanged(PseudoConsoleSession? session)
    {
        if (_currentSession != null)
            _currentSession.BufferChanged -= OnBufferChanged;

        _currentSession = session;

        if (session == null)
        {
            _buffer = null;
            InvalidateVisual();
            return;
        }

        MeasureCellSize();
        var cols = CalculateCols();
        var rows = CalculateRows();

        // Bestehenden Buffer der Sitzung wiederverwenden: Bildschirminhalt bleibt erhalten, wenn der
        // Anwender zur Aufgabe zurücknavigiert, ohne dass neue Ausgabe eintreffen muss.
        _buffer = session.Buffer;
        _buffer.Resize(cols, rows);

        session.BufferChanged += OnBufferChanged;

        // Sofort rendern, damit vorhandener Bufferinhalt sichtbar wird ohne auf neue Ausgabe warten.
        _ = Dispatcher.InvokeAsync(InvalidateVisual);
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(InvalidateVisual);
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
        var rows = snapshot.Rows;
        var cols = snapshot.Cols;
        var cursorRow = snapshot.CursorRow;
        var cursorCol = snapshot.CursorCol;
        var grid = snapshot.Grid;

        for (var r = 0; r < rows; r++)
        {
            var y = r * _cellHeight;

            var bgStart = 0;
            while (bgStart < cols)
            {
                var bgColor = grid[r, bgStart].Background;
                var bgEnd = bgStart + 1;
                while (bgEnd < cols && grid[r, bgEnd].Background == bgColor)
                    bgEnd++;

                if (bgColor != System.Drawing.Color.Black)
                {
                    var brush = GetBrush(Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
                    dc.DrawRectangle(brush, null, new Rect(bgStart * _cellWidth, y, (bgEnd - bgStart) * _cellWidth, _cellHeight));
                }

                bgStart = bgEnd;
            }

            for (var c = 0; c < cols; c++)
            {
                var cell = grid[r, c];
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
        if (e.Key == Key.V && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
        {
            if (Session?.InputStream != null)
            {
                e.Handled = true;
                _ = ReadClipboardAndInsertAsync();
            }

            return;
        }

        var bytes = KeyToVt100Encoder.Encode(e);
        if (bytes != null && Session?.InputStream != null)
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

    /// <summary>Schreibt Bytes in den Input-Stream der aktuellen Session und protokolliert Schreibfehler statt sie zu verschlucken.</summary>
    /// <param name="bytes">Die zu schreibenden Bytes.</param>
    private void WriteToInputStream(byte[] bytes)
    {
        try
        {
            Session!.InputStream!.Write(bytes);
            Session.MarkInputActivity();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Schreiben in den Terminal-Input-Stream");
        }
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

    /// <summary>Liest den Text aus der Zwischenablage, kodiert ihn für die CLI und schreibt ihn in den
    /// Input-Stream der aktuellen Session. Fehler beim Zwischenablage-Zugriff, Kodieren oder Schreiben werden
    /// abgefangen und protokolliert, statt das Control zu beeinträchtigen.</summary>
    private async Task ReadClipboardAndInsertAsync()
    {
        var text = GetClipboardText();
        if (string.IsNullOrEmpty(text))
            return;

        var bytes = KeyToVt100Encoder.EncodeClipboardText(text);
        await WriteToInputStreamAsync(bytes, "Fehler beim Einfügen aus der Zwischenablage in den Terminal-Input-Stream").ConfigureAwait(false);
    }

    /// <summary>Schreibt Bytes asynchron in den Input-Stream der aktuellen Session und protokolliert Schreibfehler
    /// statt sie zu verschlucken. Wartet mit <c>ConfigureAwait(false)</c>, damit ein synchron blockierender Aufrufer
    /// (z. B. in Tests) nicht durch die Erfassung des UI-SynchronizationContext blockiert.</summary>
    /// <param name="bytes">Die zu schreibenden Bytes.</param>
    /// <param name="errorMessage">Die Log-Nachricht bei einem Schreibfehler.</param>
    private async Task WriteToInputStreamAsync(byte[] bytes, string errorMessage)
    {
        try
        {
            await Session!.InputStream!.WriteAsync(bytes).ConfigureAwait(false);
            Session.MarkInputActivity();
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
}
