# Architektur-Blueprint – AufgabeDetail UI Register-Navigation

> **Dokument-Typ:** Architektur-Blueprint  
> **Status:** 🔄 In Arbeit (Implementierung begonnen, nicht abgeschlossen)  
> **Version:** 1.2.0  
> **Primärquelle:** Requirements v1.2.0

---

## 1. Verlinkte Grundlagen (Single Source of Truth)

- Requirements (Primärquelle): [`../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md`](../requirements/aufgabe-detail-ui-register-navigation-requirements-analysis.md)
- ERM: [`./aufgabe-detail-ui-register-navigation-entity-relationship-model.md`](./aufgabe-detail-ui-register-navigation-entity-relationship-model.md)
- Architektur-Review: [`../improvements/aufgabe-detail-ui-register-navigation-architecture-review.md`](../improvements/aufgabe-detail-ui-register-navigation-architecture-review.md)
- Planungsübersicht: [`../planning-overview-aufgabe-detail-ui-register-navigation.md`](../planning-overview-aufgabe-detail-ui-register-navigation.md)
- Ausgangsaufgabe (begonnene Umsetzung): [`../../980a1311-2ba6-455b-95c3-ca4147fd36fd.copilot-task.md`](../../980a1311-2ba6-455b-95c3-ca4147fd36fd.copilot-task.md)

Abgedeckt werden FR-1 bis FR-6, NFR-1 bis NFR-6 sowie AC-1.1 bis AC-4.3 der Requirements.

---

## 2. Zielarchitektur und Systemgrenzen

`AufgabeDetail` ist eine Blazor-Seite mit **einem zentralen Registerzustand** und **drei exklusiven UI-Kontexten**:

1. **Aufgabe**: Anforderungsbeschreibung, Kennzahlen, Kernaktionen  
2. **Ausführung**: KI-Anfrage und KI-Protokoll  
3. **Projektverzeichnis**: Explorer, Git-Aktionen und Git-Dialoge

Architekturziel:

- genau ein Registerinhalt gleichzeitig sichtbar (FR-1.1, NFR-1),
- globale Infoboxen für Commits/Geänderte Dateien immer sichtbar (FR-5),
- Git-Dialoge nur im Register Projektverzeichnis sichtbar und bedienbar (FR-4.2),
- Legacy-„Ansicht“-Pfad vollständig entfernt (FR-6),
- keine Persistenz-/Schemaänderung (NFR-6).

Systemgrenze:

- **In Scope:** UI-Komposition, State-/Renderlogik, Dialog-Lifecycle, Tests.  
- **Out of Scope:** neue Git-Backend-Verträge, Datenbankmigrationen, Domain-Refactoring außerhalb `AufgabeDetail`.

---

## 3. Komponenten und Verantwortlichkeiten

| Schicht | Komponenten/Artefakte | Verantwortung |
|---|---|---|
| Presentation | `AufgabeDetail.razor` | Register-Navigation, globale Infoboxen, registerspezifische Inhalte, Dialog-Host |
| Presentation Logic | `AufgabeDetail.razor.cs` | `_activeRegister`, Query-Init (`register`/`view`), Dialog-Flags, Lifecycle-Regeln |
| Application Services | `AufgabeService`, `KiAusfuehrungsService`, `GitOrchestrationService`, `IGitWorkspaceBrowserService`, `ProtokollService`, `ProjektService` | Daten und Aktionen für Registerinhalte |
| Domain / Read Models | `Aufgabe`, `Protokolleintrag`, `WorkspaceSnapshot` | Quelle für Inhalte und Kennzahlen |
| Tests | `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetail*Tests.cs` | Nachweis Exklusivität, Dialogsichtbarkeit, Regressionen, Empty/Error-Pfade |

Prinzip: **UI-Refactoring mit Wiederverwendung bestehender Services**, keine Backend-Neuarchitektur.

---

## 4. Zustands- und Renderlogik

### 4.1 Registerzustand

- Der Zustand wird zentral über `_activeRegister` gehalten.
- Zulässige Werte: `Aufgabe`, `Ausführung`, `Projektverzeichnis`.
- Query-Parameter `register` bzw. `view` werden beim Initialisieren gelesen.
- Ungültige Werte fallen deterministisch auf `Aufgabe` zurück (AC-1.3).

### 4.2 Renderinvariante

- Registerinhalte werden über **exklusive Renderpfade** (`if / else if`) angezeigt.
- Invariante: **Exactly one register visible** (FR-1.1, NFR-1).
- Globale Infoboxen liegen außerhalb des Register-Content-Blocks und bleiben daher immer sichtbar (FR-5).

### 4.3 Aktionsmatrix je Register

- **Aufgabe:** Startskript, Abschließen, Abbrechen (FR-2.1)  
- **Ausführung:** Startskript, Abschließen, Abbrechen (FR-3.1)  
- **Projektverzeichnis:** Startskript, Commit, Push, Pull, Pull Request, Aktualisieren, Abschließen, Abbrechen (FR-4.1)

---

## 5. Dialog-Lifecycle (Git)

Betroffene Dialogflags:

- `_showCommitForm`
- `_showPushForm`
- `_showPullForm`
- `_showPullRequestForm`

Lifecycle-Regeln:

1. Dialog darf nur geöffnet werden, wenn `_activeRegister == Projektverzeichnis`.
2. Dialog-Rendering findet ausschließlich im Projektverzeichnis-Dialog-Host statt.
3. Beim Verlassen des Registers Projektverzeichnis wird `CloseProjectDirectoryDialogs()` ausgeführt und schließt alle Git-Dialoge deterministisch.
4. Re-Entry in Projektverzeichnis startet ohne „hängende“ Dialogreste.

