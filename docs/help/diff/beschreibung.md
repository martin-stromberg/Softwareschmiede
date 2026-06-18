# Diff-Anzeige — Beschreibung

## Zweck

Die Diff-Anzeige ermöglicht es, Dateiänderungen auf Branch-Ebene zu reviewen. Sie zeigt hinzugefügte, geänderte und entfernte Zeilen farblich hervorgehoben an. Ergebnisse werden gecacht, um wiederholte Berechnungen zu vermeiden.

## Funktionsweise

### DiffViewer-Komponente

Die Hauptkomponente `DiffViewer` zeigt einen `DiffResult` an. Sie besteht aus:

- **`DiffHeader`** — Dateiname und Metadaten
- **`DiffToolbar`** — Umschalten zwischen Seite-an-Seite und einzeiligem Modus
- **`DiffContent`** — Scrollbare Liste der `DiffBlock`-Elemente
- **`DiffLine`** — Einzelne Zeile mit Statusmarkierung (hinzugefügt, entfernt, unverändert)
- **`DiffFooter`** — Zusammenfassung (Anzahl Änderungen)

### DiffPreviewPanel

In der Aufgabendetailansicht (Register „Projektverzeichnis") zeigt `DiffPreviewPanel` eine Vorschau einer ausgewählten Datei oder eines Commit-Diffs.

### Caching

Der `DiffCachingService` speichert berechnete Diffs als `DiffCache`-Einträge in der SQLite-Datenbank. Die Caching-Strategie ist konfigurierbar (`DiffCachingStrategy`).

## Beispiele

- Nach einem KI-Lauf wird der Diff per Button „🔎 Diff anzeigen" in der Aufgabendetailansicht geöffnet.
- Im Register „Projektverzeichnis" klickt der Anwender eine geänderte Datei im Repository-Explorer an, um die Zeilenvorschau zu sehen.

## Einschränkungen

- Diffs werden für Git-Repositories berechnet; bei `LocalDirectoryPlugin` kann die Verfügbarkeit eingeschränkt sein.
- Sehr große Dateien können zu langen Berechnungszeiten führen.
