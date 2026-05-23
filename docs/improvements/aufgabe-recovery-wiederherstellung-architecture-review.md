# Architektur-Review – Manuelle Aufgaben-Recovery

> **Dokument-Typ:** Architecture Review  
> **Status:** ✅ Reviewed (umgesetzt)  
> **Datum:** 2026-05-23

---

## 1. Executive Summary

Die implementierte Recovery-Lösung adressiert den Kernfehler „Aufgabe hängt in Laufstatus“, ohne Automatismen mit Nebenwirkungen einzuführen.  
Die Architektur ist robust, weil sie **Laufzeit-Guard + Concurrency-Guard + Audit** kombiniert.

**Gesamtbewertung:** ✅ Freigabe

---

## 2. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Systemarchitektur | Sehr gut | Klare Trennung UI-Trigger und Recovery-Domänenlogik im Service |
| Datenkonsistenz | Sehr gut | `RecoveryVersion` als Concurrency-Token, conditionales Update |
| UI/UX | Gut | Sichtbarkeit/Disable-Gründe klar, explizite Bestätigung vorhanden |
| Nachvollziehbarkeit | Sehr gut | Strukturierte Logs plus Protokoll-Audit mit CorrelationId |

---

## 3. Findings und Auflösungsstatus

| ID | Priorität | Finding | Status |
|---|---|---|---|
| F-01 | Major | Recovery darf nicht bei aktivem Lauf erfolgen | ✅ Gelöst (`IsRunning`-Guard in UI + Service) |
| F-02 | Major | Parallel ausgelöste Recovery kann Doppelstatus erzeugen | ✅ Gelöst (`RecoveryVersion` + conditional update) |
| F-03 | Medium | Recovery-Aktionen müssen revisionssicher nachvollziehbar sein | ✅ Gelöst (Audit-Eintrag + `TaskRecovery*` Logs) |
| F-04 | Medium | Benutzerführung bei blockierter Recovery unklar | ✅ Gelöst (Disable-Reason in UI) |

---

## 4. Restrisiken

| Risiko | Auswirkung | Bewertung | Hinweis |
|---|---|---|---|
| Laufstatusquelle temporär nicht verfügbar | Recovery blockiert | Niedrig | Beabsichtigt defensives Verhalten |
| Prozess läuft außerhalb der bekannten Sessionverwaltung | Mögliche Fehleinschätzung „nicht running“ | Mittel | Operative Prüfung über Runtime/Host-Prozesse empfohlen |

---

## 5. Empfehlungen für Betrieb & Support

1. Bei Recovery-Meldungen zuerst `TaskRecoveryRejected`-ReasonCode prüfen (`StillRunning`, `RunningStatusUnavailable`, `InvalidState`).
2. Bei Konfliktmeldung (`Status wurde bereits geändert...`) Seite neu laden und tatsächlichen Status verifizieren.
3. Für Incident-Analyse `CorrelationId` aus Log und Protokolleintrag zusammenführen.

---

## 6. Verlinkung

- Requirements: [../requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md](../requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md](../architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md)
- Technischer Contract: [../api/aufgabe-recovery.md](../api/aufgabe-recovery.md)
- Flow: [../flows/aufgabe-recovery-flow.md](../flows/aufgabe-recovery-flow.md)

