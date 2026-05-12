# Entity-Relationship-Modell – Standardplugin je Pluginart & KI-Plugin-Auswahl

## Modellierungsansatz
Minimal-invasiv mit bestehender Persistenz (`AppEinstellungen`), plus klaren Integritätsregeln.

## Entitäten
### 1) AppEinstellung (bestehend)
- `Id` (PK)
- `Schluessel` (UK)
- `Wert`
- `AktualisiertAm`

Verwendung für Defaults:
- `plugins.default.SourceCodeManagement` -> `<PluginIdentifier>`
- `plugins.default.DevelopmentAutomation` -> `<PluginIdentifier>`

### 2) PluginKonfiguration / Plugin-Registry (bestehend)
- Stellt verfügbare Plugins und deren technische Identität bereit.
- Muss eine stabile ID liefern (z. B. Prefix).

### 3) PromptDispatch (fachliches Laufzeitobjekt)
- `AufgabeId`
- `Prompt`
- `SelectedKiPluginIdentifier`
- `Timestamp`

## Beziehungen / Kardinalitäten
- Eine Pluginart hat **maximal ein** gespeichertes Default (`0..1`) in `AppEinstellung`.
- Ein PromptDispatch referenziert **genau ein** KI-Plugin (nach Auflösung).
- Ein KI-Plugin kann in **vielen** PromptDispatches genutzt werden.

## Integritätsregeln
1. Default pro Pluginart nur auf aktuell verfügbares Plugin der passenden Pluginart.
2. Persistiert wird technische ID, nicht Anzeigename.
3. Bei ungültiger gespeicherter ID erfolgt Fallback statt Fehlerabbruch.

## Alternative (optional, normalisiert)
Separate Tabelle `PluginDefaults(PluginType PK, PluginIdentifier, UpdatedAt)` ist möglich, aber für das Feature nicht zwingend erforderlich.
