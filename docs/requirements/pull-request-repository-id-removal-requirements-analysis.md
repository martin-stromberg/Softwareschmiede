# Anforderungsanalyse – Pull-Request-Repository-ID entfernen

> **Dokument-Typ:** Requirements Analysis  
> **Status:** Geplant  
> **Version:** 1.0.0  
> **Thema:** Pull-Request-Erstellung ohne manuelle Repository-ID-Eingabe

---

## 1. Überblick und Kontext

Beim Erstellen eines Pull Requests wird aktuell eine Repository-ID als optionale Eingabe angeboten. Diese Angabe ist fachlich entbehrlich, weil die Repository-Zuordnung bereits über das Projekt bzw. die Aufgabe vorhanden ist. Ziel ist eine vereinfachte, konsistente PR-Erstellung ohne redundantes Eingabefeld.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **FR-1** | Die Pull-Request-Erstellung verwendet die Repository-ID automatisch aus der Aufgabe bzw. dem zugehörigen Projekt. → [Blueprint](../architecture/pull-request-repository-id-removal-architecture-blueprint.md) · [ERM](../architecture/pull-request-repository-id-removal-entity-relationship-model.md) | UI / Git-Integration | MUST HAVE | Geplant |
| **FR-2** | Das Eingabefeld für eine manuelle Repository-ID wird aus der PR-Maske entfernt. | UI | MUST HAVE | Geplant |
| **FR-3** | Wenn keine Repository-Zuordnung ermittelt werden kann, wird die Aktion kontrolliert abgebrochen. | Fehlerbehandlung | MUST HAVE | Geplant |
| **FR-4** | Titel und Beschreibung des Pull Requests bleiben weiterhin editierbar. | UX | SHOULD HAVE | Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **NFR-1** | Reduzierte Eingabeoptionen senken Fehlbedienung und Inkonsistenzen. | Usability | HIGH | Geplant |
| **NFR-2** | Die Repository-Ermittlung ist deterministisch und nachvollziehbar. | Zuverlässigkeit | MUST HAVE | Geplant |
| **NFR-3** | Es dürfen keine neuen Persistenzartefakte entstehen. | Datenmodell | MUST HAVE | Geplant |
| **NFR-4** | Bestehende PR-Funktionalität bleibt fachlich unverändert. | Stabilität | HIGH | Geplant |

---

## 4. Akzeptanzkriterien

- AC-1: Die PR-Maske enthält kein Repository-ID-Feld mehr.
- AC-2: Ein PR wird mit der Repository-ID aus der Aufgabe bzw. dem Projekt erstellt.
- AC-3: Bei fehlender Repository-Zuordnung erscheint eine verständliche Fehlermeldung.
- AC-4: Titel und Beschreibung können weiterhin manuell gesetzt werden.
- AC-5: Es entstehen keine Änderungen am Datenbankschema.

---

## 5. Scope und Out-of-Scope

**In-Scope**
- UI-Bereinigung auf der Aufgabendetailseite
- Automatische Repository-Ermittlung im PR-Flow
- Testabsicherung für den neuen Standardpfad

**Out-of-Scope**
- Änderung des GitHub-Plugin-Interfaces
- Neue Repository-Auswahl-Dialoge
- Datenbankmigrationen

---

## 6. Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|---|---|---|
| Annahme | Jede PR-Erstellung bezieht sich auf eine Aufgabe mit Projektkontext. | Die Repository-ID kann aus bestehenden Daten abgeleitet werden. |
| Abhängigkeit | `GitOrchestrationService.PullRequestErstellenAsync(...)` löst die Repository-ID bereits serverseitig. | Die UI muss keinen Override mehr liefern. |
| Abhängigkeit | `AufgabeDetail` enthält die PR-Maske. | Dort wird das Feld entfernt. |

---

## 7. Nutzungsfall

- **UC-1:** Anwender öffnet eine Aufgabe, füllt Titel und Beschreibung für den PR aus und startet die Erstellung. Die Repository-ID wird automatisch verwendet.

---

## 8. Nächste Schritte

1. Architektur des vereinfachten PR-Flows dokumentieren.
2. Persistenzmodell prüfen: keine Änderungen erforderlich.
3. Review der Risiken bei fehlender oder mehrdeutiger Repository-Zuordnung.

