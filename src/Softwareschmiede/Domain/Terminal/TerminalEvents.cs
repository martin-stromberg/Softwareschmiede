using System.Drawing;

namespace Softwareschmiede.Domain.Terminal;

/// <summary>Basistyp für alle Terminal-Ereignisse, die vom ANSI-Parser erzeugt werden.</summary>
public abstract record TerminalEvent;

/// <summary>Klartext, der in den Terminal-Buffer geschrieben werden soll.</summary>
/// <param name="Text">Der auszugebende Text.</param>
/// <returns>Eine neue <see cref="TextWrittenEvent"/>-Instanz.</returns>
public sealed record TextWrittenEvent(string Text) : TerminalEvent;

/// <summary>Absolute Cursor-Positionierung.</summary>
/// <param name="Row">Zielzeile (0-basiert).</param>
/// <param name="Col">Zielspalte (0-basiert).</param>
/// <param name="IsAbsolute">true für absolute, false für relative Positionierung.</param>
/// <returns>Eine neue <see cref="CursorMovedEvent"/>-Instanz.</returns>
public sealed record CursorMovedEvent(int Row, int Col, bool IsAbsolute) : TerminalEvent;

/// <summary>Relative Cursor-Bewegung.</summary>
/// <param name="DeltaRow">Zeilendelta (positiv = nach unten).</param>
/// <param name="DeltaCol">Spaltendelta (positiv = nach rechts).</param>
/// <returns>Eine neue <see cref="CursorMovedRelativeEvent"/>-Instanz.</returns>
public sealed record CursorMovedRelativeEvent(int DeltaRow, int DeltaCol) : TerminalEvent;

/// <summary>SGR-Farbänderung (Select Graphic Rendition).</summary>
/// <param name="Foreground">Neue Vordergrundfarbe, oder null wenn unverändert.</param>
/// <param name="Background">Neue Hintergrundfarbe, oder null wenn unverändert.</param>
/// <param name="Bold">Neuer Bold-Zustand, oder null wenn unverändert.</param>
/// <param name="Dim">Neuer Dim-Zustand, oder null wenn unverändert.</param>
/// <param name="Underline">Neuer Underline-Zustand, oder null wenn unverändert.</param>
/// <param name="Reset">true wenn alle Attribute auf Standard zurückgesetzt werden sollen.</param>
/// <returns>Eine neue <see cref="ColorChangedEvent"/>-Instanz.</returns>
public sealed record ColorChangedEvent(
    Color? Foreground,
    Color? Background,
    bool? Bold,
    bool? Dim,
    bool? Underline,
    bool Reset) : TerminalEvent;

/// <summary>Bildschirminhalt löschen.</summary>
/// <param name="Mode">Löschmodus: 0 = Cursor bis Ende, 1 = Anfang bis Cursor, 2 = Alles.</param>
/// <returns>Eine neue <see cref="ScreenClearedEvent"/>-Instanz.</returns>
public sealed record ScreenClearedEvent(int Mode) : TerminalEvent;

/// <summary>Zeile löschen.</summary>
/// <param name="Mode">Löschmodus: 0 = Cursor bis Zeilenende, 1 = Zeilenanfang bis Cursor, 2 = Ganze Zeile.</param>
/// <returns>Eine neue <see cref="LineErasedEvent"/>-Instanz.</returns>
public sealed record LineErasedEvent(int Mode) : TerminalEvent;

/// <summary>Cursor-Sichtbarkeit ändern.</summary>
/// <param name="Visible">true wenn der Cursor sichtbar sein soll, false wenn versteckt.</param>
/// <returns>Eine neue <see cref="CursorVisibilityChangedEvent"/>-Instanz.</returns>
public sealed record CursorVisibilityChangedEvent(bool Visible) : TerminalEvent;
