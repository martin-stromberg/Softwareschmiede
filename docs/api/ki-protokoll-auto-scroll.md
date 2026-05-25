# KI-Protokoll Auto-Scroll (`/aufgaben/{Id:guid}`)

## 1. Übersicht

Das Feature **KI-Protokoll Auto-Scroll** steuert das Scroll-Verhalten im Arbeitsprotokoll der Seite `AufgabeDetail`.  
Beim erstmaligen Einblenden wird automatisch ans Ende gescrollt, bei neuen Inhalten nur dann weitergescrollt, wenn der Nutzer zuvor am Ende war.

## 2. Methode/Pfad oder Interface

**UI-Route (Host-Interface)**  
`GET /aufgaben/{Id:guid}`

**JS-Interop Interface (Scroll-Adapter)**  
- `softwareschmiedeLogScroll.getMetrics(selector)`
- `softwareschmiedeLogScroll.scrollToEnd(selector)`

**Container-Selektoren**
- Streaming-Protokoll: `#streamingOutput`
- Historisches Protokoll: `#historyProtokoll`

## 3. Authentifizierung

Aktuell keine erzwungene Authentifizierung auf dieser UI-Route.  
Bei vorgeschalteter Auth-Middleware kann `401 Unauthorized` auftreten.

## 4. Request

### Header

| Name | Wert | Pflicht | Beschreibung |
|---|---|---:|---|
| `Accept` | `text/html` | Nein | Browser-Request für die Blazor-Route. |
| `Authorization` | `Bearer <token>` | Nein | Nur bei vorgeschalteter Auth-Middleware relevant. |

### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `Id` | `Guid` | Ja *(required)* | Aufgaben-ID für die Detailseite mit KI-Protokoll. |

### Query-Parameter

| Name | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `view` | `string` | Nein | UI-Ansichtsmodus der Detailseite; Auto-Scroll-Logik bleibt unverändert aktiv. |

### Request-Body

Keiner.

### Interface-Request (JS-Interop)

#### `softwareschmiedeLogScroll.getMetrics(selector)`

| Feld | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `selector` | `string` | Ja *(required)* | CSS-Selector des Scroll-Containers (`#streamingOutput` oder `#historyProtokoll`). |

#### `softwareschmiedeLogScroll.scrollToEnd(selector)`

| Feld | Typ | Pflicht | Beschreibung |
|---|---|---:|---|
| `selector` | `string` | Ja *(required)* | CSS-Selector des Scroll-Containers. |

## 5. Response

### Erfolgsfall

#### 5.1 UI-Route erfolgreich geladen

- **HTTP-Status:** `200 OK`
- **Technisches Ergebnis:** Seite `AufgabeDetail` ist geladen; Auto-Scroll-State wird in `OnAfterRenderAsync` angewendet.

```json
{
  "route": "/aufgaben/9d0e3ca8-3506-4572-af03-5736fc951f89",
  "component": "AufgabeDetail",
  "scroll": {
    "streamingSelector": "#streamingOutput",
    "historySelector": "#historyProtokoll",
    "thresholdPx": 16,
    "initialScrollPending": true
  }
}
```

#### 5.2 `getMetrics` erfolgreich

- **Interface-Response:** `double[]` mit 4 Werten
- Reihenfolge: `[scrollTop, scrollHeight, clientHeight, existsFlag]`

```json
{
  "identifier": "softwareschmiedeLogScroll.getMetrics",
  "selector": "#streamingOutput",
  "result": [84.0, 400.0, 300.0, 1.0],
  "derivedIsAtEnd": true
}
```

#### 5.3 `scrollToEnd` erfolgreich

- **Interface-Response:** `true`

```json
{
  "identifier": "softwareschmiedeLogScroll.scrollToEnd",
  "selector": "#historyProtokoll",
  "result": true
}
```

### Fehlerfälle

#### `400 Bad Request` (ungültige `Id`)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The value 'not-a-guid' is not valid for Id.",
  "errors": {
    "Id": [
      "The value 'not-a-guid' is not valid."
    ]
  }
}
```

#### `401 Unauthorized` (bei vorgeschalteter Auth)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required to access this resource."
}
```

#### `404 Not Found` (Aufgabe nicht gefunden)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Aufgabe 9d0e3ca8-3506-4572-af03-5736fc951f89 wurde nicht gefunden."
}
```

#### `500 Internal Server Error` (JS-Interop/Render-Fehler)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Scroll-Endzustand konnte nicht ermittelt werden (#streamingOutput)."
}
```

## 6. State Transitions (Auto-Scroll-Contract)

| Trigger | Vorbedingung | Aktion | Ergebnis |
|---|---|---|---|
| Initiales Einblenden des Containers | Container zuvor nicht sichtbar, jetzt sichtbar mit Inhalt | `TryScrollToEndAsync(selector)` | Scroll springt ans Ende, `*_InitialScrollPending = false` |
| Neuer Inhalt bei Nutzer am Ende | `IsAtEnd(...) == true` | Vor Update `Capture*ScrollStateBeforeUpdateAsync()`, nach Update `ApplyPendingScrollAsync()` mit `scrollToEnd` | Auto-Follow-Scroll bleibt aktiv |
| Neuer Inhalt bei manuellem Hochscrollen | `IsAtEnd(...) == false` | Pending-Version wird verarbeitet **ohne** `scrollToEnd` | Aktuelle Scroll-Position bleibt erhalten |

## 7. Beispiel (curl + technisches Interface-Beispiel)

### 7.1 UI-Aufruf

```bash
curl -i "http://localhost:5000/aufgaben/9d0e3ca8-3506-4572-af03-5736fc951f89?view=detail" \
  -H "Accept: text/html"
```

Beispiel-Response (gekürzt auf technische Metadaten des Aufrufs):

```json
{
  "status": 200,
  "contentType": "text/html; charset=utf-8",
  "componentBootstrap": "AufgabeDetail"
}
```

### 7.2 Technisches JS-Interop-Beispiel

```javascript
const metrics = window.softwareschmiedeLogScroll.getMetrics("#streamingOutput");
const isAtEnd = (metrics[1] - (metrics[0] + metrics[2])) <= 16;
if (isAtEnd) {
  window.softwareschmiedeLogScroll.scrollToEnd("#streamingOutput");
}
```

## 8. Verwandte Dokumente

- HTTP-Übersicht: [http-endpoints.md](./http-endpoints.md)
- API-Index: [README.md](./README.md)
- KI-Protokoll Markdown-Contract: [../flows/ki-arbeitsprotokoll-rendering-flow.md](../flows/ki-arbeitsprotokoll-rendering-flow.md)
- Diff-Viewer-Contract (vergleichbares UI-Contract-Muster): [diff-viewer.md](./diff-viewer.md)
