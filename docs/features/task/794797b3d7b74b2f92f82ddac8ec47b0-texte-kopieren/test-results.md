# Testergebnisse

## Ergebnis

**Status: Fehlgeschlagen.** Ein vollständiger, erfolgreicher E2E-Nachweis liegt nicht vor. Der letzte vollständige gezielte Lauf ergab 92 von 93 bestandenen Tests; der Auswahl-Lifecycle-Test nach Scrollback war fehlgeschlagen. Nachfolgende, durch einen unterbrochenen Lauf entstandene Korrekturen sind noch nicht vollständig erneut abgenommen.

## Pflicht-E2E

| Szenario | Status |
|---|---|
| E-01 `TerminalText_LiveMarkCopyAndKeepSelection` | Registriert, aber der echte Live-Auswahl-, Overlay-, Clipboard- und Scrollback-Ablauf wird nicht geprüft. Nicht bestanden. |
| E-02 `ReplayText_MarkAndCopy` | Registriert; Maus-/Tastatur- und Clipboardteile sind vorhanden. Der sichtbare Overlay-Pixelnachweis fehlt und kein erfolgreicher aktueller E2E-Lauf liegt vor. Nicht bestanden. |

## Fehlgeschlagene Abnahmen

- `TerminalControlTests.Selection_NormalScroll_KeepsLogicalRowAndCopyText` — Auswahl wurde nach normaler Scrollback-Verschiebung verworfen; eine nachfolgende Korrektur benötigt einen erneuten Testlauf.
- E-01 — Pflichtassertions fehlen; der ConPTY-E2E-Lauf wurde ohne erfolgreichen Abschluss abgebrochen.
- E-02 — sichtbarer Pixel-/Overlaynachweis fehlt; der General-E2E-Lauf wurde ohne erfolgreichen Abschluss abgebrochen.
