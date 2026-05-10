# HTTP-Endpunkte – Softwareschmiede

## Überblick

Die Softwareschmiede stellt im aktuellen Stand **keine öffentlichen REST- oder Minimal-API-Endpunkte** bereit.

Die Anwendung ist als Blazor-Webanwendung umgesetzt und mappt Razor-Komponenten direkt:

- `app.MapRazorComponents<App>()`
- `AddInteractiveServerRenderMode()`
- `AddInteractiveWebAssemblyRenderMode()`

Referenz: `src/Softwareschmiede/Program.cs`

## Auswirkungen auf die API-Dokumentation

- Es gibt aktuell kein `src/ReceiptScanner.Api/Endpoints/` und kein alternatives Endpunkt-Verzeichnis in `src/`.
- Die technische API-Dokumentation fokussiert daher auf:
  - Plugin-Schnittstellen (`IPlugin`, `IGitPlugin`, `IKiPlugin`)
  - Infrastruktur-Verträge und Laufzeitverhalten (z. B. Workdir-Resolution)

## Nächste Erweiterung

Falls künftig HTTP-Endpunkte eingeführt werden, sollte diese Datei um eine Endpoint-Matrix erweitert werden:

- Route
- HTTP-Methode
- Request-/Response-Schema
- Authentifizierung/Autorisierung
- Fehlercodes
