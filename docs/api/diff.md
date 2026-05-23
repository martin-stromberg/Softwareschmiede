# Diff API (`/api/diff`)

Technische Dokumentation der öffentlichen Diff-Schnittstellen aus [`DiffController`](../../src/Softwareschmiede/Controllers/DiffController.cs).

Verwandte UI-Dokumentation: [`diff-viewer.md`](./diff-viewer.md) für die Blazor-Route `/diff/{DiffResultId:guid}`.

## Datenmodelle, Enums und Serviceverträge

- Request-/Response-DTOs: [`DiffController.cs`](../../src/Softwareschmiede/Controllers/DiffController.cs)
- Serviceverträge (implementierte Service-APIs):
  - [`DiffService`](../../src/Softwareschmiede/Application/Services/DiffService.cs)
  - [`DiffAlgorithmService`](../../src/Softwareschmiede/Application/Services/DiffAlgorithmService.cs)
  - [`DiffCachingService`](../../src/Softwareschmiede/Application/Services/DiffCachingService.cs)
- Relevante Tests:
  - [`DiffServiceTests`](../../src/Softwareschmiede.Tests/Application/Services/DiffServiceTests.cs)
  - [`DiffAlgorithmServiceTests`](../../src/Softwareschmiede.Tests/Application/Services/DiffAlgorithmServiceTests.cs)
  - [`DiffCachingServiceTests`](../../src/Softwareschmiede.Tests/Application/Services/DiffCachingServiceTests.cs)
- Enum-Definitionen:
  - [`DiffType`](../../src/Softwareschmiede/Domain/Enums/DiffType.cs): `0=Full`, `1=SideBySide`, `2=Split`
  - [`DiffCachingStrategy`](../../src/Softwareschmiede/Domain/Enums/DiffCachingStrategy.cs): `0=TTL`, `1=LRU`, `2=Manual`
  - [`DiffResultStatus`](../../src/Softwareschmiede/Domain/Enums/DiffResultStatus.cs): `0=Pending`, `1=Generated`, `2=Cached`, `3=Error`
  - [`DiffBlockType`](../../src/Softwareschmiede/Domain/Enums/DiffBlockType.cs): `0=Added`, `1=Removed`, `2=Modified`, `3=Context`
  - [`DiffLineStatus`](../../src/Softwareschmiede/Domain/Enums/DiffLineStatus.cs): `0=Added`, `1=Removed`, `2=Modified`, `3=Context`

> Hinweise zur aktuellen Implementierung:
> - Enum-Felder in normalen JSON-Properties werden numerisch serialisiert.
> - `statusBreakdown` verwendet Enum-Keys als JSON-Objektschlüssel (`"Generated"`, `"Error"`).
> - Der Algorithmus zählt aktuell Änderungen als `Added`/`Removed`; `modifiedLines` ist derzeit typischerweise `0` (siehe Tests).
> - `cachingStrategy` im Request wird aktuell nicht in die Service-Logik übernommen; im Response wird derzeit `0` (`TTL`) ausgegeben.

---

## Endpunkt: Diff erzeugen

### 1. Übersicht
Erzeugt einen neuen Diff zwischen zwei Textständen, speichert das Ergebnis und liefert Blöcke/Zeilen für die Anzeige zurück.

### 2. HTTP-Methode & Pfad
`POST /api/diff/generate`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- `Content-Type: application/json` *(required)*

**Path-/Query-Parameter**
- Keine.

**Request-Body (JSON)**
- `aufgabeId` (`string`, UUID) *(required)*
- `filePath` (`string`) *(optional; default `"Unknown"`)*
- `sourceContent` (`string`) *(required)*
- `targetContent` (`string`) *(required)*
- `sourceVersion` (`string`) *(optional; default `"v1"`)*
- `targetVersion` (`string`) *(optional; default `"v2"`)*
- `diffType` (`integer`) *(optional; default `0` = `Full`)*
- `cachingStrategy` (`integer`) *(optional; aktuell ohne Effekt in der Generierungslogik; Response bleibt `0` = `TTL`)*

