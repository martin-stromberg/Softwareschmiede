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

## Feature-Impact: KI-Arbeitsprotokoll als Markdown und sichere Render-Pipeline

**Ergebnis:** **kein API-Impact** auf öffentliche HTTP-Schnittstellen, aber **interner Contract-Impact** in Persistenz- und Renderlogik des Protokollinhalts.

Das Feature wurde in Application-Service und Blazor-Seite umgesetzt, ohne neue oder geänderte öffentliche HTTP-Endpunkte.

### API-relevante Umsetzung (ohne HTTP-Änderung)

| Bereich | Status | Umsetzung / Referenz |
|---|---|---|
| Persistenzformat KI-Antwort als Markdown | Erfüllt | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`BuildKiArbeitsprotokollMarkdown`) mit Datumszeile `# yyyy-MM-dd`, `RunId` und Schritttrennung `## Schritt n` |
| Fehlerfall nutzt identisches Protokollschema | Erfüllt | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`KiStartenAsync` → `BuildKiArbeitsprotokollMarkdown` auch bei Exceptions) |
| Markdown-Rendering in der Webausgabe | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`RenderProtokollInhalt`, `Markdown.ToHtml`) |
| Sanitizing der gerenderten HTML-Ausgabe | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`MarkdownPipelineBuilder().DisableHtml()`, `SanitizeMarkdownHtml`) |
| Fallback für robuste Lesbarkeit | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`BuildFallbackHtml` mit HTML-encoding in `<pre>`) |

### Interner technischer Contract (Kurzform)

- **Formatvertrag Protokollinhalt:** Jeder KI-Antworteintrag folgt dem Schema `# {Datum}` + `- RunId: ...` + `## Schritt n`.
- **Rendervertrag UI:** Persistierter Markdown-Inhalt wird in HTML transformiert, danach sanitiziert und nur dann angezeigt; bei Fehlern/leerem Sanitizing greift ein sicherer `<pre>`-Fallback.
- **HTTP-Vertrag unverändert:** Keine Änderung an `Program.cs`-Routing (`MapRazorComponents`) und keine öffentlichen API-Routen hinzugefügt.

### Verknüpfte Dokumentation

- Fachliche Sicht: [`../business/features/F005-aufgabenprotokoll.md`](../business/features/F005-aufgabenprotokoll.md)
- Ablaufübersicht: [`../flows/README.md`](../flows/README.md)
- Technischer Ablauf (KI-Streaming/Protokollierung): [`../flows/development-process-flow.md#ablauf-2-ki-streaming-und-protokollierung`](../flows/development-process-flow.md#ablauf-2-ki-streaming-und-protokollierung)
- Detaillierter Rendering-Flow: [`../flows/ki-arbeitsprotokoll-rendering-flow.md`](../flows/ki-arbeitsprotokoll-rendering-flow.md)

## Nächste Erweiterung

Falls künftig HTTP-Endpunkte eingeführt werden, sollte diese Datei um eine Endpoint-Matrix erweitert werden:

- Route
- HTTP-Methode
- Request-/Response-Schema
- Authentifizierung/Autorisierung
- Fehlercodes
