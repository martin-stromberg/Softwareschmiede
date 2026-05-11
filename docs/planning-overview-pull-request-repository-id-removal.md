# Planungsübersicht – Pull-Request-Repository-ID entfernen

> **Dokument-Typ:** Planungsübersicht  
> **Projekt:** Softwareschmiede  
> **Status:** ✅ Planungsphase abgeschlossen

---

## 1. Ziel

Die Pull-Request-Erstellung wurde so geplant, dass die Repository-ID nicht mehr manuell eingegeben werden muss. Die bestehende Projekt- bzw. Aufgabenverknüpfung liefert die Information automatisch.

---

## 2. Erstellte Planungsdokumente

| Dokument | Beschreibung | Link |
|---|---|---|
| Anforderungsanalyse | Fachliche Anforderungen, Scope, Akzeptanzkriterien | [requirements/pull-request-repository-id-removal-requirements-analysis.md](requirements/pull-request-repository-id-removal-requirements-analysis.md) |
| Architektur-Blueprint | UI- und Service-Design für den vereinfachten PR-Flow | [architecture/pull-request-repository-id-removal-architecture-blueprint.md](architecture/pull-request-repository-id-removal-architecture-blueprint.md) |
| ERM | Konzeptionelles Modell ohne Persistenzänderung | [architecture/pull-request-repository-id-removal-entity-relationship-model.md](architecture/pull-request-repository-id-removal-entity-relationship-model.md) |
| Architektur-Review | Risiken, Maßnahmen und Freigabeempfehlung | [improvements/pull-request-repository-id-removal-architecture-review.md](improvements/pull-request-repository-id-removal-architecture-review.md) |

---

## 3. Wichtigste Entscheidungen

- Keine manuelle Repository-ID-Eingabe mehr.
- Repository wird serverseitig aus dem Projekt-/Aufgabenkontext abgeleitet.
- Kein neues Datenmodell und keine Migration erforderlich.
- Fehlende Repository-Zuordnung führt zu einem kontrollierten Abbruch.

---

## 4. Offene Punkte

- Auswahlregel bei mehreren Repositories pro Projekt ist festgelegt: Aufgabegebundenes Repository hat Vorrang; andernfalls wird genau ein aktives Projekt-Repository verwendet; bei Mehrdeutigkeit erfolgt ein kontrollierter Abbruch.

