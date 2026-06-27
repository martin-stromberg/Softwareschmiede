using System.Drawing;

namespace Softwareschmiede.Domain.Terminal;

/// <summary>Repräsentiert eine einzelne Zelle im Terminal-Grid.</summary>
public record struct TerminalCell
{
    /// <summary>Das angezeigte Zeichen.</summary>
    public char Character { get; init; }

    /// <summary>Vordergrundfarbe der Zelle.</summary>
    public Color Foreground { get; init; }

    /// <summary>Hintergrundfarbe der Zelle.</summary>
    public Color Background { get; init; }

    /// <summary>Gibt an, ob der Text fett dargestellt wird.</summary>
    public bool Bold { get; init; }

    /// <summary>Gibt an, ob der Text unterstrichen dargestellt wird.</summary>
    public bool Underline { get; init; }

    /// <summary>Gibt an, ob der Text gedimmt dargestellt wird.</summary>
    public bool Dim { get; init; }

    /// <summary>Die Standardzelle mit Leerzeichen und Standardfarben.</summary>
    /// <value>Eine <see cref="TerminalCell"/> mit Leerzeichen, heller grauer Vordergrundfarbe und schwarzem Hintergrund.</value>
    public static TerminalCell Default => new()
    {
        Character = ' ',
        Foreground = Color.FromArgb(229, 229, 229),
        Background = Color.Black,
        Bold = false,
        Underline = false,
        Dim = false,
    };
}
