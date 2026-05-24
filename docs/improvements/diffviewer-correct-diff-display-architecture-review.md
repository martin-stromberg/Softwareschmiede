# Architektur-Review: Korrekte Diff-Anzeige im DiffViewer

## 1. Review-Zusammenfassung
Die Hauptursache ist eine globale Diff-Auswahl statt dateispezifischer Diff-Zuordnung. Dadurch entstehen falsche „kein DiffResult“-Meldungen oder fachlich falsche Diffs.

## 2. Bewertung
| Bereich | Bewertung | Begründung |
|--------|-----------|------------|
| Systemarchitektur | ⚠️ | Dateiauswahl und DiffLookup sind nicht eindeutig gekoppelt |
| Technologieentscheidungen | ✅ | Stack geeignet; Query-Schnittstelle muss erweitert werden |
| UI/UX | ⚠️ | Kein-Diff-Zustand wird teils falsch angezeigt |
| Qualitätsziele | ⚠️ | Zuverlässigkeit aktuell nicht erfüllt |

## 3. Identifizierte Schwachstellen
1. Globaler Latest-Diff wird im UI-Kontext wiederverwendet.
2. Kein klarer Zustandspfad für Dateiwechsel und Diff-Lookup.
3. Unzureichende Regressionstests für +1-Zeile und Dateiwechsel.

## 4. Priorisierte Maßnahmen
| Priorität | Maßnahme | Wirkung |
|-----------|----------|---------|
| P0 | Dateispezifische Query (`AufgabeId + FilePath`) einführen | behebt Kernfehler |
| P0 | DiffPreviewPanel nur mit dateispezifischer DiffResultId ansteuern | verhindert falsche Anzeige |
| P1 | Pfadnormalisierung zentralisieren | reduziert Mapping-Fehler |
| P1 | Tests für +1-Zeile, Null-Diff, Dateiwechsel ergänzen | regressionssicher |
| P2 | Logging/Diagnostik entlang Auswahl -> Lookup -> Render | schnellere Fehleranalyse |

## 5. Abnahme- und Regressionkriterien
- Geänderte Datei zeigt ihren realen Diff.
- Eine +1-Zeile ist sichtbar.
- Kein-Diff-Meldung erscheint nur bei tatsächlichem Null-Fall.
- Wechsel zwischen Datei A/B zeigt nie den Diff der jeweils anderen Datei.

## 6. Ergebnis
**Go mit Nachbesserung:** Architektur tragfähig, aber P0-Maßnahmen sind zwingend vor Umsetzung/Abnahme.
