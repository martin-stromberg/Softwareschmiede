# API-Contract – KI-Plugin-spezifische Agenten-Discovery/Auswahl (Issue 58)

## Überblick

Dieses Dokument beschreibt den internen technischen Contract für die Umsetzung von **Issue 58**:

- Discovery von Agentenpaketen/Agenten erfolgt **plugin-spezifisch**.
- Selektionsreihenfolge in der UI ist verbindlich: **KI-Plugin → Agentenpaket → Agent**.
- Das gewählte KI-Plugin wird pro Aufgabe als `Aufgabe.KiPluginPrefix` persistiert.
- Start- und Folgeprompt verwenden dieselbe Auflösungskette.

Es werden **keine neuen öffentlichen HTTP-Endpunkte** eingeführt.

## Geltungsbereich

- UI: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor(.cs)`
- Services: `PluginSelectionService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `AufgabeService`
- Domäne/Migration: `Aufgabe.KiPluginPrefix`, Migration `AddKiPluginPrefix`

> Hinweis: Das in älteren Orchestrator-Runbooks genannte Verzeichnis `src/ReceiptScanner.Api/Endpoints/` ist in diesem Repository nicht vorhanden; die bestehende HTTP-API liegt weiterhin im Diff-Bereich (`DiffController`).

## Verbindliche Auflösungskette

Für KI-Plugins gilt zur Laufzeit:

1. explizite Auswahl (`selectedKiPluginPrefix`),
2. gespeicherter Aufgabenwert (`Aufgabe.KiPluginPrefix`),
3. gespeichertes Default-Plugin (`plugins.default.DevelopmentAutomation`),
4. deterministischer Fallback (`PluginSelectionService`, Copilot-Provider bevorzugt).

## Discovery- und Kompatibilitätsvertrag

1. Verfügbare KI-Plugins werden über `PluginManager.GetDevelopmentAutomationPlugins()` geladen.
2. Für jedes Agentenpaket wird plugin-spezifisch `GetAvailableAgentsAsync(packagePath)` aufgerufen.
3. Nur Pakete mit mindestens einem verfügbaren Agenten werden angezeigt.
4. Beim Prozessstart erfolgt zusätzlich ein Preflight per `IsAgentPackageCompatibleAsync(...)`.
5. Bei Inkompatibilität wird vor `CloneRepositoryAsync(...)` mit `InvalidOperationException` abgebrochen.

## Persistenzvertrag

- Entität `Aufgabe` enthält `KiPluginPrefix` (nullable, rückwärtskompatibel).
- `AufgabeService.UpdateAsync(...)` persistiert `AgentenpaketName`, `AgentenName` und `KiPluginPrefix` gemeinsam.
- Migration: `20260524151645_202605241703_AddKiPluginPrefix`.

## Start-/Prompt-Flow-Vertrag

- `AufgabeDetail.ProzessStartenAsync(...)` speichert Auswahl und übergibt `selectedKiPluginPrefix` an `EntwicklungsprozessService.ProzessStartenAsync(...)`.
- `AufgabeDetail.KiStartenAsync(...)` übergibt `selectedKiPluginPrefix` an `KiAusfuehrungsService.StartKiLauf(...)`.
- `KiAusfuehrungsService` reicht den Prefix unverändert an `EntwicklungsprozessService.KiStartenAsync(...)` weiter.

## UI-Validierung und Fehlerzustände

- Start/Senden ist deaktiviert, wenn Auswahl nicht vollständig/gültig ist.
- Meldungen:
  - „Kein KI-Plugin verfügbar.“
  - „Für das gewählte KI-Plugin sind keine kompatiblen Agentenpakete verfügbar.“
  - „Für das gewählte Agentenpaket sind keine kompatiblen Agenten verfügbar.“
- Bei Pluginwechsel werden Paket/Agent zurückgesetzt.
- Bei Paketwechsel wird Agent zurückgesetzt.

## Testbezug (bestehende Absicherung)

- `PluginSelectionServiceTests`
  - explizite Auswahl
  - gespeicherter Default
  - Fallback-Präferenz
- `EntwicklungsprozessServiceTests`
  - Inkompatibles Paket bricht Start vor Clone ab
  - Prefix-Auflösung: Aufgabenwert vs. expliziter Wert
- `AufgabeDetailFolgePromptTests`
  - Reihenfolge Plugin → Paket → Agent
  - Prefix-Vorbelegung und Weitergabe
- `AufgabeServiceTests`
  - Persistenz von `KiPluginPrefix` in `UpdateAsync`

## Verknüpfte Dokumentation

- [Flow: KI-Plugin-spezifische Agenten-Discovery/Auswahl](../flows/ki-plugin-spezifische-agenten-discovery-auswahl-flow.md)
- [F026 – KI-Plugin-spezifische Agenten-Discovery und -Auswahl](../business/features/F026-ki-plugin-spezifische-agenten-discovery-auswahl.md)
- [HTTP-Endpunkte](./http-endpoints.md)
