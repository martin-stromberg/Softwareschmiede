# Architektur-Review – DiffViewer-Integration

**Primärquelle:** `780b1414-80fc-4596-8b6c-37edde0475fa.copilot-task.md`  
**Geprüfte Dokumente:**
- `docs/requirements/diffviewer-integration-requirements-analysis.md`
- `docs/architecture/diffviewer-integration-blueprint.md`
- `docs/architecture/diffviewer-integration-entity-relationship-model.md`

**Bewertung:** Go mit Nachbesserung – 2 Blocker, 3 Major, 3 Minor

---

## 1. Alignment mit Anforderungen

### Positiv
- Grundidee passt gut zu FR-1, FR-2, FR-5 und FR-8: DiffViewer als wiederverwendbare Komponente, Wrapper-Route, keine doppelte Rendering-Logik.
- NFR-1, NFR-5, NFR-6 und NFR-7 sind konzeptionell adressiert.
- ERM bleibt konsistent zur bestehenden Persistenz.

### Abweichungen / Lücken
- **FR-4 nicht vollständig abgebildet:** Der Blueprint delegiert Sonderfälle (binär, zu groß, gelöscht, Fehler) an `AufgabeDetail`, rendert die Komponente nur bei gültigem `DiffResultId`. Die Anforderung verlangt hingegen, dass der DiffViewer selbst diese Fälle verständlich darstellt.
- **`@rendermode InteractiveServer`** ist im Blueprint nur für den Wrapper erwähnt – muss als harte Umsetzungsregel für alle betroffenen Pages festgeschrieben werden.

---

## 2. Technische Korrektheit des Blueprints

### Positiv
- Route-Wrapper statt Route in der Komponente: sauber.
- Klare Trennung Routing ↔ Darstellung.
- Keine neue Diff-Berechnung im UI.
- Explizite Includes im DiffService passen zu den Richtlinien (keine Lazy Loading Proxies).

### Kritische Punkte

**1. Parameter-Lifecycle-Problem (Blocker)**  
Der aktuelle DiffViewer lädt in `OnInitializedAsync()`. Als eingebettete Komponente kann sich `DiffResultId` ändern, ohne dass ein Reload passiert.

Empfehlung: `OnParametersSetAsync()` oder Reload bei Parameterwechsel; alternativ `@key="DiffResultId"` am Komponenteneinsatz.

**2. Sonderfälle nicht komponentenseitig gekapselt (Blocker)**  
Der Blueprint verschiebt Hint-/Fallback-Logik in die Host-Seite. Das widerspricht dem Ziel einer wiederverwendbaren Diff-Komponente mit konsistentem Verhalten.

Empfehlung: Ein gemeinsames Preview-Modell oder Adapter, der Sonderfälle in derselben Komponente abbildet.

---

## 3. Blocker / Risiken

### Blocker
| # | Problem | Beschreibung |
|---|---------|--------------|
| B-1 | Reload bei Parameterwechsel fehlt | Ohne Fix bleibt die eingebettete Ansicht bei Dateiwahlwechseln veraltet |
| B-2 | FR-4 nicht vollständig erfüllt | Wenn Fallbacks außerhalb des DiffViewers liegen, ist die Komponente keine zentrale Anzeigeeinheit |

### Major
| # | Problem | Beschreibung |
|---|---------|--------------|
| M-1 | Unklare Zustandsverantwortung | Grenze zwischen AufgabeDetail und DiffViewer für Preview-Fälle ist nicht eindeutig |
| M-2 | Race Conditions nicht adressiert | Schnelle Dateiwechsel / parallel laufende Ladeoperationen nicht beschrieben |
| M-3 | Route-Wrapper-Fehlerverhalten | Nur grob beschrieben; visuell und semantisch eindeutig definieren |

### Minor
| # | Problem | Beschreibung |
|---|---------|--------------|
| m-1 | `PresentationMode` evtl. zu viel API | Wenn der Wrapper ohnehin eindeutig ist, könnte ein bool-Parameter genügen |
| m-2 | `DiffCache` im ERM überrepräsentiert | Für diese UI-Refaktorierung nicht notwendig zu betonen |
| m-3 | Fehlende Hinweise zu async Guards | `CancellationToken` / Lade-Generation bei Reload-Szenarien fehlen |

---

## 4. Verbesserungsvorschläge

### Blocker-Maßnahmen
- DiffViewer auf parametergetriebenes Laden umbauen: `OnParametersSetAsync()`, Reload nur bei geändertem `DiffResultId`, optional `CancellationToken`.
- Sonderfälle in konsistentes Preview-Konzept integrieren: `NoSelection`, `Hint`, `Deleted`, `NoDiff`, `DiffLoaded`.

### Major-Maßnahmen
- `AufgabeDetail` nur als Host/Container verwenden, nicht als Ort der Fachlogik für Preview-Fälle.
- Ein kleines ViewModel oder Adapter-Objekt für die Preview-Entscheidung definieren.
- Sicherstellen, dass Wrapper und `AufgabeDetail` jeweils explizit `@rendermode InteractiveServer` setzen.
- Fehler lokal im Previewbereich behandeln, keine Seitenabbrüche.

### Minor-Maßnahmen
- Optional `PresentationMode` behalten, aber klar dokumentieren, welche UI-Elemente davon abhängen.
- Semantik für Standalone-Ansicht ergänzen (`PageTitle`, eindeutige Überschrift).
- Tests um Parameterwechsel, schnelle Auswahlwechsel und Fallback-Zustände erweitern.

---

## 5. Konsistenz ERM ↔ Blueprint

| Aspekt | Bewertung |
|--------|-----------|
| Keine neuen DB-Entitäten | ✅ Konsistent |
| `DiffResult`, `DiffBlock`, `DiffLine` passen zum bestehenden Kontext | ✅ Konsistent |
| UI-only-Refaktorierung korrekt außerhalb des ERM | ✅ Konsistent |
| `WorkspaceFileNode` / `FilePreview` als Nicht-DB-Typen markiert | ✅ Konsistent |
| Zustands-/Fallback-Verantwortung | ⚠️ Unschärfe zwischen Blueprint und ERM |

---

## 6. Gesamturteil

**Go mit Nachbesserung**

Die Architektur ist grundsätzlich solide. Vor Umsetzung sind die beiden Blocker zu schließen:

1. **Reload bei Parameterwechsel** in der eingebetteten Komponente sicherstellen.
2. **Konsistentes Preview-/Fallback-Modell** für alle Zustände in der Komponente definieren.

Die weiteren Major-Findings sind empfohlen, blockieren die Implementierung aber nicht zwingend.
