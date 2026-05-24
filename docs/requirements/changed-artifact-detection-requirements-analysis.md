# Requirements Analysis – Robuste Ermittlung geänderter Code- und Planungsartefakte

## 1. Überblick und Projektkontext
- **Ziel:** Sicherstellen, dass bei Änderungsanalysen nicht nur Codedateien, sondern auch relevante Planungsdokumente zuverlässig gefunden und verarbeitet werden.
- **Kontext:** Bisherige Abläufe fokussierten implizit auf Quellcode-Dateien; Änderungen unter `docs/requirements/`, `docs/architecture/`, `docs/improvements/` wurden nicht zuverlässig berücksichtigt.

## 2. Funktionale Anforderungen
| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Duale Artefakt-Ermittlung:** Geänderte Dateien werden in `codeFiles` und `planningDocs` getrennt ermittelt. | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-2** | **Planungsdokument-Erkennung:** Mindestens `.md` in `docs/requirements/`, `docs/architecture/`, `docs/improvements/` werden als Planungsartefakte erkannt. | Dokumentation | MUST HAVE | ✅ Umgesetzt |
| **FR-3** | **Robuste Fallback-Prüfung:** Wenn initial keine Planungsdokumente erkannt werden, erfolgt eine explizite Nachprüfung der drei docs-Ordner. | Zuverlässigkeit | HIGH | ✅ Umgesetzt |
| **FR-4** | **Getrennte Ergebnisdarstellung:** Berichte weisen Code- und Planungsfunde getrennt aus. | Reporting & Analyse | HIGH | ✅ Umgesetzt |

## 3. Nicht-funktionale Anforderungen
| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Deterministische Erkennung:** Planungsdokumente werden pfadbasiert erkannt, unabhängig von der Code-Dateitypklassifikation. | Zuverlässigkeit | MUST HAVE | ✅ Umgesetzt |
| **NFR-2** | **Wartbarkeit:** Regeln sind zentral in den Agentenvorgaben dokumentiert und nachvollziehbar. | Wartbarkeit | HIGH | ✅ Umgesetzt |

## 4. Akzeptanzkriterien
- **AC-1:** Bei geänderten Dateien in `src/` und `docs/requirements/` werden beide in getrennten Kategorien erkannt.
- **AC-2:** Bei ausschließlich geänderten Planungsdokumenten ist `planningDocs` nicht leer.
- **AC-3:** Abschlussberichte enthalten getrennte Ausweisung von `codeFiles` und `planningDocs`.

## 5. Scope und Out-of-Scope
### In Scope
- Agentenvorgaben zur Artefakt-Ermittlung und -Auswertung.
- Planungsdokumente für Requirements, Architektur, ERM und Review.

### Out of Scope
- Änderung fachfremder Build-Pipelines.
- Einführung neuer Persistenzmodelle zur Dateiklassifikation.

## 6. Verlinkte Planungsdokumente
- Architektur: [changed-artifact-detection-architecture-blueprint](../architecture/changed-artifact-detection-architecture-blueprint.md)
- ERM: [changed-artifact-detection-entity-relationship-model](../architecture/changed-artifact-detection-entity-relationship-model.md)
- Review: [changed-artifact-detection-architecture-review](../improvements/changed-artifact-detection-architecture-review.md)