```json
{
  "aufgabeId": "11111111-1111-1111-1111-111111111111",
  "filePath": "src/file.cs",
  "sourceContent": "a\nb\nc",
  "targetContent": "a\nx\nc",
  "sourceVersion": "v1",
  "targetVersion": "v2",
  "diffType": 0,
  "cachingStrategy": 2
}
```

### 5. Response
**Erfolg (200 OK)**

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "filePath": "src/file.cs",
  "sourceVersion": "v1",
  "targetVersion": "v2",
  "addedLines": 1,
  "removedLines": 1,
  "modifiedLines": 0,
  "status": 1,
  "diffType": 0,
  "cachingStrategy": 0,
  "generatedAt": "2026-05-22T17:00:00+00:00",
  "blocks": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "blockType": 3,
      "startLineSource": 1,
      "endLineSource": 3,
      "startLineTarget": 1,
      "endLineTarget": 3,
      "summaryContent": "",
      "lines": [
        {
          "id": "44444444-4444-4444-4444-444444444444",
          "lineNumber": 1,
          "content": "a",
          "status": 3,
          "isContext": true
        },
        {
          "id": "55555555-5555-5555-5555-555555555555",
          "lineNumber": 2,
          "content": "b",
          "status": 1,
          "isContext": false
        },
        {
          "id": "66666666-6666-6666-6666-666666666666",
          "lineNumber": 2,
          "content": "x",
          "status": 0,
          "isContext": false
        },
        {
          "id": "77777777-7777-7777-7777-777777777777",
          "lineNumber": 3,
          "content": "c",
          "status": 3,
          "isContext": true
        }
      ]
    }
  ]
}
```

**Fehlerfälle**

- `400 Bad Request` – `sourceContent` oder `targetContent` fehlt/leer
```json
{
  "type": "about:blank",
  "title": "Ungültige Anforderung",
  "status": 400,
  "detail": "Quell- und Zielinhalt dürfen nicht leer sein."
}
```

- `401 Unauthorized` – nur bei vorgeschalteter Auth-Middleware
```json
{
  "type": "about:blank",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required to access this resource."
}
```

- `404 Not Found` – derzeit nicht vom Controller für diesen Endpunkt erzeugt
```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

- `500 Internal Server Error` – unerwarteter Fehler (z. B. Aufgabe nicht vorhanden oder Servicefehler)
```json
{
  "type": "about:blank",
  "title": "Fehler beim Generieren des Diffs",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

### 6. Beispiel (curl)
```bash
curl -X POST "http://localhost:5000/api/diff/generate" \
  -H "Content-Type: application/json" \
  -d '{
    "aufgabeId":"11111111-1111-1111-1111-111111111111",
    "filePath":"src/file.cs",
    "sourceContent":"a\nb\nc",
    "targetContent":"a\nx\nc",
    "sourceVersion":"v1",
    "targetVersion":"v2",
    "diffType":0,
    "cachingStrategy":2
  }'
```

Response (200):
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "filePath": "src/file.cs",
  "sourceVersion": "v1",
  "targetVersion": "v2",
  "addedLines": 1,
  "removedLines": 1,
  "modifiedLines": 0,
  "status": 1,
  "diffType": 0,
  "cachingStrategy": 0,
  "generatedAt": "2026-05-22T17:00:00+00:00",
  "blocks": []
}
```

