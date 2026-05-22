# Anforderungsanalyse – Behebung von Kompilierfehlern

> **Dokument-Typ:** Requirements Analysis  
> **Status:** Zur Umsetzung freigegeben  
> **Version:** 1.0.0  
> **Datum:** 2026-05-22

---

## 1. Ausgangslage und Quelle

- Hauptanforderung: `ae13a240-9557-470e-994b-8c550d843312.copilot-task.md`  
  Inhalt: **„Es gibt Kompilierfehler.“**
- Reproduzierter Ist-Zustand: `dotnet build Softwareschmiede.slnx` schlägt fehl (12 Fehler, `CS0246`).
- Fehlerfokus: Diff-Komponenten unter `src/Softwareschmiede/Components/Diff/`.

---

## 2. Beobachtete Fehlersignatur

Nicht auflösbare Typen:

- `DiffViewMode`
- `NavigationDirection`
- `ExportFormat`

Betroffene Dateien:

- `DiffToolbar.razor`
- `DiffContent.razor`
- `DiffViewer.razor`

---

## 3. Zielbild

Die Solution muss ohne Kompilierfehler erfolgreich bauen.  
Die Diff-UI-Typen sind konsistent, zentral definiert und für alle relevanten Komponenten eindeutig referenzierbar.

---

## 4. Scope / Nicht-Scope

### In Scope
1. Analyse und Behebung der genannten Compile-Fehler.
2. Vereinheitlichung der Typdefinitionen für Diff-View, Navigation und Export.
3. Build- und Test-Absicherung für den betroffenen Bereich.

### Nicht in Scope
1. Funktionale Erweiterung des Diff-Features.
2. Redesign des UI-Layouts.
3. Persistenz-/Datenbankänderungen.

---

## 5. Annahmen (aufgrund knapper Anforderung)

1. Die aktuelle Fachlogik der Diff-Ansichten ist grundsätzlich korrekt; primär betroffen ist die Typ-/Strukturablage.
2. Die drei Typen sind als **gemeinsamer UI-Vertrag** gedacht, nicht als lokale Dateidetails.
3. Ein erfolgreicher Build aller betroffenen Projekte ist Mindestdefinition von „behoben“.

---

## 6. Fachliche und technische Anforderungen

### FR-1 Fehlerfreiheit Build
- Das Projekt `Softwareschmiede` baut ohne `CS0246`-Fehler im Diff-Bereich.

### FR-2 Konsistenter Typvertrag
- `DiffViewMode`, `NavigationDirection`, `ExportFormat` werden an einem stabilen, gemeinsam nutzbaren Ort definiert.

### FR-3 Komponenten-Konsistenz
- `DiffViewer`, `DiffToolbar`, `DiffContent` verwenden dieselben Typen mit eindeutigem Namespace.

### NFR-1 Wartbarkeit
- Keine redundanten oder versteckten Typdefinitionen in einzelnen Razor-Dateien.

### NFR-2 Nachvollziehbarkeit
- Ursache, Lösung und Validierung sind dokumentiert.

---

## 7. Akzeptanzkriterien

1. `dotnet build Softwareschmiede.slnx` endet mit Exit Code 0.
2. Keine `CS0246`-Fehler für die drei genannten Typen.
3. Diff-Komponenten kompilieren ohne lokale ad-hoc-Typduplikate.
4. Relevante Tests (mindestens Build + bestehende betroffene Testprojekte) laufen erfolgreich.
5. Dokumentation unter `docs/` ist mit Architektur/ERM/Review verlinkt.

---

## 8. Domänenmodell (fachlich)

- **CompileErrorIncident**: beschreibt den Build-Fehlerzustand eines Moduls.
- **UiContractType**: beschreibt gemeinsam genutzte Typdefinitionen im UI-Vertrag.
- **RemediationAction**: konkrete technische Korrekturmaßnahmen.
- **VerificationRun**: Nachweislauf (Build/Test) zur Erfolgsbestätigung.

Beziehungen:
- Ein `CompileErrorIncident` hat mehrere `RemediationAction`.
- `RemediationAction` referenziert `UiContractType`.
- Ein Incident gilt erst als gelöst mit mindestens einem erfolgreichen `VerificationRun`.

---

## 9. Risiken und Entscheidungsbegründungen

### Risiken
1. **Unvollständige Typkonsolidierung** → einzelne Komponenten bleiben auf alten Referenzen.
2. **Verdeckte Folgekopplungen** → weitere Dateien nutzen implizite Typen.
3. **Regression bei UI-Events** → Signaturen ändern sich unbeabsichtigt.

### Entscheidungen
1. Typen als zentraler Vertrag statt komponentenlokaler Definition.
2. Build-First-Validierung als primäres Done-Kriterium.
3. Keine Persistenzänderung, da Problem rein compile-/strukturbezogen ist.

---

## 10. Traceability

- Architektur-Blueprint: [../architecture/kompilierfehler-architecture-blueprint.md](../architecture/kompilierfehler-architecture-blueprint.md)
- ERM: [../architecture/kompilierfehler-entity-relationship-model.md](../architecture/kompilierfehler-entity-relationship-model.md)
- Architecture-Review: [../improvements/kompilierfehler-architecture-review.md](../improvements/kompilierfehler-architecture-review.md)
- Überblick: [../planning-overview-kompilierfehler.md](../planning-overview-kompilierfehler.md)
