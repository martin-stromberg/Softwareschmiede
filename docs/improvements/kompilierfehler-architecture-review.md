# Architecture-Review – Kompilierfehlerbehebung im Diff-Modul

> **Dokument-Typ:** Architecture Review  
> **Status:** Freigabe mit Auflagen  
> **Version:** 1.0.0  
> **Datum:** 2026-05-22

---

## 1. Referenzen

- Anforderungen: [../requirements/kompilierfehler-requirements-analysis.md](../requirements/kompilierfehler-requirements-analysis.md)
- Architektur: [../architecture/kompilierfehler-architecture-blueprint.md](../architecture/kompilierfehler-architecture-blueprint.md)
- ERM: [../architecture/kompilierfehler-entity-relationship-model.md](../architecture/kompilierfehler-entity-relationship-model.md)

---

## 2. Review-Fazit

Der Ansatz ist stimmig und adressiert die beobachtete Fehlerklasse (`CS0246`) zielgerichtet.  
Die Zentralisierung der Diff-Vertragstypen ist architektonisch sinnvoll.

**Freigabeentscheidung:** ✅ **Go mit Auflagen**

---

## 3. Priorisierte Findings

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| AR-01 | MAJOR | Ohne harte Regel kann erneut eine lokale Typdefinition in Razor entstehen. | Coding-Regel „UI-Vertragstypen nur in dedizierten `.cs`-Contracts“ verbindlich festlegen. |
| AR-02 | MAJOR | Build-Erfolg allein deckt nicht alle Referenzpfade ab (z. B. Tests/CI-Kontext). | Build + relevante Testläufe verpflichtend im DoD verankern. |
| AR-03 | MINOR | Namensraumkonventionen für Diff-Contracts sind noch nicht formalisiert. | Namespace- und Ablagekonvention dokumentieren und im Team teilen. |
| AR-04 | MINOR | Folgefehler ähnlicher Art in anderen UI-Modulen sind möglich. | Optionaler Repo-Scan nach lokal deklarierten, mehrfach verwendeten UI-Typen. |

---

## 4. Risiken und Trade-offs

### Risiken
1. Teilweise Migration: einzelne Dateien bleiben auf alten Typquellen.
2. Verdeckte Kopplungen in weiteren Komponenten werden erst später sichtbar.

### Trade-offs
| Entscheidung | Vorteil | Nachteil |
|---|---|---|
| Fokus auf Compile-Fix | schnelle Wiederherstellung der Lieferfähigkeit | mögliche Altlasten außerhalb des Scopes bleiben |
| Kein Feature-Umbau | geringe Änderungsbreite | funktionale Verbesserungen werden vertagt |

---

## 5. Auflagen vor Abschluss

1. Erfolgreicher vollständiger Solution-Build als harter Nachweis.
2. Relevante Testausführung dokumentieren.
3. Einheitliche Typablage inkl. Namespace-Konvention verbindlich machen.
4. Dokumentlinks zwischen Requirements, Blueprint, ERM und Review konsistent halten.

---

## 6. Empfehlung für nächste Iteration

- Präventiver Architektur-Check für Razor-Komponenten:
  - Wo werden wiederverwendete Typen deklariert?
  - Gibt es weitere lokale Enum-/DTO-Definitionen mit Querverwendung?

Damit kann die gleiche Fehlerklasse proaktiv reduziert werden.
