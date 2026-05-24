# Anforderungsanalyse: Korrekte Diff-Anzeige im DiffViewer für geänderte Dateien

## 1. Überblick und Projektkontext
- **Projekt:** Softwareschmiede
- **Feature:** Korrekte Diff-Anzeige im DiffViewer für geänderte Dateien
- **Geschäftsziel:** Änderungen pro ausgewählter Datei nachvollziehbar machen.
- **Stakeholder:** Entwickler, QA, Product Owner
- **Abgrenzung:** Fokus auf Diff-Ermittlung und Diff-Anzeige im Projektverzeichnis.

## 2. Funktionale Anforderungen
| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Dateispezifische Diff-Auflösung:** Bei Auswahl einer geänderten Datei wird genau der zugehörige Diff geladen, nicht ein globaler letzter Diff. → [Blueprint](../architecture/diffviewer-correct-diff-display-architecture-blueprint.md) · [ERM](../architecture/diffviewer-correct-diff-display-entity-relationship-model.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2** | **Korrekte Fehlermeldungslogik:** „Für diese Datei ist kein DiffResult vorhanden.“ erscheint nur, wenn für die ausgewählte Datei tatsächlich kein Diff existiert. → [Architecture Review](../improvements/diffviewer-correct-diff-display-architecture-review.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **FR-3** | **Sichtbare Added-Line:** Eine real hinzugefügte Zeile (+1) wird im DiffViewer sichtbar dargestellt. → [Blueprint](../architecture/diffviewer-correct-diff-display-architecture-blueprint.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-4** | **Ursachenanalyse:** Fehlerursache im Diff-Ermittlungs-/Anzeigeprozess wird reproduzierbar identifiziert und dokumentiert. → [Architecture Review](../improvements/diffviewer-correct-diff-display-architecture-review.md) | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **FR-5** | **Deterministischer Dateiwechsel:** Beim schnellen Wechsel zwischen Dateien wird immer der Diff der zuletzt gewählten Datei angezeigt. | Zuverlässigkeit | HIGH | 📋 Geplant |

## 3. Nicht-funktionale Anforderungen
| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Performance:** Diff wird nach Dateiauswahl in <= 2 Sekunden angezeigt (lokaler Standardfall). | Performance | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Zuverlässigkeit:** Falsch-positive Kein-Diff-Meldungen in den definierten Abnahmetests: 0 %. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Testbarkeit:** Automatisierte Tests decken +1-Zeile-Fall und echten Kein-Diff-Fall ab. | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-4** | **Wartbarkeit:** Diff-Zuordnungslogik ist zentral gekapselt und nicht über UI-Komponenten verteilt. | Wartbarkeit | HIGH | 📋 Geplant |

## 4. Akzeptanzkriterien
- **US-1:** Als Entwickler möchte ich bei Auswahl einer geänderten Datei den tatsächlichen Diff sehen.
  - AC-1.1: Kein falsches „kein DiffResult“ bei vorhandener Änderung.
  - AC-1.2: Eine +1-Zeile ist im DiffViewer sichtbar.
  - AC-1.3: Header/Statistik und Zeileninhalt sind konsistent.
- **US-2:** Als QA möchte ich die Fehlerursache reproduzierbar nachvollziehen.
  - AC-2.1: Reproduktionsschritte für bisherigen Fehler sind dokumentiert.
  - AC-2.2: Ursachenstufe (Zuordnung/Lookup/Rendering) ist benannt.
  - AC-2.3: Regressionstest verhindert Wiederauftreten.

## 5. Annahmen und Abhängigkeiten
| Typ | Beschreibung |
|-----|--------------|
| Annahme | Geänderte Datei wird korrekt erkannt und gespeichert/geladen. |
| Annahme | Diff-Berechnung hat valide Vergleichsgrundlage. |
| Abhängigkeit | DiffResult enthält Dateipfad und Zeileninformationen. |
| Abhängigkeit | Dateiauswahl liefert stabilen relativen Pfad. |

## 6. Scope und Out-of-Scope
- **In-Scope ✅:** Diff-Ermittlung je ausgewählter Datei, DiffViewer-Rendering, Fehlermeldungslogik, Abnahmetests.
- **Out-of-Scope ❌:** Vollständiger Austausch des Diff-Algorithmus, generelles UI-Redesign.

## 7. Domänenmodell und Glossar
- **DiffResult:** Persistiertes Ergebnis einer Diff-Berechnung für eine Datei.
- **FilePath:** Relativer Pfad zur Zuordnung Auswahl -> DiffResult.
- **DiffBlock/DiffLine:** Strukturierte Darstellung der Änderungen.
- **Kein-Diff-Fall:** Es existiert für die ausgewählte Datei kein passender DiffResult.

## 8. Nutzungsfälle (Use Cases)
1. **UC-1:** Nutzer wählt geänderte Datei -> System lädt dateispezifischen Diff -> Viewer zeigt Änderungen.
2. **UC-2:** Nutzer wählt Datei ohne Diff -> System zeigt korrekte Kein-Diff-Info.
3. **UC-3:** Nutzer wechselt schnell zwischen Dateien -> final sichtbar ist der Diff der zuletzt gewählten Datei.

## 9. Nächste Schritte
1. Architektur-Blueprint finalisieren.
2. ERM für Diff-Zuordnung finalisieren.
3. Architektur-Review mit priorisierten Maßnahmen durchführen.
4. Umsetzung und Regressionstests planen.

## 10. Approval & Versionierung
| Version | Datum | Änderung | Status |
|---------|-------|----------|--------|
| 1.0.0 | 2026-05-24 | Initiale Anforderungsanalyse erstellt | 📋 Entwurf |
