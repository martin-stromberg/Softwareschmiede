# Diff Viewer (`/diff/{DiffResultId:guid}`)

Der Diff Viewer ist eine wiederverwendbare Blazor-Komponente mit Wrapper-Route. Das Feature unterstützt zwei Darstellungsmodi (Embedded/Standalone), stabile Parameterwechsel und einen klaren Verantwortungsschnitt zwischen Aufgaben-Detailseite, Vorschaupanel und Viewer.

## 1. Übersicht

Der Endpunkt `GET /diff/{DiffResultId:guid}` stellt eine eigenständige Diff-Seite über eine Wrapper-Page bereit. Dieselbe `DiffViewer`-Komponente wird zusätzlich eingebettet in `AufgabeDetail` über `DiffPreviewPanel` verwendet.

## 2. HTTP-Methode & Pfad

`GET /diff/{DiffResultId:guid}`

## 3. Authentifizierung

Keine.

## 4. Request

**Header**
- Keine speziellen Header erforderlich.

**Path-Parameter**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `DiffResultId` | `Guid` | Ja *(required)* | ID des anzuzeigenden Diff-Ergebnisses. |

**Query-Parameter**
- Keine.

**Request-Body**
- Keiner.

## 5. Dual-Mode-Verhalten (Embedded vs Standalone)

### Embedded
- Verwendung in `DiffPreviewPanel` innerhalb von `AufgabeDetail`.
- Aufruf mit `PresentationMode="DiffViewerPresentationMode.Embedded"`.
- ARIA-Rolle des Root-Containers: `region`.
- Kein „Back to Home“-Link bei Fehler-/Not-Found-Anzeige.

### Standalone
- Verwendung über Wrapper-Page `DiffViewerPage` mit Route `/diff/{DiffResultId:guid}`.
- Aufruf mit `PresentationMode="DiffViewerPresentationMode.Standalone"`.
- ARIA-Rolle des Root-Containers: `main`.
- Fehler-/Not-Found-Anzeige enthält „Back to Home“-Link.

## 6. Zustandsverantwortung und FR-4-Fallbacks

### `AufgabeDetail` (Orchestrierung)
- Lädt und hält den Kontext (`_latestDiffResultId`, Dateiauswahl, Workspace-Preview).
- Übergibt `HasSelectedFile`, `Preview` und `DiffResultId` an `DiffPreviewPanel`.
- Unterstützt direkte Navigation zur kompatiblen Route per `NavigationManager.NavigateTo($"/diff/{diffResultId}")`.

### `DiffPreviewPanel` (Fallback-Entscheidung)
- Entscheidet anhand des Zustands, was gerendert wird:
  1. Keine Datei ausgewählt → Hinweis „Wählen Sie links eine Datei aus.“
  2. Preview lädt noch (`Preview is null`) → Ladehinweis.
  3. `Preview.Hint` vorhanden → FR-4-Fallback-Hinweis (Warning) und optional `CurrentContent`.
  4. `DiffResultId` vorhanden → eingebetteter `DiffViewer`.
  5. Datei gelöscht (`Preview.IsDeleted`) → Hinweis „Datei gelöscht … kein Diff verfügbar.“
  6. Sonst → Hinweis „kein DiffResult vorhanden“.

### `DiffViewer` (Laden und Rendern)
- Lädt das Diff über `DiffService.GetDiffAsync(diffResultId, cancellationToken)`.
- Rendert abhängig vom Ladezustand:
  - Loading
  - Error
  - Not Found
  - Vollständige Diff-Ansicht (`DiffHeader`, `DiffToolbar`, `DiffContent`, `DiffFooter`).

## 7. Stabilitäts-Safeguards bei Parameterwechsel

`DiffViewer` schützt schnelle `DiffResultId`-Wechsel explizit über:

- `OnParametersSetAsync()` als zentralen Umschaltpunkt bei Parameteränderungen.
- Abbruch laufender Requests via `CancellationTokenSource` vor neuem Load.
- Versionsschutz via `loadingVersion` (`Interlocked.Increment`) gegen veraltete Async-Ergebnisse.
- Guard gegen unnötiges Reload, wenn dieselbe `DiffResultId` bereits geladen ist.
- Reset auf validierten Fehlerzustand bei `Guid.Empty`.

Diese Kombination verhindert, dass verspätete Responses einen neueren Zustand überschreiben.

## 8. Route-Kompatibilität

Die Route `/diff/{DiffResultId:guid}` bleibt kompatibel und wird durch `DiffViewerPage` als Wrapper bereitgestellt:

- `@page "/diff/{DiffResultId:guid}"`
- Übergabe der Route-Parameter an `DiffViewer`
- Erzwingung des Standalone-Modus

Damit bleibt Deep-Linking auf bekannte Diff-IDs erhalten, während dieselbe Viewer-Implementierung in der eingebetteten Vorschau genutzt wird.

## 9. Response

**Erfolg (200 OK)**

Die Route liefert die Blazor-Seite, die den Diff Viewer rendert. Beispiel des gerenderten HTML-Rahmens:

```html
<div class="diff-viewer" role="main" aria-label="Diff Viewer - File Comparison">
  <header class="diff-header"></header>
  <nav class="diff-toolbar" role="toolbar" aria-label="Diff viewer controls"></nav>
  <div class="diff-viewer__container">
    <div class="diff-content"></div>
  </div>
  <footer class="diff-footer" role="contentinfo" aria-label="Diff summary and statistics"></footer>
</div>
```

**Fehlerfälle**

- `404 Not Found` – kein Diff mit der angegebenen ID.
```html
<div class="diff-viewer__not-found alert alert-warning">
  <p>Diff not found.</p>
  <a href="/" class="btn btn-sm btn-outline-warning">Back to Home</a>
</div>
```

- `500 Internal Server Error` – Fehler beim Laden des Diffs.
```html
<div class="diff-viewer__error alert alert-danger" role="alert">
  <h4 class="alert-heading">Error Loading Diff</h4>
  <p>An error occurred while loading the diff. Please try again.</p>
  <a href="/" class="btn btn-sm btn-outline-danger">Back to Home</a>
</div>
```

## 10. Beispiel

```bash
curl -X GET "http://localhost:5000/diff/22222222-2222-2222-2222-222222222222"
```

Antwort (gekürzt auf den gerenderten Rahmen):

```html
<div class="diff-viewer" role="main" aria-label="Diff Viewer - File Comparison">
  <header class="diff-header"></header>
  <nav class="diff-toolbar" role="toolbar" aria-label="Diff viewer controls"></nav>
  <div class="diff-viewer__container">
    <div class="diff-content"></div>
  </div>
  <footer class="diff-footer" role="contentinfo" aria-label="Diff summary and statistics"></footer>
</div>
```

## 11. Verwandte Inhalte

- [HTTP-Endpunkte der Anwendung](./http-endpoints.md)
- [Diff API](./diff.md)
- [Live Project Browser mit Git-Status](./live-project-browser-git-status.md)
