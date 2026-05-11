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

## Feature-Impact: Kontextsteuerung bei Folgeanweisungen

**Ergebnis:** **kein API-Impact** auf öffentliche HTTP-Schnittstellen.

Das Feature „Kontextsteuerung bei Folgeanweisungen (Kontext mitgeben / ignorieren / neu beginnen)“ wurde in der Blazor-UI und in der internen Orchestrierung umgesetzt (Aufgabe-Detailseite und Application-Service), ohne neue oder geänderte öffentliche HTTP-Endpunkte.

### API-relevante Umsetzung (ohne HTTP-Änderung)

| Bereich | Status | Umsetzung / Referenz |
|---|---|---|
| UI-Kontextmodi vorhanden (3 feste Werte) | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor` (`FolgeanweisungsKontextmodus`: `KontextMitgeben`, `KontextIgnorieren`, `KontextNeuBeginnen`) |
| Guardrail bei „Kontext neu beginnen“ | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor` + `AufgabeDetail.razor.cs` (Bestätigung erforderlich vor Senden) |
| Modusübergabe in den Laufstart | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`KiMitPromptStartenAsync(..., FolgeanweisungsKontextmodus?)`) |
| Prompt-Building je Modus inkl. Kontextdatei-Lifecycle | Erfüllt | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`KiStartenAsync`, `BuildFollowPromptWithContextAsync`) |
| KI-Plugin-Contract unverändert | Erfüllt | `docs/api/plugin-interfaces.md#startdevelopmentasync` |
| Testabdeckung erweitert | Erfüllt | `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailFolgePromptTests.cs`, `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`, `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs` |

### Fachliche und architektonische Referenzen

Zur Vermeidung von Redundanz werden Anforderungen, Architekturentscheidungen und Testdetails in den jeweiligen Fachdokumenten geführt:

- Anforderungen: [`../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md`](../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md)
- Architektur: [`../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md`](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md)
- Architektur-Review: [`../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md`](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md)
- Testplan: [`../tests/testplan-kontextsteuerung-folgeanweisungen.md`](../tests/testplan-kontextsteuerung-folgeanweisungen.md)
- Testlücken: [`../tests/testluecken-kontextsteuerung-folgeanweisungen.md`](../tests/testluecken-kontextsteuerung-folgeanweisungen.md)

## Nächste Erweiterung

Falls künftig HTTP-Endpunkte eingeführt werden, sollte diese Datei um eine Endpoint-Matrix erweitert werden:

- Route
- HTTP-Methode
- Request-/Response-Schema
- Authentifizierung/Autorisierung
- Fehlercodes
