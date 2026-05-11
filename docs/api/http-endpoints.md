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

## Feature-Impact: Agent-Auswahl bei Folgeanweisungen

**Ergebnis:** **kein API-Impact** auf öffentliche HTTP-Schnittstellen.

Das Feature „Agent-Auswahl bei Folgeanweisungen“ wurde in der Blazor-UI und in der internen Orchestrierung umgesetzt (Aufgabe-Detailseite), ohne neue oder geänderte öffentliche HTTP-Endpunkte.

### Nachvollziehbarkeit der Akzeptanzkriterien (ohne HTTP-Änderung)

| Kriterium | Status | Umsetzung / Referenz |
|---|---|---|
| 1) Agent-Auswahl bei Folgeanweisungen sichtbar/verfügbar | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor` (Select mit `@bind="_folgeAgentName"` im Folge-Prompt) |
| 2) Standardwert = initial gewählter Agent | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`LadeAsync*`: `_folgeAgentName = _aufgabe.AgentenName`) |
| 3) Auswahl vor Absenden änderbar | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor` (UI-Select für `_folgeAgentName`) |
| 4) Folgeanweisung geht an tatsächlich ausgewählten Agenten | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`FolgePromptAsync` → `KiMitPromptStartenAsync(_folgePrompt, _folgeAgentName)`), zusätzlich abgesichert durch `AufgabeDetailFolgePromptTests` und `EntwicklungsprozessServiceTests` |
| 5) Initialprompt-Verhalten unverändert | Erfüllt | `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`KiStartenAsync` unverändert über `_kiAgentName`), abgesichert durch `AufgabeDetailFolgePromptTests.KiStartenAsync_ShouldKeepInitialPromptBehavior` |

## Nächste Erweiterung

Falls künftig HTTP-Endpunkte eingeführt werden, sollte diese Datei um eine Endpoint-Matrix erweitert werden:

- Route
- HTTP-Methode
- Request-/Response-Schema
- Authentifizierung/Autorisierung
- Fehlercodes
