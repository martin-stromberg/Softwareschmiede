# Planungsübersicht – DiffViewer-Integration in AufgabeDetail

## Primäre Anforderungsquelle
- `780b1414-80fc-4596-8b6c-37edde0475fa.copilot-task.md`

## Erzeugte Planungsdokumente

- **Anforderungen:** [requirements/diffviewer-integration-requirements-analysis.md](requirements/diffviewer-integration-requirements-analysis.md)
- **Architektur:** [architecture/diffviewer-integration-blueprint.md](architecture/diffviewer-integration-blueprint.md)
- **ERM:** [architecture/diffviewer-integration-entity-relationship-model.md](architecture/diffviewer-integration-entity-relationship-model.md)
- **Review:** [improvements/diffviewer-integration-architecture-review.md](improvements/diffviewer-integration-architecture-review.md)

## Kurzfazit

- Der bestehende `DiffViewer` wird von einer gerouteten Blazor-Page zu einer **wiederverwendbaren Komponente** refaktoriert.
- In `AufgabeDetail` ersetzt er die bisherige Dateivorschau mit zwei `<pre>`-Blöcken.
- Die Route `/diff/{DiffResultId:guid}` bleibt über einen dünnen **Wrapper** erhalten.
- Das Datenbankschema **bleibt unverändert** – es ist eine reine UI-Refaktorierung.
- Der Review identifiziert **2 Blocker**: parametergetriebenes Laden (`OnParametersSetAsync`) und ein konsistentes Fallback-Modell für Sonderfälle.

## Umsetzungstransfer (Stand: abgeschlossen)

- ✅ Parameterwechsel ist im `DiffViewer` auf `OnParametersSetAsync` umgestellt und durch Cancellation-/Version-Guard gegen stale Anzeige abgesichert.
- ✅ Sonder- und Fehlerfälle sind in `Components/Diff/DiffPreviewPanel.razor` zentral gekapselt.
- ✅ Zustandsgrenze ist festgezogen:
  - `AufgabeDetail`: Dateiauswahl und Preview-Datenbeschaffung
  - `DiffPreviewPanel`: Fallback-/Hint-Entscheidung
  - `DiffViewer`: Diff-Laden und Diff-Rendering
- ✅ Route `/diff/{DiffResultId:guid}` bleibt über `Components/Pages/Diff/DiffViewerPage.razor` kompatibel.
- ✅ Blazor-Vorgabe eingehalten: betroffene Seiten mit `@rendermode InteractiveServer` gesetzt.
