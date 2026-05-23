# Anforderungsanalyse – Manuelle Wiederherstellung festhängender Aufgaben

> **Dokument-Typ:** Requirements Analysis  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0  
> **Thema:** Defekte/festhängende Aufgaben in `KiAktiv` oder `TestsLaufen` manuell sicher auf `InBearbeitung` zurückführen.

---

## 1. Überblick und Projektkontext

Bei einzelnen Fehlerfällen (z. B. unterbrochene Streaming-Verbindung, abgebrochener Prozess, inkonsistente UI-Session) kann eine Aufgabe in `KiAktiv` oder `TestsLaufen` verbleiben, obwohl keine Verarbeitung mehr läuft.  
Die Funktion ermöglicht eine **bewusste, manuelle Recovery direkt in AufgabeDetail**, ohne automatische Statusmutation im Hintergrund.

**Stakeholder:** Anwender, Support, Entwicklung

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **FR-1** | **Recovery-Aktion in AufgabeDetail:** Die Aktion **„Aufgabe wiederherstellen“** ist nur in Recovery-Status sichtbar (`KiAktiv`, `TestsLaufen`). | UI | MUST HAVE | ✅ Umgesetzt |
| **FR-2** | **Manuelle Bestätigung:** Vor Ausführung ist eine explizite Nutzerbestätigung erforderlich. | Sicherheit/Bedienung | MUST HAVE | ✅ Umgesetzt |
| **FR-3** | **Laufzeit-Guard:** Recovery ist nur erlaubt, wenn für die Aufgabe keine aktive Verarbeitung läuft (`IRunningAutomationStatusSource.IsRunning == false`). | Fachlogik | MUST HAVE | ✅ Umgesetzt |
| **FR-4** | **Statuswechsel:** Erfolgreiche Recovery setzt den Status auf `InBearbeitung`. | Statusmodell | MUST HAVE | ✅ Umgesetzt |
| **FR-5** | **Audit-Protokoll:** Jeder erfolgreiche Recovery-Fall erzeugt einen `Protokolleintrag` (`StatusUebergang`) mit ReasonCode und CorrelationId. | Nachvollziehbarkeit | MUST HAVE | ✅ Umgesetzt |
| **FR-6** | **Concurrency-Schutz:** Bei konkurrierenden Änderungen darf nur ein Recovery-Vorgang erfolgreich sein; Konflikte liefern eine verständliche Fehlermeldung. | Datenkonsistenz | MUST HAVE | ✅ Umgesetzt |
| **FR-7** | **Fehlermeldungen:** Nicht zulässige Zustände, laufende Verarbeitung, fehlende Aufgabe und nicht prüfbarer Laufstatus werden klar kommuniziert. | UX/Support | MUST HAVE | ✅ Umgesetzt |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **NFR-1** | **Idempotenz unter Parallelität:** Bei zwei gleichzeitigen Recovery-Versuchen gewinnt genau ein Versuch. | Zuverlässigkeit | MUST HAVE | ✅ Umgesetzt |
| **NFR-2** | **Transparenz:** Recovery-Ereignisse sind über strukturierte Logs und Audit-Eintrag korrelierbar. | Observability | MUST HAVE | ✅ Umgesetzt |
| **NFR-3** | **Defensives Verhalten:** Bei nicht verfügbarem Laufstatus wird Recovery blockiert statt „blind“ ausgeführt. | Sicherheit/Stabilität | MUST HAVE | ✅ Umgesetzt |
| **NFR-4** | **Testabdeckung:** Unit-, Integrations- und UI-Tests decken Happy Path und Fehlerpfade ab. | Qualität | MUST HAVE | ✅ Umgesetzt |

---

## 4. Akzeptanzkriterien

- **AC-1:** In `KiAktiv`/`TestsLaufen` erscheint in AufgabeDetail die Aktion „Aufgabe wiederherstellen“.  
- **AC-2:** Läuft noch eine Verarbeitung, bleibt die Aktion deaktiviert und zeigt den Grund.  
- **AC-3:** Nach erfolgreicher Recovery steht die Aufgabe auf `InBearbeitung`.  
- **AC-4:** Im Protokoll existiert genau ein `StatusUebergang` mit „Manuelle Wiederherstellung“, `ReasonCode: RecoveryManual` und `CorrelationId`.  
- **AC-5:** Bei konkurrierenden Änderungen wird ein Konflikt mit Benutzerhinweis ausgegeben, ohne falsche Doppelprotokollierung.

---

## 5. Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|---|---|---|
| Annahme | Laufstatus kann über `IRunningAutomationStatusSource` aufgelöst werden. | Recovery ist an echte Laufzeitinformation gebunden. |
| Abhängigkeit | `AufgabeDetail` lädt Status und steuert UI-Zustände. | Sichtbarkeit/Disable-Logik liegt im Page-ViewModel-Code-Behind. |
| Abhängigkeit | `SoftwareschmiededDbContext` verwaltet `RecoveryVersion` als Concurrency-Token. | Konflikte werden deterministisch erkannt. |

---

## 6. Scope und Out-of-Scope

**In-Scope ✅**
- Manuelle Recovery in AufgabeDetail
- Statuswechsel `KiAktiv|TestsLaufen -> InBearbeitung`
- Audit- und Logging-Transparenz
- Concurrency-Schutz über `RecoveryVersion`

**Out-of-Scope ❌**
- Automatische Recovery-Jobs im Hintergrund
- Recovery aus beliebigen anderen Status
- Wiederaufnahme/Korrektur tatsächlich noch laufender Prozesse

---

## 7. Referenzen

- Blueprint: [../architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md](../architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md)  
- Architecture Review: [../improvements/aufgabe-recovery-wiederherstellung-architecture-review.md](../improvements/aufgabe-recovery-wiederherstellung-architecture-review.md)  
- Technischer Contract: [../api/aufgabe-recovery.md](../api/aufgabe-recovery.md)  
- Flow: [../flows/aufgabe-recovery-flow.md](../flows/aufgabe-recovery-flow.md)

---

## 8. Approval & Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-23 | documentation-orchestrator | Erstfassung der umgesetzten Recovery-Anforderungen inkl. AC/NFR |