Diese Regeln schließen die Review-Blocker F-B01/F-B02 fachlich-architektonisch.

---

## 6. UI/UX-Konzept

### 6.1 Informationsarchitektur

- 3 Register als Primärnavigation.
- Globale Kennzahlen als persistenter Seitenkopfbereich.
- Registerinterner Content strikt nach Kontext getrennt.

### 6.2 Interaktionsdesign

- Registerwechsel ist der primäre Kontextwechsel.
- Registerspezifische Aktionen liegen jeweils nahe am zugehörigen Inhalt.
- Git-Dialoge sind kontextgebunden und erscheinen nur dort, wo die Aktion fachlich gültig ist.

### 6.3 Accessibility-Basis

- Tab-Pattern: `tablist`, `tab`, `aria-selected`.
- Tastaturbedienung für Registerwechsel (NFR-5).
- Semantische Beschriftung und Fokusführung pro Registerwechsel.

---

## 7. Fehlerbehandlung und Fallbacks

| Situation | Reaktion |
|---|---|
| Ungültiger `register`/`view` Querywert | Fallback auf Register `Aufgabe` |
| Explorer-Daten fehlen | Empty-State mit erklärendem Text + Refresh-CTA |
| Explorer-/Git-Ladevorgang schlägt fehl | Error-State mit Fehlermeldung + erneuter Versuch |
| Aktion nicht erlaubt/verfügbar | Aktion deaktiviert, kein Dialog-Open |
| Registerwechsel während offenem Git-Dialog | Sofortiges deterministisches Schließen aller Git-Dialoge |

---

## 8. Qualitätsziele

| Qualitätsziel | Zielwert / Nachweis |
|---|---|
| Zuverlässigkeit | 100 % deterministische Exklusivität der Registersichtbarkeit (NFR-1) |
| Performance | Registerwechsel im Regelfall < 200 ms auf definierter Zielumgebung (NFR-2) |
| Regressionssicherheit | Kernaktionen bleiben funktional nach Refactoring (NFR-3) |
| Testbarkeit | bUnit-Nachweis für G1–G8 (NFR-4) |
| Accessibility | Tab-Semantik + Tastaturfluss dokumentiert und geprüft (NFR-5) |
| Stabilität | Keine DB-Schema-/Persistenzänderung (NFR-6) |

---

## 9. Testbarkeit und Teststrategie

Pflichtabdeckung in `AufgabeDetail*Tests`:

1. Exklusive Registersichtbarkeit inkl. Query-Init/Fallback (G1).
2. Git-Dialogsichtbarkeit und Interaktivität nur im Projektverzeichnis (G2).
3. Globale Infoboxen in allen Registern sichtbar (G3).
4. Legacy-„Ansicht“ vollständig entfernt (G4).
5. FR-2-Kennzahlen inkl. Empty-/Error-Pfade (G5).
6. Performancenachweis gemäß NFR-2 (G6).
7. A11y-Checks für ARIA + Tastatur (G7).
8. Regressionstests für Startskript/Abschließen/Abbrechen/Git (G8).

Testprinzip:

- deterministische Service-Stubs,
- je Register positive + negative Pfade,
- explizite Lifecycle-Tests bei Registerwechseln.

---

## 10. Rollout- und Migrationsstrategie

### 10.1 Technische Migration (ohne DB-Migration)

- Migration ist ein **UI-State-Refactoring in-place** in `AufgabeDetail.razor` und `AufgabeDetail.razor.cs`.
- Keine Tabellen-/Schema-/Contract-Änderung.
- Legacy-Ansicht und zugehörige State-Pfade werden entfernt.

### 10.2 Rollout-Schritte

1. Register- und Dialog-Lifecycle finalisieren.
2. Pflichttests G1–G8 auf grün bringen.
3. Performance-/A11y-Nachweise dokumentieren.
4. Feature in regulärem Release ausrollen (kein Datenmigrationsfenster erforderlich).

### 10.3 Rollback

- Bei Regression Rückkehr auf vorherigen Commit der UI-Dateien.
- Da keine Persistenzänderung vorliegt, ist Rollback ohne Datenwiederherstellung möglich.

---

## 11. Konsistenzabgleich mit Requirements v1.2.0

| Requirement-Cluster | Architekturabdeckung |
|---|---|
| FR-1 / FR-1.1 | Zentraler Registerzustand + exklusive Renderpfade |
| FR-2 / FR-2.1 | Aufgabe-Register mit Kennzahlen + Aktionen |
| FR-3 / FR-3.1 | Ausführung-Register mit KI-Anfrage/-Protokoll + Aktionen |
| FR-4 / FR-4.1 / FR-4.2 | Projektverzeichnis-Register mit Explorer, Git-Aktionen, Dialog-Lifecycle |
| FR-5 | Globale Infoboxen außerhalb Register-Body |
| FR-6 | Vollständige Entfernung der Legacy-„Ansicht“ |
| NFR-1..NFR-6 | Sichtbarkeitsinvariante, Performance-Ziel, Regression, Testbarkeit, A11y, keine Schemaänderung |

---

## 12. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | planning-architecture-blueprint | Erstfassung |
| 1.1.0 | 2026-05-25 | Copilot | Ausbau auf Requirements v1.1.0 (Schichten, Entscheidungen, Testarchitektur) |
| 1.2.0 | 2026-05-25 | Copilot | Konsolidierung auf Requirements v1.2.0; Zielarchitektur, Komponenten, Zustands-/Renderlogik, Dialog-Lifecycle, UI/UX, Fehlerbehandlung, Qualitätsziele, Testbarkeit sowie Rollout/Migration aktualisiert |
