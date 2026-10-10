# Offene Aufgaben

Erstellt am: 2026-10-05
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [x] E-01 `TerminalText_LiveMarkCopyAndKeepSelection` als isolierten Live-Nachweis umgesetzt und bestanden: UIA-Viewportgeometrie, echter Mausdrag, sichtbare Hervorhebung per Pixelvergleich, Strg+Umschalt+C, Erhalt nach weiterer Ausgabe und Scrollback-Verschiebung sowie exakter STA-Clipboardvergleich.
- [x] E-02 aufgeteilt und bestanden: `ReplayText_MarkAndCopyViaMouse_E2E` belegt Auswahl-Overlay und Mehrzeilenkopie im echten Replay-Fenster; `ReplayText_CopyShortcutWithFocusedSelection_E2E` belegt UIA-Fokus und Strg+Umschalt+C auf einer bestehenden Auswahl. `OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` belegt den Umschalt-Pfeil-Pfad mit echten WPF-`KeyEventArgs`, gültigem Renderer-Overlay und Clipboard. Der vorhandene Test `OnPreviewKeyDown_CtrlShiftC_CopiesSelectionWithoutCliInput` ist der Negativnachweis für fehlende CLI-Eingabe. Die frühere native Windows-Injektion wurde entfernt, weil sie Modifier im E2E-Testdesktop nicht zuverlässig an WPF liefert und kein Produktfehler ist.
- [ ] Fehlende Control-/Buffer-Tests ergänzen: Extraktionsgrenzen, Leerzeichen- und Zeilenumbruchregeln, Mauskoordinaten mit Scrolloffset, sichtbares Overlay, Invalidierung durch Mutation/Erase/Abwurf/Reset/Resize/Screen-/Sessionwechsel sowie kontrollierter Clipboard-Schreibfehler.
- [x] `Selection_NormalScroll_KeepsLogicalRowAndCopyText` korrigiert und im gezielten Lauf bestanden; Auswahl bleibt auch bei neuer Ausgabe außerhalb der Auswahl erhalten.

## Code-Review-Befunde

- [x] E-01 um reale UIA-Zellkoordinaten, Mausdrag, Screenshot-/Pixelnachweis, Clipboard-Retry, weitere Ausgabe außerhalb der Auswahl und einen deterministischen Scrollback-Fall ergänzt; isolierter E2E-Lauf bestanden.
- [x] E-02 um Pixel-/Overlayprüfung, korrekte CR/LF-Fixture und begrenzten STA-Clipboard-Retry ergänzt; erfolgreicher E2E-Lauf und Negativnachweis bleiben offen.

## Usability-Befunde

- [ ] Auswahl bei neuer CLI-Ausgabe außerhalb der Auswahl erhalten: Der aktuelle Snapshot-/Validierungspfad verwirft die Markierung noch, obwohl die ausgewählten Zellen unverändert vorhanden sind.

## Fehlgeschlagene Tests

- [x] `TerminalControlTests.Selection_NormalScroll_KeepsLogicalRowAndCopyText` — im gezielten Lauf bestanden.
- [x] E-01 `TerminalText_LiveMarkCopyAndKeepSelection` — isolierter E2E-Lauf bestanden (1/1, 22 s).
- [x] E-02 — in getrennten, gezielt ausführbaren Maus-, Fokus/Kopierkürzel- und WPF-Modifiertests bestanden; keine fragile Shift-Injektion mehr als Produktnachweis verwendet.
