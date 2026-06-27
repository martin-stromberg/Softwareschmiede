using System.Drawing;
using System.Text;
using Softwareschmiede.Domain.Terminal;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Zustandsbehafteter VT100/ANSI-Parser. Verarbeitet Byte-Blöcke und erzeugt <see cref="TerminalEvent"/>-Instanzen.</summary>
public sealed class AnsiSequenceParser
{
    private enum State { Normal, Escape, Csi, CsiQuestion, Osc }

    private State _state = State.Normal;
    private readonly StringBuilder _paramBuffer = new();
    private readonly List<byte> _textBuffer = new();

    private static readonly Color[] StandardColors =
    [
        Color.FromArgb(0, 0, 0),
        Color.FromArgb(205, 0, 0),
        Color.FromArgb(0, 205, 0),
        Color.FromArgb(205, 205, 0),
        Color.FromArgb(0, 0, 238),
        Color.FromArgb(205, 0, 205),
        Color.FromArgb(0, 205, 205),
        Color.FromArgb(229, 229, 229),
        Color.FromArgb(127, 127, 127),
        Color.FromArgb(255, 0, 0),
        Color.FromArgb(0, 255, 0),
        Color.FromArgb(255, 255, 0),
        Color.FromArgb(92, 92, 255),
        Color.FromArgb(255, 0, 255),
        Color.FromArgb(0, 255, 255),
        Color.FromArgb(255, 255, 255),
    ];

    /// <summary>Verarbeitet einen Byte-Block und gibt die erzeugten Terminal-Ereignisse zurück.</summary>
    /// <param name="data">Der zu parsende Byte-Block.</param>
    /// <returns>Eine Sequenz von <see cref="TerminalEvent"/>-Instanzen.</returns>
    public IEnumerable<TerminalEvent> Parse(ReadOnlySpan<byte> data)
    {
        var events = new List<TerminalEvent>();

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];

