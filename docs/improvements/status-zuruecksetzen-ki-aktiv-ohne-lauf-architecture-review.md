# Architektur-Review – Status zurücksetzen bei `KI Aktiv` ohne Lauf

> **Dokument-Typ:** Architecture Review  
> **Status:** Freigabe mit Hinweis  
> **Datum:** 2026-05-23

---

## 1. Executive Summary

Die geplante Korrektur ist architektonisch stimmig:  
Die UI darf den Button nicht pauschal wegen `KI Aktiv` sperren; entscheidend ist der Laufzeitstatus.  
Die Lösung bleibt innerhalb der bestehenden Schichten und benötigt **keine Datenmodelländerung**.

**Gesamtbewertung:** ✅ Freigabe

---

## 2. Bewertungsmatrix

| Bereich | Bewertung | Kurzbegründung |
|---|---|---|---|
| Systemarchitektur | Sehr gut | Korrektur bleibt in UI + Service-Logik, keine neue Domänenstruktur |
| Datenkonsistenz | Gut | Reset muss über vorhandene Status-Transition erfolgen |
| UI/UX | Gut | Bestätigung und Laufzeit-Hinweis sind fachlich nötig |
| Nachvollziehbarkeit | Gut | Bestehende Logik kann weiter protokolliert werden |

---

## 3. Findings und Auflösungsstatus

| ID | Priorität | Finding | Status |
|---|---|---|---|
| F-01 | Major | Der Button darf nicht allein am Status `KI Aktiv` hängen. | ✅ Gelöst durch Laufzeit-Guard statt Status-Only-Check |
| F-02 | Major | Ohne Bestätigung ist das Zurücksetzen zu riskant. | ✅ Gelöst durch Confirm-Dialog |
| F-03 | Medium | Die Aktion muss nach Erfolg konsistent zur neuen Anfrage führen. | ✅ Gelöst durch Reset auf `InBearbeitung` |
| F-04 | Medium | Datenmodelländerungen wären hier unnötig. | ✅ Keine ERM-/Schema-Änderung erforderlich |

---

## 4. Restrisiken

| Risiko | Auswirkung | Bewertung | Hinweis |
|---|---|---|---|
| Laufstatusquelle temporär nicht verfügbar | Aktion wird defensiv blockiert | Niedrig | Bevorzugt gegenüber falschem Freigeben |
| Unklare UI-Begriffe (`Reset`, `Status zurücksetzen`, Recovery) | Nutzerirritation | Mittel | Begriffe im UI konsolidieren |
| Parallele Statusänderung | Aktion scheitert oder muss neu geladen werden | Niedrig | Nach Erfolg immer Reload |

---

## 5. Empfehlungen

1. Button-Freigabe nicht auf `KI Aktiv`-Status allein stützen.
2. Laufzeitprüfung als Quelle der Wahrheit beibehalten.
3. Keine DB-/ERM-Änderung einführen.
4. UI-Terminologie vereinheitlichen.
5. Tests für:
   - `KI Aktiv` + kein Lauf → Button aktiv
   - `KI Aktiv` + Lauf aktiv → Button gesperrt
   - erfolgreicher Reset → neuer Anfragepfad möglich

---

## 6. Verlinkung

- Requirements: [../requirements/status-zuruecksetzen-ki-aktiv-ohne-lauf-requirements-analysis.md](../requirements/status-zuruecksetzen-ki-aktiv-ohne-lauf-requirements-analysis.md)
- Architektur-Blueprint: [../architecture/status-zuruecksetzen-ki-aktiv-ohne-lauf-architecture-blueprint.md](../architecture/status-zuruecksetzen-ki-aktiv-ohne-lauf-architecture-blueprint.md)

