# Offene Aufgaben

Erstellt am: 2026-10-05
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] E-01 `TerminalText_LiveMarkCopyAndKeepSelection` als vollständigen Live-Nachweis umsetzen: Maus- und Tastaturauswahl, sichtbare Hervorhebung per Pixelvergleich, Erhalt nach weiterer Ausgabe und Scrollback-Verschiebung sowie exakter STA-Clipboardvergleich.
- [ ] E-02 `ReplayText_MarkAndCopy` mit echten CR/LF-Zeilen statt literalen `\\r\\n` ausführen, die sichtbare Auswahl per Vorher-/Nachher-Pixelvergleich nachweisen und beide Clipboardpfade am echten Replay-Control prüfen.
- [ ] Fehlende Control-/Buffer-Tests ergänzen: Extraktionsgrenzen, Leerzeichen- und Zeilenumbruchregeln, Mauskoordinaten mit Scrolloffset, sichtbares Overlay, Invalidierung durch Mutation/Erase/Abwurf/Reset/Resize/Screen-/Sessionwechsel sowie kontrollierter Clipboard-Schreibfehler.
- [ ] Den fehlschlagenden Lifecycle-Test `Selection_NormalScroll_KeepsLogicalRowAndCopyText` korrigieren, sodass eine Auswahl nach normaler Scrollback-Verschiebung derselben logischen Zeile zugeordnet bleibt und erneut kopiert werden kann.

## Code-Review-Befunde

- [ ] E-01 um reale Zellkoordinaten, Mausdrag, Tastaturauswahl, Screenshot-/Pixelnachweis, Clipboard-Retry, weitere Ausgabe außerhalb der Auswahl und einen deterministischen Scrollback-Fall ergänzen.
- [ ] E-02 um Pixel-/Overlayprüfung, korrekte CR/LF-Fixture, begrenzten STA-Clipboard-Retry und einen dokumentierten Fokus-/Caret-Nachweis für den Tastaturpfad ergänzen.

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

- [ ] `TerminalControlTests.Selection_NormalScroll_KeepsLogicalRowAndCopyText` — `_selection` ist nach normaler Scrollback-Verschiebung `null`, obwohl die logische Zeile weiterhin vorhanden sein soll.
- [ ] E-01 `TerminalText_LiveMarkCopyAndKeepSelection` — Pflichtassertions fehlen; `RunConPtyTests` lief über 90 Sekunden ohne Fortschritt und wurde abgebrochen.
- [ ] E-02 `ReplayText_MarkAndCopy` — fehlerhafte Zeilenumbruch-Fixture sowie fehlende Pixel- und Negativassertionen; `RunGeneralTests` lief über 90 Sekunden ohne Fortschritt und wurde abgebrochen.
