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
}
