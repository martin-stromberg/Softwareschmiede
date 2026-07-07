using System.Text;
using System.Windows.Input;

namespace Softwareschmiede.App.Controls;

/// <summary>Konvertiert WPF-Tastaturereignisse in VT100-Byte-Sequenzen.</summary>
internal static class KeyToVt100Encoder
{
    /// <summary>Kodiert ein WPF-Tastaturereignis als VT100-Byte-Sequenz.</summary>
    /// <param name="e">Das Tastaturereignis.</param>
    /// <returns>Die VT100-Byte-Sequenz, oder null wenn das Zeichen über TextInput übermittelt werden soll.</returns>
    internal static byte[]? Encode(KeyEventArgs e)
    {
        var ctrl = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0;

        if (ctrl && e.Key >= Key.A && e.Key <= Key.Z)
            return [unchecked((byte)(e.Key - Key.A + 1))];

        return e.Key switch
        {
            Key.Enter => [0x0D],
            Key.Back => [0x7F],
            Key.Tab => [0x09],
            Key.Escape => [0x1B],
            Key.Delete => Encoding.ASCII.GetBytes("\x1b[3~"),
            Key.Up => Encoding.ASCII.GetBytes("\x1b[A"),
            Key.Down => Encoding.ASCII.GetBytes("\x1b[B"),
            Key.Left => Encoding.ASCII.GetBytes("\x1b[D"),
            Key.Right => Encoding.ASCII.GetBytes("\x1b[C"),
            Key.Home => Encoding.ASCII.GetBytes("\x1b[H"),
            Key.End => Encoding.ASCII.GetBytes("\x1b[F"),
            Key.PageUp => Encoding.ASCII.GetBytes("\x1b[5~"),
            Key.PageDown => Encoding.ASCII.GetBytes("\x1b[6~"),
            Key.F1 => Encoding.ASCII.GetBytes("\x1bOP"),
            Key.F2 => Encoding.ASCII.GetBytes("\x1bOQ"),
            Key.F3 => Encoding.ASCII.GetBytes("\x1bOR"),
            Key.F4 => Encoding.ASCII.GetBytes("\x1bOS"),
            Key.F5 => Encoding.ASCII.GetBytes("\x1b[15~"),
            Key.F6 => Encoding.ASCII.GetBytes("\x1b[17~"),
            Key.F7 => Encoding.ASCII.GetBytes("\x1b[18~"),
            Key.F8 => Encoding.ASCII.GetBytes("\x1b[19~"),
            Key.F9 => Encoding.ASCII.GetBytes("\x1b[20~"),
            Key.F10 => Encoding.ASCII.GetBytes("\x1b[21~"),
            Key.F11 => Encoding.ASCII.GetBytes("\x1b[23~"),
            Key.F12 => Encoding.ASCII.GetBytes("\x1b[24~"),
            _ => null,
        };
    }

    /// <summary>Kodiert einen Text als UTF-8-Byte-Array.</summary>
    /// <param name="text">Der zu kodierende Text.</param>
    /// <returns>Die UTF-8-kodierten Bytes.</returns>
    internal static byte[] EncodeText(string text)
        => Encoding.UTF8.GetBytes(text);

    /// <summary>Kodiert Zwischenablage-Text für die CLI-Eingabe: Zeilenumbrüche (<c>\n</c>, <c>\r\n</c>, <c>\r</c>)
    /// werden einheitlich zu <c>\r</c> (CR) normalisiert, das Ergebnis wird als UTF-8 kodiert.</summary>
    /// <param name="text">Der zu kodierende Zwischenablage-Text, oder <see langword="null"/>.</param>
    /// <returns>Die UTF-8-kodierten, newline-normalisierten Bytes, oder ein leeres Array bei <see langword="null"/> oder leerem Text.</returns>
    internal static byte[] EncodeClipboardText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var normalized = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\r':
                    normalized.Append('\r');
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    break;
                case '\n':
                    normalized.Append('\r');
                    break;
                default:
                    normalized.Append(c);
                    break;
            }
        }

        return Encoding.UTF8.GetBytes(normalized.ToString());
    }
}
