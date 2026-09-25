using System.Drawing;

namespace Softwareschmiede.Domain.Terminal;

/// <summary>Basistyp für alle Terminal-Ereignisse, die vom ANSI-Parser erzeugt werden.</summary>
public abstract record TerminalEvent;

/// <summary>Klartext, der in den Terminal-Buffer geschrieben werden soll.</summary>
/// <param name="Text">Der auszugebende Text.</param>
/// <returns>Eine neue <see cref="TextWrittenEvent"/>-Instanz.</returns>
public sealed record TextWrittenEvent(string Text) : TerminalEvent;

/// <summary>Absolute Cursor-Positionierung.</summary>
/// <param name="Row">Zielzeile (0-basiert); ein negativer Wert behält die aktuelle Zeile (z. B. CHA/HPA).</param>
/// <param name="Col">Zielspalte (0-basiert); ein negativer Wert behält die aktuelle Spalte (z. B. VPA).</param>
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

/// <summary>Alternate Screen ein-/ausschalten (DECSET/DECRST 47/1047/1049).</summary>
/// <param name="Enabled">true beim Wechsel in den Alternate Screen, false beim Verlassen.</param>
/// <returns>Eine neue <see cref="AlternateScreenChangedEvent"/>-Instanz.</returns>
public sealed record AlternateScreenChangedEvent(bool Enabled) : TerminalEvent;

/// <summary>Leerzeilen an der Cursorposition einfügen (IL: CSI Ps L).</summary>
/// <param name="Count">Anzahl der einzufügenden Zeilen.</param>
/// <returns>Eine neue <see cref="LinesInsertedEvent"/>-Instanz.</returns>
public sealed record LinesInsertedEvent(int Count) : TerminalEvent;

/// <summary>Zeilen an der Cursorposition löschen (DL: CSI Ps M).</summary>
/// <param name="Count">Anzahl der zu löschenden Zeilen.</param>
/// <returns>Eine neue <see cref="LinesDeletedEvent"/>-Instanz.</returns>
public sealed record LinesDeletedEvent(int Count) : TerminalEvent;

/// <summary>Leerzeichen an der Cursorposition einfügen (ICH: CSI Ps @).</summary>
/// <param name="Count">Anzahl der einzufügenden Zeichen.</param>
/// <returns>Eine neue <see cref="CharsInsertedEvent"/>-Instanz.</returns>
public sealed record CharsInsertedEvent(int Count) : TerminalEvent;

/// <summary>Zeichen an der Cursorposition löschen (DCH: CSI Ps P).</summary>
/// <param name="Count">Anzahl der zu löschenden Zeichen.</param>
/// <returns>Eine neue <see cref="CharsDeletedEvent"/>-Instanz.</returns>
public sealed record CharsDeletedEvent(int Count) : TerminalEvent;

/// <summary>Zeichen ab der Cursorposition durch Leerzeichen ersetzen (ECH: CSI Ps X).</summary>
/// <param name="Count">Anzahl der zu löschenden Zeichen.</param>
/// <returns>Eine neue <see cref="CharsErasedEvent"/>-Instanz.</returns>
public sealed record CharsErasedEvent(int Count) : TerminalEvent;

/// <summary>Scroll-Region setzen (DECSTBM: CSI Pt;Pb r).</summary>
/// <param name="Top">Obere Region-Grenze (0-basiert, inklusiv).</param>
/// <param name="Bottom">Untere Region-Grenze (0-basiert, inklusiv); ein negativer Wert bedeutet "letzte Zeile des Screens".</param>
/// <returns>Eine neue <see cref="ScrollRegionChangedEvent"/>-Instanz.</returns>
public sealed record ScrollRegionChangedEvent(int Top, int Bottom) : TerminalEvent;

/// <summary>Bildschirminhalt innerhalb der Scroll-Region scrollen (SU: CSI Ps S, SD: CSI Ps T).</summary>
/// <param name="DeltaRows">Anzahl der Scroll-Schritte (positiv = nach oben, negativ = nach unten).</param>
/// <returns>Eine neue <see cref="ScreenScrolledEvent"/>-Instanz.</returns>
public sealed record ScreenScrolledEvent(int DeltaRows) : TerminalEvent;

/// <summary>Cursorposition speichern bzw. wiederherstellen (DECSC/DECRC: ESC 7/8, CSI s/u).</summary>
/// <param name="Restored">true für Wiederherstellen, false für Speichern.</param>
/// <returns>Eine neue <see cref="CursorSavedEvent"/>-Instanz.</returns>
public sealed record CursorSavedEvent(bool Restored) : TerminalEvent;

/// <summary>Vollständiger Terminal-Reset (RIS: ESC c).</summary>
/// <returns>Eine neue <see cref="TerminalResetEvent"/>-Instanz.</returns>
public sealed record TerminalResetEvent() : TerminalEvent;
