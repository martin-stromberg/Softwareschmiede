# Architektur-Review: LocalDirectoryPlugin

> **Dokument-Typ:** Architektur-Review  
> **Projekt:** Softwareschmiede  
> **Status:** Aktualisiert  
> **Version:** 1.1.0  
> **Datum:** 2026-05-12

---

## Verwandte Dokumente

- [Requirements Analysis](../requirements/lokales-verzeichnis-plugin-requirements-analysis.md)
- [Architektur-Blueprint](../architecture/lokales-verzeichnis-plugin-architecture-blueprint.md)
- [ERM](../architecture/lokales-verzeichnis-plugin-entity-relationship-model.md)

---

## 1. Executive Summary

Die Planung ist insgesamt gut ausgerichtet (Requirements ↔ Blueprint ↔ ERM), insbesondere bei:
- `WorkspaceMode` (Enum + Select),
- Local-only Scope,
- explizitem `NotSupportedException` für Remote-Funktionen,
- Test-/Build-Gates.

**Gesamturteil:** ⚠️ **Freigabe mit Auflagen**  
Hauptaufwände liegen in Confirm-Flow-Architektur, Sicherheits-/Integritätsdetails bei lokalen Git/FS-Operationen und robustem Verhalten bei großen Verzeichnissen.

---

## 2. Konsistenzprüfung (Requirements / Architektur / ERM)

## ✅ Konsistent

1. `WorkspaceMode` als Enum (`InSourceDirectory`, `SeparateWorkingDirectory`) inkl. UI-Select und String-Serialisierung.
2. Nicht unterstützte Remote-Operationen als expliziter Fehlerpfad (`NotSupportedException`).
3. Guardrails für große Kopien (Timeout/Limits) als Muss-Anforderung.
4. Release-Gates (`dotnet build`, `dotnet test`) als verpflichtende Qualitätsbarriere.

## ⚠️ Inkonsistenzen / Unklarheiten

1. **Confirm-Flow Schichtengrenze unklar**  
   Blueprint-Sequenz zeigt Rückfrage „Plugin -> UI“. Dafür fehlt ein sauberer Contract (z. B. Confirm-Callback/Policy im Application-Layer).

2. **ERM ist teils konzeptuell, teils physisch formuliert**  
   Gleichzeitig „keine DB-Migration / Credential Store“ und relationale Entitäten inkl. Audit-Tabellen. Physisches Zielmodell muss klar getrennt werden.

3. **`CheckoutRemoteBranchAsync` in Basisklasse genannt, lokal aber NotSupported**  
   Risiko widersprüchlicher Vererbung/Default-Implementierungen.

---

## 3. Schwachstellen & Risiken (priorisiert)

## 🔴 Blocker

1. **Fehlender technischer Confirm-Contract (`git init` in Source)**
   - Risiko: unsaubere Kopplung zwischen Plugin- und UI-Schicht.
   - Folge: schwer testbar, potenziell inkonsistente UX.

2. **Sicherheits-/Integritätslücken bei lokalen Git/FS-Operationen**
   - Fehlende harte Vorgaben zu Canonical Path Check, Symlink/Reparse-Point-Handling, erlaubten Root-Pfaden, Cleanup bei Teilkopien.
   - Risiko: unbeabsichtigte Änderungen außerhalb des Zielbereichs.

## 🟠 Major

3. **UX für NotSupported nur über Exceptions beschrieben**
   - Risiko: Nutzer sieht späte Fehler statt früh deaktivierter Aktionen.
   - Bedarf: Capability-getriebene UI-Gates.

4. **Robustheit bei großen Verzeichnissen nicht vollständig operationalisiert**
   - Limits vorhanden, aber keine klare Strategie für:
     - atomaren Abbruch,
     - partielles Rollback/Cleanup,
     - reproduzierbare Timeout-/Cancellation-Semantik.

5. **Testbarkeit/Gates noch nicht vollständig messbar**
   - Build/Test-Gates vorhanden, aber keine expliziten Qualitätsmetriken (z. B. verpflichtende Testfallmatrix je Risiko).

## 🟡 Minor

6. **Fehlerkatalog zu grob**
   - Weitere Differenzierung nötig (Pfad nicht lesbar/schreibbar, Lock, Symlink-Verstoß, Guardrail-Verstoß).

7. **Logging-/Telemetry-Leitplanken**
   - Sanitization erwähnt, aber keine konkreten Regeln für Pfad-Maskierung/Korrelation.

---

## 4. Fokusprüfung

### 4.1 Sicherheit/Integrität lokale Git-Operationen
Empfehlung:
- Canonicalization + `Path.GetFullPath`-Vergleich vor jeder Operation.
- Blocklist für sensible Ziele (Root/Systempfade).
- Explizite Symlink/Reparse-Policy (default: blockieren).
- `git init` nur mit bestätigtem Intent + idempotentem Guard.
- Cleanup-Strategie bei abgebrochenen Kopien.

### 4.2 UX Confirm-Flow & nicht unterstützte Remote-Funktionen
Empfehlung:
- Confirm als Application-Service-Policy (nicht direkt im Plugin/UI-Hopping).
- UI zeigt Capability-basiert nur verfügbare Aktionen.
- Für NotSupported: verständliche, einheitliche Nutzertexte + „Was stattdessen möglich ist“.

### 4.3 Testbarkeit/Validierbarkeit inkl. Gates
Empfehlung:
- Verbindliche Testfallmatrix:
  - Confirm angenommen/abgelehnt,
  - dirty tree,
  - NotSupported je Methode,
  - Invalid Enum + Fallback,
  - Timeout/Cancellation,
  - Copy-Limit-Verstoß.
- Gates in CI als Pflicht (Build + Unit + Integration), kein Merge bei Rot.

### 4.4 Robustheit bei großen Verzeichnissen/Timeouts
Empfehlung:
- Streaming-/chunk-basierte Kopierstrategie.
- Frühzeitige Guardrail-Checks vor Vollkopie.
- Deterministisches Abbruch-/Cleanup-Verhalten.
- Reproduzierbare Performance-/Grenzwerttests.

---

## 5. Konkrete Verbesserungsmaßnahmen

| Prio | Maßnahme | Ergebnis/Nachweis |
|---|---|---|
| Blocker | Confirm-Contract als technischen Interface-/Policy-Punkt definieren | Architekturdokument + Tests für Confirm-Flow |
| Blocker | Security Hardening für Pfad-/Symlink-/Root-Schutz und Copy-Cleanup | Negativtests + Security-Checkliste |
| Major | Capability-Modell in Application/UI nutzen (präventives Disable statt später Exception) | UI-/Service-Tests für Feature-Gating |
| Major | Guardrail-Implementierung mit klarer Timeout/Cancellation-Semantik | Last-/Grenzwerttests grün |
| Major | Testfallmatrix als verbindlicher Release-Artefakt | CI-Report mit vollständiger Abdeckung der Risikofälle |
| Minor | Fehlerkatalog + standardisierte Fehlermeldungen verfeinern | Konsistente Fehlercodes/-texte |
| Minor | Logging-Sanitization-Regeln konkretisieren | Reviewbare Logging-Guideline |

---

## 6. Abschlussbewertung

Die Planung ist inhaltlich tragfähig und weitgehend konsistent.  
Für eine belastbare Umsetzung müssen jedoch die **Blocker** (Confirm-Contract, Sicherheits-/Integritätsregeln) vor Implementierungsabschluss verbindlich geklärt und testbar gemacht werden.

