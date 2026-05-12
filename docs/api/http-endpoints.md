# HTTP-Endpunkte – Softwareschmiede

## Übersicht

Die Softwareschmiede stellt derzeit **keine öffentlichen HTTP-Endpunkte** bereit.  
Die Anwendung wird über Razor Components bereitgestellt (`app.MapRazorComponents<App>()` in `src/Softwareschmiede/Program.cs`).

## Endpoint-Status (einheitlich)

Da es keine öffentlichen API-Routen gibt, gelten die üblichen Endpoint-Bestandteile aktuell als **nicht anwendbar**:

1. **HTTP-Methode & Pfad:** nicht vorhanden
2. **Authentifizierung:** nicht anwendbar (kein öffentlicher HTTP-Zugriffspunkt)
3. **Request:** keine Header/Parameter/Bodies für öffentliche Endpunkte
4. **Response:** keine öffentlichen HTTP-Statuscodes für API-Calls
5. **curl-Beispiel:** entfällt, da kein aufrufbarer Endpoint existiert

## Was stattdessen dokumentiert ist

- Interne Plugin-Contracts: [plugin-interfaces.md](./plugin-interfaces.md)
- Interner Contract für **Standardplugin** je **Pluginart**, **KI-Plugin-Auswahl** und **Fallback**: [plugin-default-selection.md](./plugin-default-selection.md)
- Interner Contract für Workdir-**Fallback**: [workdir-configuration.md](./workdir-configuration.md)

## Explizites Mapping (Feature F014)

Das Feature „Standardplugin je Pluginart & KI-Plugin-Auswahl“ ist ein **interner Application-Contract** und **kein HTTP-Contract**.

| Thema | Abbildung |
|---|---|
| Standardplugin je Pluginart speichern | `PluginDefaultSettingsService` persistiert `plugins.default.SourceCodeManagement` und `plugins.default.DevelopmentAutomation` |
| KI-Plugin-Auswahl beim Prompt | `selectedKiPluginPrefix` wird von der Aufgaben-Detailseite an `KiAusfuehrungsService` und `EntwicklungsprozessService` weitergereicht |
| Auflösung inkl. Fallback | `PluginSelectionService`: explizite Auswahl → gespeicherter Default → Fallback |

## Verknüpfte Dokumentation

- Business: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
- Flow: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)
- Flow-Index: [docs/flows/README.md](../flows/README.md)
