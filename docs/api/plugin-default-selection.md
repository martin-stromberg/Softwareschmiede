# Standardplugin je Pluginart & KI-Plugin-Auswahl

## Übersicht

Dieses Dokument beschreibt den internen API-Contract für:
- Speicherung eines **Standardplugins** je **Pluginart**
- **KI-Plugin-Auswahl** beim Prompt-Start
- projektspezifische/aufgabenbezogene Auflösung des `IGitPlugin` in `AufgabeDetail` und `GitOrchestrationService`
- Auflösung des effektiven Plugins mit robustem **Fallback**

Es handelt sich um einen Application-/Service-Contract, nicht um einen HTTP-Endpoint-Contract.

## Technische Komponenten

### `PluginDefaultSettingsService`

- Persistiert pro Pluginart den Standardwert in `AppEinstellungen`.
- Schlüssel:
  - `plugins.default.SourceCodeManagement`
  - `plugins.default.DevelopmentAutomation`
- Wert: `PluginPrefix` (technische ID des Plugins)
- Leerer/Whitespace-Wert wird als `null` gespeichert.

### `PluginSelectionService`

- Verantwortlich für die Auflösung des effektiven Plugins.
- Zentrale Methoden:
  - `ResolveSourceCodeManagementPluginAsync(...)`
  - `ResolveDevelopmentAutomationPluginAsync(...)`
  - `GetStoredDefaultPluginPrefixAsync(...)`
  - `SaveDefaultPluginPrefixAsync(...)`

## Explizites Mapping der Auflösung

### Reihenfolge (verbindlich)

1. Explizite Auswahl (z. B. `selectedKiPluginPrefix`)
2. Gespeicherter Aufgabenwert (`Aufgabe.KiPluginPrefix`)
3. Gespeichertes Standardplugin der Pluginart
4. Fallback

### Entscheidungsmatrix

| Explizite Auswahl | Aufgaben-Prefix | Gespeicherter Standard | Verfügbare Plugins | Ergebnis |
|---|---|---|---|---|
| gültig und vorhanden | beliebig | beliebig | mindestens 1 | explizite Auswahl |
| leer/ungültig | gültig und vorhanden | beliebig | mindestens 1 | Aufgaben-Prefix |
| leer/ungültig | leer/ungültig | gültig und vorhanden | mindestens 1 | gespeichertes Standardplugin |
| leer/ungültig | leer/ungültig | leer/ungültig/nicht mehr vorhanden | mindestens 1 | Fallback aus verfügbarer Liste |
| beliebig | beliebig | beliebig | 0 | `PluginManager`-DefaultResolver |

### Fallback-Verhalten (KI-Plugins)

- Bei verfügbarer Liste wird nach einem stabilen Sortierschlüssel aufgelöst.
- KI-Plugins mit Provider-Präfix `copilot` werden im Fallback bevorzugt.
- Falls die Liste leer ist, nutzt der Service den `PluginManager`-DefaultResolver.

## KI-Plugin-Auswahl beim Prompt

1. Aufgaben-Detailseite lädt verfügbare KI-Plugins und setzt die Vorauswahl auf das aktuell aufgelöste Plugin (explizit/Task/Default/Fallback).
2. Beim Prompt-Senden wird `selectedKiPluginPrefix` an den KI-Lauf übergeben.
3. `EntwicklungsprozessService.KiStartenAsync(...)` löst darüber das effektive KI-Plugin auf; ohne expliziten Wert wird `Aufgabe.KiPluginPrefix` berücksichtigt.
4. Das Protokoll enthält das tatsächlich verwendete KI-Plugin (`PluginName`, `PluginPrefix`).

## SCM-Plugin-Auflösung für `AufgabeDetail`/`GitOrchestrationService`

### Auswahlregel (verbindlich)

1. Aufgaben-Repository (`Aufgabe.GitRepository.PluginTyp`) *(required, falls gesetzt)*.
2. Projekt-Repository (`Projekt.Repositories`) bei fehlender Aufgaben-Verknüpfung:
   - genau **ein** aktives Repository mit `PluginTyp` *(required für diese Stufe)*.
3. Standard-/Fallback-Auflösung über `PluginSelectionService.ResolveSourceCodeManagementPluginAsync(null, ...)`.

Damit gilt: **projekt-/aufgabenbezogene Pluginkonfiguration hat Vorrang vor dem globalen Default**.

### Fallback bei fehlender Repository-Verknüpfung

- Hat eine Aufgabe keine direkte Repository-Verknüpfung, wird das Projekt betrachtet.
- Gibt es dort genau **ein aktives** Repository, wird dessen `PluginTyp` verwendet.
- Bei **mehreren** aktiven Repositories wird keine implizite Auswahl getroffen; es erfolgt der Fallback auf das konfigurierte Standardplugin.

### LocalDirectory-/lokales Repository-Verhalten

- Wenn die Auflösung auf `LocalDirectoryPlugin` fällt, laufen Git-Aktionen über den lokalen Contract (kein Remote-Merge bei Pull).
- Für den lokalen Arbeitskopie-Fall (`RepositoryKind.LocalDirectory` + `IsWorkingDirectoryCopy=true`) steuert die UI die Aktionsmatrix capability-basiert: Push/Pull/PR ausblenden, Merge einblenden.
- Details: [local-directory-plugin.md](./local-directory-plugin.md)

### Testnachweise

- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
  - `CommitAsync_ShouldUseSelectedPlugin_WhenTaskRepositoryContainsTrimmedLowercasePluginType`
  - `PullAsync_ShouldUseLocalDirectoryPlugin_WhenTaskHasNoLinkedRepositoryAndProjectHasSingleActiveRepository`
  - `CommitAsync_ShouldUseDefaultPlugin_WhenProjectRepositorySelectionIsAmbiguous`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`
  - `AufgabeDetail_ShouldUseProjectSelectedGitPlugin_InInjectedGitOrchestrationService`
  - `AufgabeDetail_ShouldUseLocalDirectoryPlugin_WhenSelectedPluginIsLocalAndDefaultIsGitHub`
  - `AufgabeDetail_ShouldInvokePullOnProjectSelectedGitPlugin`

## Fehler- und Kompatibilitätsverhalten

- Nicht mehr verfügbare gespeicherte Standardplugins brechen den Lauf nicht ab.
- In diesem Fall wird ein Warn-Log geschrieben und der Fallback verwendet.
- Das Verhalten ist rückwärtskompatibel: ohne gespeicherten Standard bleibt die bisherige Fallback-Logik aktiv.

## Verknüpfte Dokumentation

- HTTP-Status (keine öffentlichen Endpunkte): [http-endpoints.md](./http-endpoints.md)
- Plugin-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- Flow: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)
- Business: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