            switch (_state)
            {
                case State.Normal:
                    if (b == 0x1B)
                    {
                        FlushText(events);
                        _state = State.Escape;
                    }
                    else
                    {
                        _textBuffer.Add(b);
                    }
                    break;

                case State.Escape:
                    if (b == (byte)'[')
                    {
                        _paramBuffer.Clear();
                        _state = State.Csi;
                    }
                    else if (b == (byte)']')
                    {
                        _paramBuffer.Clear();
                        _state = State.Osc;
                    }
                    else
                    {
                        _state = State.Normal;
                    }
                    break;

                case State.Csi:
                    if (b == (byte)'?')
                    {
                        _state = State.CsiQuestion;
                    }
                    else if (b >= 0x40 && b <= 0x7E)
                    {
                        ProcessCsiCommand((char)b, _paramBuffer.ToString(), events);
                        _state = State.Normal;
                        _paramBuffer.Clear();
                    }
                    else
                    {
                        _paramBuffer.Append((char)b);
                    }
                    break;

                case State.CsiQuestion:
                    if (b >= 0x40 && b <= 0x7E)
                    {
                        ProcessCsiQuestionCommand((char)b, _paramBuffer.ToString(), events);
                        _state = State.Normal;
                        _paramBuffer.Clear();
                    }
                    else
                    {
                        _paramBuffer.Append((char)b);
                    }
                    break;

                case State.Osc:
                    if (b == 0x07)
                    {
                        _state = State.Normal;
                        _paramBuffer.Clear();
                    }
                    else if (b == 0x1B)
                    {
                        // Zweistelliger String-Terminator ESC \: das nachfolgende \ überspringen.
                        if (i + 1 < data.Length && data[i + 1] == 0x5C)
                            i++;
                        _state = State.Normal;
                        _paramBuffer.Clear();
                    }
                    break;
            }
        }

        FlushText(events);
        return events;
    }

    private void FlushText(List<TerminalEvent> events)
    {
        if (_textBuffer.Count == 0)
            return;

        var text = Encoding.UTF8.GetString(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_textBuffer));
        _textBuffer.Clear();

        if (text.Length > 0)
            events.Add(new TextWrittenEvent(text));
    }

    private static void ProcessCsiCommand(char command, string paramStr, List<TerminalEvent> events)
    {
        var parts = ParseParams(paramStr);

        switch (command)
        {
            case 'A':
                events.Add(new CursorMovedRelativeEvent(-(GetParam(parts, 0, 1)), 0));
                break;
            case 'B':
                events.Add(new CursorMovedRelativeEvent(GetParam(parts, 0, 1), 0));
                break;
            case 'C':
                events.Add(new CursorMovedRelativeEvent(0, GetParam(parts, 0, 1)));
                break;
            case 'D':
                events.Add(new CursorMovedRelativeEvent(0, -(GetParam(parts, 0, 1))));
                break;
            case 'H':
            case 'f':
                var row = Math.Max(0, GetParam(parts, 0, 1) - 1);
                var col = Math.Max(0, GetParam(parts, 1, 1) - 1);
                events.Add(new CursorMovedEvent(row, col, IsAbsolute: true));
                break;
            case 'J':
                events.Add(new ScreenClearedEvent(GetParam(parts, 0, 0)));
                break;
            case 'K':
                events.Add(new LineErasedEvent(GetParam(parts, 0, 0)));
                break;
            case 'm':
                ParseSgr(parts, events);
                break;
        }
    }

    private static void ProcessCsiQuestionCommand(char command, string paramStr, List<TerminalEvent> events)
    {
        var parts = ParseParams(paramStr);
        var p = GetParam(parts, 0, 0);

        if (p == 25)
        {
            events.Add(new CursorVisibilityChangedEvent(command == 'h'));
        }
    }

    private static void ParseSgr(int[] parts, List<TerminalEvent> events)
    {
        if (parts.Length == 0)
        {
            events.Add(new ColorChangedEvent(null, null, null, null, null, Reset: true));
            return;
        }

        var i = 0;
        while (i < parts.Length)
        {
            var code = parts[i];
            switch (code)
            {
                case 0:
                    events.Add(new ColorChangedEvent(null, null, null, null, null, Reset: true));
                    break;
                case 1:
                    events.Add(new ColorChangedEvent(null, null, Bold: true, null, null, Reset: false));
                    break;
                case 2:
                    events.Add(new ColorChangedEvent(null, null, null, Dim: true, null, Reset: false));
                    break;
                case 4:
                    events.Add(new ColorChangedEvent(null, null, null, null, Underline: true, Reset: false));
                    break;
                case 22:
                    events.Add(new ColorChangedEvent(null, null, Bold: false, Dim: false, null, Reset: false));
                    break;
                case 24:
                    events.Add(new ColorChangedEvent(null, null, null, null, Underline: false, Reset: false));
                    break;
                case >= 30 and <= 37:
                    events.Add(new ColorChangedEvent(StandardColors[code - 30], null, null, null, null, Reset: false));
                    break;
                case 38:
                    var fg = ParseExtendedColor(parts, ref i);
                    if (fg.HasValue)
                        events.Add(new ColorChangedEvent(fg, null, null, null, null, Reset: false));
                    break;
                case 39:
                    events.Add(new ColorChangedEvent(TerminalCell.Default.Foreground, null, null, null, null, Reset: false));
                    break;
                case >= 40 and <= 47:
                    events.Add(new ColorChangedEvent(null, StandardColors[code - 40], null, null, null, Reset: false));
                    break;
                case 48:
                    var bg = ParseExtendedColor(parts, ref i);
                    if (bg.HasValue)
                        events.Add(new ColorChangedEvent(null, bg, null, null, null, Reset: false));
                    break;
                case 49:
                    events.Add(new ColorChangedEvent(null, TerminalCell.Default.Background, null, null, null, Reset: false));
                    break;
                case >= 90 and <= 97:
                    events.Add(new ColorChangedEvent(StandardColors[code - 90 + 8], null, null, null, null, Reset: false));
                    break;
                case >= 100 and <= 107:
                    events.Add(new ColorChangedEvent(null, StandardColors[code - 100 + 8], null, null, null, Reset: false));
                    break;
            }
            i++;
        }
    }

    private static Color? ParseExtendedColor(int[] parts, ref int i)
    {
        if (i + 1 >= parts.Length)
            return null;

        var mode = parts[i + 1];
        if (mode == 5 && i + 2 < parts.Length)
        {
            i += 2;
            return GetColor256(parts[i]);
        }

        if (mode == 2 && i + 4 < parts.Length)
        {
            i += 4;
            return Color.FromArgb(parts[i - 2], parts[i - 1], parts[i]);
        }

        return null;
    }

    private static Color GetColor256(int index)
    {
        if (index < 16)
            return StandardColors[index];

        if (index < 232)
        {
            var n = index - 16;
            var b = n % 6;
            var g = (n / 6) % 6;
            var r = n / 36;
            return Color.FromArgb(r == 0 ? 0 : r * 40 + 55, g == 0 ? 0 : g * 40 + 55, b == 0 ? 0 : b * 40 + 55);
        }

        var gray = (index - 232) * 10 + 8;
        return Color.FromArgb(gray, gray, gray);
    }

    private static int[] ParseParams(string paramStr)
    {
        if (string.IsNullOrEmpty(paramStr))
            return [];

        var parts = paramStr.Split(';');
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            int.TryParse(parts[i], out result[i]);
        return result;
    }

    private static int GetParam(int[] parts, int index, int defaultValue)
        => index < parts.Length ? parts[index] : defaultValue;
}
