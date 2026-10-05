# Testergebnisse

## Ergebnis

**Status: Fehlgeschlagen.** Der Build im gezielten Testlauf war erfolgreich. Von 93 gezielt ausgeführten Buffer-, Control- und Encoder-Tests bestanden 92; ein neuer Lifecycle-Test zur Auswahl nach Scrollback-Verschiebung schlug fehl. E-01 und E-02 sind im E2E-Runner aufrufbar, ihre E2E-Suiten ließen sich in dieser Umgebung jedoch nicht erfolgreich abschließen. Zudem erfüllen die gegenwärtigen E2E-Methoden wesentliche Assertions aus dem Plan nicht.

## Pflicht-E2E: Existenz, Registrierung und Inhalt

| Szenario | Existenz / Registrierung | Inhaltsprüfung | Lauf / Ergebnis |
|---|---|---|---|
| E-01 `TerminalText_LiveMarkCopyAndKeepSelection` | Methode in `E2E_ConPtyLifecycle.cs`; wird aus dem registrierten `RunConPtyTests`-Lifecycle aufgerufen. | Unzureichend: schreibt nur eine einzelne `echo`-Markerzeile, prüft Output-Protokoll, sichtbares Terminal und laufenden Prozess. Keine Maus- oder Tastaturauswahl, kein Clipboardvergleich, keine Pixelprüfung, kein Erhalt nach Ausgabe und kein Scrollback-Fall. | `RunConPtyTests` gestartet, blieb über 90 Sekunden ohne weitere Ausgabe und wurde abgebrochen. Szenario nicht erfolgreich nachgewiesen; **fehlgeschlagen**. |
| E-02 `ReplayText_MarkAndCopy` | Methode in `E2E_KonsolenTestfenster.cs`; wird aus dem registrierten `RunGeneralTests` aufgerufen. | Teilweise: synthetisches Replay, Maus- und Shift+End-Auswahl sowie Clipboard-Text-Assertions sind implementiert. Fehlend: Pixel-/Overlayprüfung und expliziter Nachweis, dass keine Live-Prozess-Eingabe erfolgte. Fixture-String enthält `\\r\\n` als literale Backslash-Zeichenfolge statt `\r\n`; damit erzeugt er keine zwei Zeilen, obwohl der Assert CRLF/`Environment.NewLine` erwartet. | `RunGeneralTests` gestartet, blieb über 90 Sekunden ohne weitere Ausgabe und wurde abgebrochen. E-02 nicht erfolgreich nachgewiesen; **fehlgeschlagen**. |

Die Suite-Aufrufe sind vorhanden, aber da beide Tests private Phasen innerhalb großer E2E-Läufe sind, kann ihr individueller Laufstatus aus dem abgebrochenen Runner nicht festgestellt werden. Fehlender Abschluss zählt gemäß Plan als fehlgeschlagene Abnahme.

## Ausgeführte Tests

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "FullyQualifiedName~TerminalBufferTests|FullyQualifiedName~TerminalControlTests|FullyQualifiedName~KeyToVt100EncoderTests"` — Build erfolgreich; **92 bestanden, 1 fehlgeschlagen, 0 übersprungen**.
- Fehlgeschlagen: `TerminalControlTests.Selection_NormalScroll_KeepsLogicalRowAndCopyText`. Assertion: `_selection` war nach normaler Scrollback-Verschiebung `null`, obwohl die logische Zeile weiterhin vorhanden war. Fundstelle: `TerminalControlTests.KeyInput.cs:106`.
- `dotnet test ... --no-build --no-restore --filter "FullyQualifiedName~RunGeneralTests"` — nach ca. 90 Sekunden ohne Fortschritt abgebrochen; kein Testergebnis für E-02.
- `dotnet test ... --no-build --no-restore --filter "FullyQualifiedName~RunConPtyTests"` — nach ca. 90 Sekunden ohne Fortschritt abgebrochen; kein Testergebnis für E-01.

## Fehlgeschlagene Abnahmen

- Auswahl-Lifecycle-Test `Selection_NormalScroll_KeepsLogicalRowAndCopyText`.
- E-01: Pflichtassertions fehlen und der ConPTY-E2E-Lauf wurde nicht erfolgreich abgeschlossen.
- E-02: Zeilenumbruch-Fixture ist falsch escaped, Pixel- und Negativassertion fehlen, der allgemeine E2E-Lauf wurde nicht erfolgreich abgeschlossen.
- Vollständiger erfolgreicher E2E-Nachweis für Live und Replay liegt nicht vor.
