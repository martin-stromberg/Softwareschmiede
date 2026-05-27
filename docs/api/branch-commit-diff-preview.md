# Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview

## 1. Übersicht

Das Feature erweitert den Repository-Explorer um Branch-Commit-Knoten mit lazy geladenem Dateibaum und Commit-spezifischer Vorschau.  
Öffentlich bleibt die HTTP-Fläche unverändert; es wird weiterhin nur die bestehende Diff-API verwendet.

## 2. HTTP-Methode & Pfad

`GET /api/diff/{id}` *(bestehender Endpunkt, kein neuer Feature-Endpunkt)*

## 3. Authentifizierung

Keine (wie in [diff.md](./diff.md)).

## 4. Request

**Header**
- Optional: `Accept: application/json`

**Path-Parameter**
- `id` (`Guid`, path) *(required)* – DiffResult-ID.

**Query-Parameter**
- Keine.

**Request-Body**
- Keiner.

## 5. Response

**Erfolg (200 OK)**

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "filePath": "src/module/alpha.cs",
  "sourceVersion": "HEAD~1",
  "targetVersion": "HEAD",
  "addedLines": 1,
  "removedLines": 0,
  "modifiedLines": 0,
  "status": 1,
  "diffType": 0,
  "cachingStrategy": 0,
  "generatedAt": "2026-05-27T12:00:00+00:00",
  "blocks": []
}
```

**Fehlerfälle**

- `400 Bad Request` – ungültige ID
```json
{
  "type": "about:blank",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The value 'invalid-guid' is not valid."
}
```

- `401 Unauthorized`
```json
{
  "type": "about:blank",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required to access this resource."
}
```

- `404 Not Found`
```json
{
  "type": "about:blank",
  "title": "Diff nicht gefunden",
  "status": 404,
  "detail": "Kein Diff mit ID '22222222-2222-2222-2222-222222222223' existiert."
}
```

- `500 Internal Server Error`
```json
{
  "type": "about:blank",
  "title": "Fehler beim Abrufen des Diffs",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

## 6. Beispiel (curl)

```bash
curl -X GET "http://localhost:5000/api/diff/22222222-2222-2222-2222-222222222222" \
  -H "Accept: application/json"
```

Response (200):
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "filePath": "src/module/alpha.cs",
  "sourceVersion": "HEAD~1",
  "targetVersion": "HEAD",
  "addedLines": 1,
  "removedLines": 0,
  "modifiedLines": 0,
  "status": 1,
  "diffType": 0,
  "cachingStrategy": 0,
  "generatedAt": "2026-05-27T12:00:00+00:00",
  "blocks": []
}
```

## Interner Contract (kein öffentlicher Endpoint)

- `IGitWorkspaceBrowserService.LoadSnapshotAsync(...)` liefert `WorkspaceSnapshot.BranchCommits`.
- `LoadCommitFilesAsync(...)` lädt Commit-Dateien über `git diff-tree --root --no-commit-id -r --name-status -z`.
- `LoadCommitPreviewAsync(...)` lädt `current` über `git show <sha>:path` und `original` über `git show <sha>^:path`.
- `AufgabeDetail` setzt für Commit-Dateien `_selectedWorkspaceDiffResultId = null`; Darstellung erfolgt über `FilePreview` (kein DiffResult-Lookup).
- Für Working-Tree-Dateien bleibt dateispezifischer Lookup aktiv (`AufgabeService.GetLatestDiffResultIdForFileAsync(...)`).

## Architektur, Tests und Grenzen

- **Requirements:** [live-project-browser-git-status-requirements-analysis.md](../requirements/live-project-browser-git-status-requirements-analysis.md), [diffviewer-correct-diff-display-requirements-analysis.md](../requirements/diffviewer-correct-diff-display-requirements-analysis.md)
- **Architektur:** [live-project-browser-git-status-architecture-blueprint.md](../architecture/live-project-browser-git-status-architecture-blueprint.md), [diffviewer-correct-diff-display-architecture-blueprint.md](../architecture/diffviewer-correct-diff-display-architecture-blueprint.md)
- **Improvements:** [live-project-browser-git-status-architecture-review.md](../improvements/live-project-browser-git-status-architecture-review.md), [diffviewer-correct-diff-display-architecture-review.md](../improvements/diffviewer-correct-diff-display-architecture-review.md)
- **Tests:** `GitWorkspaceBrowserServiceTests`, `CommitTreePresenterTests`, `AufgabeDetailWorkspacePreviewBunitTests`, `AufgabeServiceTests`
- **Bekannte Grenzen:** keine neuen REST-Endpunkte; kein Commit-DiffResult-Lookup; bei fehlender Basisreferenz keine Branch-Commit-Liste.
