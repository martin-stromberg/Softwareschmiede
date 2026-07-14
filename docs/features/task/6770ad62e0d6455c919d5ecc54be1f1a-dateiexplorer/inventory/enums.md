# Enums

## `DiffLineStatus`
Datei: `src/Softwareschmiede/Domain/Enums/DiffLineStatus.cs`

Status einer einzelnen Zeile innerhalb eines Diffs.

| Wert | Bedeutung |
|------|-----------|
| `Added` | Zeile wurde hinzugefügt (im neuen/Ziel-Inhalt vorhanden, nicht im alten/Quell-Inhalt) |
| `Removed` | Zeile wurde gelöscht (im alten/Quell-Inhalt vorhanden, nicht im neuen/Ziel-Inhalt) |
| `Modified` | Zeile wurde modifiziert (vorhanden in beiden, aber mit geändertem Inhalt) |
| `Context` | Zeile ist unverändert (Kontextzeile, umgebend Änderungen) |

**Verwendung:** Markiert den Änderungsstatus einzelner Zeilen in `DiffLine` und wird für die farbliche Kennzeichnung im UI verwendet (grün = Added, rot = Removed, orange = Modified).

## `DiffType`
Datei: `src/Softwareschmiede/Domain/Enums/DiffType.cs`

Rendering-Typ für Diff-Darstellung.

| Wert | Bedeutung |
|------|-----------|
| `Full` | Unified-View (einzelner Stream mit +/- Präfixen) |
| `SideBySide` | Side-by-Side-View (zwei Spalten nebeneinander) |
| `Split` | Split-View (mit Gutter zwischen Original und Neu) |

**Verwendung:** Bestimmt, wie der Diff im DiffViewer angezeigt wird. Die Anforderung beschreibt eine "split-view-Architektur", was einer Mischung aus Side-by-Side und Split entspricht.

## `DiffResultStatus`
Datei: `src/Softwareschmiede/Domain/Enums/DiffResultStatus.cs`

Status eines Diff-Ergebnisses (in der Datenbank persistiert).

| Wert | Bedeutung |
|------|-----------|
| `Pending` | Diff-Generierung steht aus (in Warteschlange) |
| `Generated` | Diff wurde erfolgreich generiert |
| `Cached` | Diff wurde aus Cache geladen |
| `Error` | Fehler bei der Diff-Generierung |

**Verwendung:** Markiert den Zustand eines gespeicherten `DiffResult` und wird für Statistiken und Fehlerbehandlung verwendet.

## Noch nicht vorhanden:

### `DateibrowserAnsichtsmodus`
Benötigt für:
- Auswahl zwischen Standardansicht (alle Dateien) und Vergleichsmodus (nur geänderte)
- Modus-Umschaltung in `TaskDetailViewModel`

| Wert | Bedeutung |
|------|-----------|
| `Standard` | Standardansicht: Alle Dateien des Repositories werden angezeigt |
| `Vergleich` | Vergleichsmodus: Nur im Branch modifizierte Dateien werden angezeigt, gruppiert nach Commits |

### `FileChangeType`
Benötigt für:
- Markierung der Änderungsart einer Datei im Vergleichsmodus
- Filterung und Anzeige-Optionen

| Wert | Bedeutung |
|------|-----------|
| `Added` | Datei wurde neu hinzugefügt |
| `Modified` | Datei wurde geändert |
| `Deleted` | Datei wurde gelöscht |
| `Renamed` | Datei wurde umbenannt |
| `CopiedFrom` | Datei wurde aus anderer Datei kopiert |