Verwandte Endpunkte: [Diff abrufen](#endpunkt-diff-abrufen), [Diffs auflisten](#endpunkt-diffs-auflisten), [Statistiken abrufen](#endpunkt-statistiken-abrufen).

---

## Endpunkt: Diff abrufen

### 1. Übersicht
Lädt ein einzelnes Diff-Ergebnis inklusive aller Blöcke und Zeilen über die Diff-ID.

### 2. HTTP-Methode & Pfad
`GET /api/diff/{id}`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- Optional: `Accept: application/json`

**Path-/Query-Parameter**
- `id` (`string`, UUID, path) *(required)*

**Request-Body**
- Keiner.

### 5. Response
**Erfolg (200 OK)**  
Antwortschema wie bei [Diff erzeugen](#endpunkt-diff-erzeugen).

**Fehlerfälle**

- `400 Bad Request` – ungültiges UUID-Format
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

- `404 Not Found` – Diff nicht vorhanden
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

### 6. Beispiel (curl)
```bash
curl -X GET "http://localhost:5000/api/diff/22222222-2222-2222-2222-222222222222" \
  -H "Accept: application/json"
```

Response (200):
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "filePath": "src/file.cs",
  "sourceVersion": "v1",
  "targetVersion": "v2",
  "addedLines": 1,
  "removedLines": 1,
  "modifiedLines": 0,
  "status": 1,
  "diffType": 0,
  "cachingStrategy": 0,
  "generatedAt": "2026-05-22T17:00:00+00:00",
  "blocks": []
}
```

Verwandte Endpunkte: [Diff erzeugen](#endpunkt-diff-erzeugen), [Cache invalidieren](#endpunkt-cache-invalidieren), [Diff löschen](#endpunkt-diff-löschen).

---

## Endpunkt: Diffs auflisten

### 1. Übersicht
Liefert eine paginierte Liste aller Diff-Ergebnisse zu einer Aufgabe.

### 2. HTTP-Methode & Pfad
`GET /api/diff`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- Optional: `Accept: application/json`

**Path-/Query-Parameter**
- `aufgabeId` (`string`, UUID, query) *(required)*
- `page` (`integer`, query) *(optional; default `1`; min `1`)*
- `pageSize` (`integer`, query) *(optional; default `20`; min `1`; max `100`)*

**Request-Body**
- Keiner.

### 5. Response
**Erfolg (200 OK)**

```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "filePath": "src/file.cs",
      "sourceVersion": "v1",
      "targetVersion": "v2",
      "addedLines": 1,
      "removedLines": 1,
      "modifiedLines": 0,
      "status": 1,
      "diffType": 0,
      "cachingStrategy": 0,
      "generatedAt": "2026-05-22T17:00:00+00:00",
      "blocks": []
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

**Fehlerfälle**

- `400 Bad Request` – ungültige Pagination
```json
{
  "type": "about:blank",
  "title": "Ungültige Parameter",
  "status": 400,
  "detail": "Page muss >= 1 sein und PageSize zwischen 1 und 100 liegen."
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

- `404 Not Found` – derzeit nicht vom Controller für Listenabfragen erzeugt
```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

- `500 Internal Server Error`
```json
{
  "type": "about:blank",
  "title": "Fehler beim Abrufen der Diffs",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

### 6. Beispiel (curl)
```bash
curl -X GET "http://localhost:5000/api/diff?aufgabeId=11111111-1111-1111-1111-111111111111&page=1&pageSize=20" \
  -H "Accept: application/json"
```

Response (200):
```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "filePath": "src/file.cs",
      "sourceVersion": "v1",
      "targetVersion": "v2",
      "addedLines": 1,
      "removedLines": 1,
      "modifiedLines": 0,
      "status": 1,
      "diffType": 0,
      "cachingStrategy": 0,
      "generatedAt": "2026-05-22T17:00:00+00:00",
      "blocks": []
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

Verwandte Endpunkte: [Diff erzeugen](#endpunkt-diff-erzeugen), [Statistiken abrufen](#endpunkt-statistiken-abrufen).

---

## Endpunkt: Statistiken abrufen

### 1. Übersicht
Liefert aggregierte Kennzahlen zu allen Diffs einer Aufgabe.

### 2. HTTP-Methode & Pfad
`GET /api/diff/statistics`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- Optional: `Accept: application/json`

**Path-/Query-Parameter**
- `aufgabeId` (`string`, UUID, query) *(required)*

**Request-Body**
- Keiner.

### 5. Response
**Erfolg (200 OK)**

```json
{
  "totalDiffCount": 2,
  "totalAddedLines": 4,
  "totalRemovedLines": 3,
  "totalModifiedLines": 1,
  "averageLinesPerDiff": 4,
  "oldestDiff": "2026-05-22T16:59:00+00:00",
  "newestDiff": "2026-05-22T17:00:00+00:00",
  "statusBreakdown": {
    "Generated": 1,
    "Error": 1
  }
}
```

**Fehlerfälle**

- `400 Bad Request` – ungültiges UUID-Format in `aufgabeId`
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

- `404 Not Found` – derzeit nicht vom Controller für Statistikabfragen erzeugt
```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

- `500 Internal Server Error`
```json
{
  "type": "about:blank",
  "title": "Fehler beim Abrufen der Statistiken",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

### 6. Beispiel (curl)
```bash
curl -X GET "http://localhost:5000/api/diff/statistics?aufgabeId=11111111-1111-1111-1111-111111111111" \
  -H "Accept: application/json"
```

Response (200):
```json
{
  "totalDiffCount": 2,
  "totalAddedLines": 4,
  "totalRemovedLines": 3,
  "totalModifiedLines": 1,
  "averageLinesPerDiff": 4,
  "oldestDiff": "2026-05-22T16:59:00+00:00",
  "newestDiff": "2026-05-22T17:00:00+00:00",
  "statusBreakdown": {
    "Generated": 1,
    "Error": 1
  }
}
```

Verwandte Endpunkte: [Diffs auflisten](#endpunkt-diffs-auflisten), [Diff erzeugen](#endpunkt-diff-erzeugen).

---

## Endpunkt: Diff löschen

### 1. Übersicht
Löscht ein bestehendes Diff-Ergebnis inklusive zugehöriger Persistenzdaten und invalidiert den Cache.

### 2. HTTP-Methode & Pfad
`DELETE /api/diff/{id}`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- Optional: `Accept: application/json`

**Path-/Query-Parameter**
- `id` (`string`, UUID, path) *(required)*

**Request-Body**
- Keiner.

### 5. Response
**Erfolg (204 No Content)**  
Leerer Body.

**Fehlerfälle**

- `400 Bad Request` – ungültiges UUID-Format
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

- `404 Not Found` – Diff existiert nicht
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
  "title": "Fehler beim Löschen des Diffs",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

### 6. Beispiel (curl)
```bash
curl -X DELETE "http://localhost:5000/api/diff/22222222-2222-2222-2222-222222222222"
```

Response (204): *(empty body)*

Verwandte Endpunkte: [Diff abrufen](#endpunkt-diff-abrufen), [Cache invalidieren](#endpunkt-cache-invalidieren).

---

## Endpunkt: Cache invalidieren

### 1. Übersicht
Invalidiert den Cache eines bestehenden Diff-Ergebnisses, ohne den persistierten Diff selbst zu löschen.

### 2. HTTP-Methode & Pfad
`POST /api/diff/{id}/invalidate-cache`

### 3. Authentifizierung
Keine.

### 4. Request
**Header**
- Optional: `Accept: application/json`

**Path-/Query-Parameter**
- `id` (`string`, UUID, path) *(required)*

**Request-Body**
- Keiner.

### 5. Response
**Erfolg (204 No Content)**  
Leerer Body.

**Fehlerfälle**

- `400 Bad Request` – ungültiges UUID-Format
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

- `404 Not Found` – Diff existiert nicht
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
  "title": "Fehler beim Invalidieren des Caches",
  "status": 500,
  "detail": "Ein unerwarteter Fehler ist aufgetreten."
}
```

### 6. Beispiel (curl)
```bash
curl -X POST "http://localhost:5000/api/diff/22222222-2222-2222-2222-222222222222/invalidate-cache"
```

Response (204): *(empty body)*

Verwandte Endpunkte: [Diff abrufen](#endpunkt-diff-abrufen), [Diff löschen](#endpunkt-diff-löschen).
