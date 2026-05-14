# Testplan – AufgabeDetail: projektspezifische IGitPlugin-Auswahl

## Ziel
Absichern, dass `AufgabeDetail` und `GitOrchestrationService` für Git-Aktionen das projektspezifisch aufgelöste `IGitPlugin` verwenden und nicht versehentlich nur das injizierte Default-Plugin.

## Scope
- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`

## Teststrategie

### 1) Service-Ebene (`GitOrchestrationServiceTests`)
- Verhaltensnahe Prüfung der Plugin-Auflösung pro Aktion.
- Pflichtfälle:
  - Aufgaben-Repository mit explizitem `PluginTyp` wird bevorzugt.
  - Kein Aufgaben-Repository + genau ein aktives Projekt-Repository (`LocalDirectoryPlugin`) nutzt das lokale Plugin.
  - Mehrdeutige Projekt-Repositories führen zu bewusstem Standard-Fallback.
  - Nicht auflösbarer `PluginTyp` führt ebenfalls zum Standard-Fallback.

### 2) UI-/Integrationsnahe Ebene (`AufgabeDetailGitActionsBunitTests`)
- `AufgabeDetail` verwendet den injizierten `GitOrchestrationService` mit derselben Auflösungslogik.
- Pflichtfälle:
  - Projektspezifisch ausgewähltes GitHub-Plugin bleibt aktiv, auch wenn Default lokal ist.
  - Projektspezifisch ausgewähltes LocalDirectory-Plugin bleibt aktiv, auch wenn Default GitHub ist.
  - Pull-Aufruf erfolgt auf dem effektiv ausgewählten Plugin.

## Qualitätskriterien
- Keine direkte Kopplung der Assertions an interne Felder; Fokus auf beobachtbares Laufzeitverhalten.
- LocalRepository-/LocalDirectory-Szenario ist explizit als eigener Pflichtfall vorhanden.
- Bestehende Capability- und Aktionsleisten-Tests bleiben grün.

## Validierung
- Build: `dotnet build .\Softwareschmiede.slnx`
- Tests: `dotnet test .\Softwareschmiede.slnx --no-build`

## Bekannte offene Punkte
- Bei mehreren aktiven Projekt-Repositories ohne Aufgabenverknüpfung greift absichtlich der Standard-Fallback. Die fachliche Entscheidung bleibt dokumentiert und sollte bei künftigen UX-Änderungen erneut geprüft werden.
