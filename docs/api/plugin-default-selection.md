# Standardplugin je Pluginart & KI-Plugin-Auswahl

## Übersicht

Dieses Dokument beschreibt den internen API-Contract für:
- Speicherung eines **Standardplugins** je **Pluginart**
- **KI-Plugin-Auswahl** beim Prompt-Start
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
2. Gespeichertes Standardplugin der Pluginart
3. Fallback

### Entscheidungsmatrix

| Explizite Auswahl | Gespeicherter Standard | Verfügbare Plugins | Ergebnis |
|---|---|---|---|
| gültig und vorhanden | beliebig | mindestens 1 | explizite Auswahl |
| leer/ungültig | gültig und vorhanden | mindestens 1 | gespeichertes Standardplugin |
| leer/ungültig | leer/ungültig/nicht mehr vorhanden | mindestens 1 | Fallback aus verfügbarer Liste |
| beliebig | beliebig | 0 | `PluginManager`-DefaultResolver |

### Fallback-Verhalten (KI-Plugins)

- Bei verfügbarer Liste wird nach einem stabilen Sortierschlüssel aufgelöst.
- KI-Plugins mit Provider-Präfix `copilot` werden im Fallback bevorzugt.
- Falls die Liste leer ist, nutzt der Service den `PluginManager`-DefaultResolver.

## KI-Plugin-Auswahl beim Prompt

1. Aufgaben-Detailseite lädt verfügbare KI-Plugins und setzt die Vorauswahl auf das aktuell aufgelöste Standardplugin.
2. Beim Prompt-Senden wird `selectedKiPluginPrefix` an den KI-Lauf übergeben.
3. `EntwicklungsprozessService.KiStartenAsync(...)` löst darüber das effektive KI-Plugin auf.
4. Das Protokoll enthält das tatsächlich verwendete KI-Plugin (`PluginName`, `PluginPrefix`).

## Fehler- und Kompatibilitätsverhalten

- Nicht mehr verfügbare gespeicherte Standardplugins brechen den Lauf nicht ab.
- In diesem Fall wird ein Warn-Log geschrieben und der Fallback verwendet.
- Das Verhalten ist rückwärtskompatibel: ohne gespeicherten Standard bleibt die bisherige Fallback-Logik aktiv.

## Verknüpfte Dokumentation

- HTTP-Status (keine öffentlichen Endpunkte): [http-endpoints.md](./http-endpoints.md)
- Plugin-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- Flow: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)
- Business: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
