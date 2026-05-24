# App-Favicon-Integration (`favicon-hammer-pick.svg`)

## Übersicht

Dieses Dokument beschreibt den App-level Contract für die Einbindung des SVG-Favicons `favicon-hammer-pick.svg`.
Es handelt sich um einen statischen Asset-/Markup-Contract und **nicht** um einen HTTP-Endpoint-Contract.

## App-Level Integration

Die Root-Komponente `src/Softwareschmiede/Components/App.razor` referenziert das Asset im `<head>` bewusst in drei Varianten:

- `<link rel="icon" type="image/svg+xml" sizes="any" href="favicon-hammer-pick.svg" />`
- `<link rel="shortcut icon" type="image/svg+xml" href="favicon-hammer-pick.svg" />`
- `<link rel="mask-icon" href="favicon-hammer-pick.svg" color="#f59e0b" />`

Legacy-Referenzen auf `favicon.ico` und `favicon.png` sind entfernt.

## Static-Asset-Contract

- Asset-Pfad: `src/Softwareschmiede/wwwroot/favicon-hammer-pick.svg`
- Auslieferung über statische Dateien unter Root-Pfad `/favicon-hammer-pick.svg`
- Struktur-/Branding-Marker im SVG:
  - `viewBox="0 0 64 64"`
  - `<title>Softwareschmiede Favicon</title>`
  - `<desc>Crossed hammer and pick symbol for the Softwareschmiede web application.</desc>`
  - Primärfarbe `#f59e0b`

## HTTP-Impact

Für das Feature **„favicon-hammer-pick-svg“** wurden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.
Die Änderung betrifft ausschließlich statische Asset-Auslieferung und Head-Markup der App.

## Testnachweise

- `src/Softwareschmiede.Tests/Components/AppTests.cs`
  - Validiert alle drei Link-Varianten (`icon`, `shortcut icon`, `mask-icon`)
  - Erzwingt das Entfernen von `favicon.ico`/`favicon.png`
  - Prüft genau drei Referenzen auf `favicon-hammer-pick.svg`
- `src/Softwareschmiede.Tests/Infrastructure/StaticAssets/FaviconHammerPickSvgTests.cs`
  - Validiert Existenz des SVGs in `wwwroot`
  - Prüft zentrale SVG-Marker (Namespace, ViewBox, Title/Desc, Farbmarker)

## Verknüpfte Dokumentation

- [HTTP-Endpunkte der Anwendung](./http-endpoints.md)
- [API-Index](./README.md)
