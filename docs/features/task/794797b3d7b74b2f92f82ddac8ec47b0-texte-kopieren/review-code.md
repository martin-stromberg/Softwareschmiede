# Code-Review: Texte kopieren

## Status: Befunde vorhanden

Geprüft wurden alle Änderungen des Feature-Branches einschließlich der aktuellen
Arbeitsbereichsänderungen. Die Korrekturen verwenden nun echte Zeilenumbrüche
in den relevanten Tests; der Zugriff auf die Windows-Zwischenablage im
Replay-E2E hat außerdem einen begrenzten Retry. Die Selection-Validierung
bindet sich über Zeilen-IDs, Zellversionen und die Buffer-Generation an den
ursprünglichen Inhalt. Bei normalem Scrollen werden die Versionsdaten zusammen
mit den logischen Zeilen verschoben.

`RaiseUiActionRequested` kommt im Produktquellcode unter `src/` nicht vor.
Es gibt daher keine ausgelöste UI-Aktion, für die ein Blazor-Seiten- oder
Komponentenhandler fehlen könnte.

## Befunde

1. **Hoch – E-01 weist den geforderten Live-Auswahl- und Kopierfluss weiterhin nicht nach.**
   `TerminalText_LiveMarkCopyAndKeepSelection` in
   `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs` erzeugt lediglich
   einen Marker und prüft sichtbare Ausgabe sowie den laufenden Prozess. Der
   Test führt keine Maus- oder Tastaturauswahl am Live-Terminal aus, prüft kein
   Auswahl-Overlay, löst keine Ausgabe nach der Auswahl und keinen
   Scrollback-Fall aus und kontrolliert weder `Ctrl+Shift+C` noch den
   Clipboard-Inhalt. Damit fehlt der vollständige, im Plan verbindliche
   Nachweis für E-01.

2. **Hoch – E-02 prüft die sichtbare Auswahl nicht.**
   `ReplayText_MarkAndCopy` in
   `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` erzeugt jetzt
   korrekt zwei Terminalzeilen und vergleicht nach Maus- und Tastaturpfad den
   Clipboard-Text. Es nimmt jedoch keinen Vorher-/Nachher-Screenshot auf und
   führt keinen Pixelvergleich im erwarteten Auswahlrechteck aus. Der im Plan
   festgelegte visuelle Nachweis kann daher auch bei fehlendem oder falsch
   positioniertem Overlay bestehen.

3. **Mittel – Die übrige verbindliche Testabdeckung für Auswahl-Lifecycle und
   Extraktion fehlt.**
   Es fehlen gezielte Tests für Vorwärts-/Rückwärts-Extraktion, Teilzeilen,
   Leerzeilen und Leerzeichenregeln, Mauskoordinaten mit Scrolloffsets sowie
   Overlay-Clipping. Ebenso fehlen die geforderten Invalidierungsfälle
   (Überschreiben, Erase, Scrollback-Abwurf, Reset, Resize,
   Alternate-Screen- und Sessionwechsel) und ein deterministischer
   Clipboard-Schreibfehler. Der vorhandene Scrollback-Erhaltstest deckt nur
   einen Teil dieser Regeln ab.

## Positiv geprüft

- `Ctrl+Shift+C` wird bei gültiger Auswahl lokal verarbeitet und schreibt keine
  Eingabebytes; der bestehende `Ctrl+C`-Pfad bleibt unverändert.
- Die Korrektur für den Cursor hinter der letzten Spalte begrenzt die
  Auswahlspalte vor dem Zellversionszugriff.
- `TerminalBuffer.GetSnapshot` normiert nach Resize auch die Versionsarrays
  der Scrollback-Zeilen auf die aktuelle Spaltenzahl.
- Die aktuelle Teständerung verwendet für Scrollback und Replay echte
  Steuerzeichen statt literaler `\\n` beziehungsweise `\\r\\n`-Folgen.

## Prüfung

- `git diff --check` enthält keine Whitespace-Befunde; Git meldet nur die
  bekannte CRLF-Normalisierung in bestehenden Arbeitsbereichsdateien.
- Suche nach `RaiseUiActionRequested` unter `src/`: keine Vorkommen.
- Tests wurden in diesem Review nicht erneut ausgeführt; der separate
  Testschritt ist für den Ausführungsnachweis maßgeblich.

## Ergebnis

Die aktuelle Korrektur beseitigt die fehlerhafte E2E-Fixture und reduziert
Clipboard-Flakiness. Vor Abschluss müssen E-01 vollständig implementiert,
E-02 um den visuellen Pixelnachweis ergänzt und die noch ausstehenden
verbindlichen Auswahltests ergänzt sowie erfolgreich ausgeführt werden.
