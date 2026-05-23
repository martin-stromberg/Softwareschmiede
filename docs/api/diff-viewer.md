# Diff Viewer (`/diff/{DiffResultId:guid}`)

Der Diff Viewer ist die Ansicht, in der der Anwender ein bereits erzeugtes Diff-Ergebnis prüft. Er dient dazu, Unterschiede zwischen zwei Ständen einer Datei schnell zu erkennen, zu navigieren und fachlich zu bewerten.

## 1. Nutzerweg von der Startseite

1. Der Anwender startet auf der Startseite (`/`) und sieht das Dashboard mit den aktiven Aufgaben.
2. Er öffnet die gewünschte Aufgabe über den Eintrag in der Aufgabenliste.
3. In der aktuellen Aufgabenansicht gibt es **keinen eigenen Button**, der direkt zum Diff Viewer navigiert.
4. Der Aufruf erfolgt über die Detailroute `/diff/{DiffResultId:guid}` (z. B. per direkter URL mit bekannter Diff-ID).

Die Diff-Ansicht ist damit keine eigenständige Hauptnavigation, sondern eine Detailansicht zu einem bereits erzeugten Diff.

## 2. Funktionaler Zweck

Der Diff Viewer unterstützt die Sichtprüfung von Änderungen. Er zeigt:

- Datei- und Versionsinformationen
- die betroffenen Zeilen und Änderungsblöcke
- eine Zusammenfassung der Änderungen
- Such- und Navigationsfunktionen im Diff
- eine Darstellung der Änderung aus Sicht der Zielversion

Damit hilft die Ansicht beim fachlichen Abgleich, bei Reviews und beim schnellen Auffinden von relevanten Änderungen.

## 3. HTTP-Methode & Pfad

`GET /diff/{DiffResultId:guid}`

## 4. Authentifizierung

Keine.

## 5. Request

**Header**
- Keine speziellen Header erforderlich

**Path-Parameter**

| Name | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `DiffResultId` | `Guid` | Ja | ID des anzuzeigenden Diff-Ergebnisses. |

**Query-Parameter**
- Keine

**Request-Body**
- Keiner

## 6. Response

**Erfolg (200 OK)**

Die Route liefert die Blazor-Seite. Nach dem Laden zeigt die Anwendung je nach Zustand:

1. eine Ladeanzeige
2. eine Fehlermeldung, wenn das Laden fehlschlägt
3. die vollständige Diff-Ansicht mit Kopfbereich, Toolbar, Inhaltsbereich und Footer

Beispiel des gerenderten HTML-Rahmens:

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

- `404 Not Found` – ungültige Route oder kein Diff mit der angegebenen ID
```html
<div class="diff-viewer__not-found alert alert-warning">
  <p>Diff not found.</p>
  <a href="/" class="btn btn-sm btn-outline-warning">Back to Home</a>
</div>
```

- `500 Internal Server Error` – Fehler beim Laden des Diffs
```html
<div class="diff-viewer__error alert alert-danger" role="alert">
  <h4 class="alert-heading">Error Loading Diff</h4>
  <p>An error occurred while loading the diff. Please try again.</p>
  <a href="/" class="btn btn-sm btn-outline-danger">Back to Home</a>
</div>
```

## 7. Beispiel

```bash
curl -X GET "http://localhost:5000/diff/22222222-2222-2222-2222-222222222222"
```

Antwort:

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

## 8. Verwandte Inhalte

- [HTTP-Endpunkte der Anwendung](./http-endpoints.md)
- [Diff API](./diff.md)
