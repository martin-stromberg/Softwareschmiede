# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und internen API-Contracts.

## API-Status (HTTP)

Die Softwareschmiede stellt aktuell **keine öffentlichen HTTP-Endpunkte** bereit (keine REST- und keine Minimal-API-Routen).  
Details: [http-endpoints.md](./http-endpoints.md)

## Dokumentierte API-Bereiche

| Dokument | Kurzbeschreibung |
|---|---|
| [http-endpoints.md](./http-endpoints.md) | Verbindlicher HTTP-Status: keine öffentlichen Endpunkte, inkl. einheitlicher Dokumentation für Request/Response = *nicht anwendbar*. |
| [local-directory-plugin.md](./local-directory-plugin.md) | Technischer Contract der `IGitPlugin`-Implementierung `LocalDirectoryPlugin` inkl. Workspace-Modi, Settings, Guardrails und Support-Matrix der Operationen. |
| [plugin-default-selection.md](./plugin-default-selection.md) | Interner API-Contract für **Standardplugin** je **Pluginart**, **KI-Plugin-Auswahl** beim Prompt und **Fallback**-Regeln. |
| [plugin-interfaces.md](./plugin-interfaces.md) | Schnittstellenreferenz für `IPlugin`, `IGitPlugin`, `IKiPlugin`, `PluginType`, Plugin-Discovery sowie dynamische Repository-Feldschemata (`GetRepositoryLinkFields`). |
| [workdir-configuration.md](./workdir-configuration.md) | Interner Contract für Arbeitsverzeichnis-Auflösung und Laufzeit-**Fallback** beim Klonpfad. |

## Feature-Fokus: Standardplugin je Pluginart & KI-Plugin-Auswahl

- Technischer Contract: [plugin-default-selection.md](./plugin-default-selection.md)
- Fachliche Einordnung: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
- Ablaufdarstellung: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)

## Feature-Fokus: Lokales Verzeichnis Plugin

- Technischer Contract: [local-directory-plugin.md](./local-directory-plugin.md)
- Schnittstellen-Referenz: [plugin-interfaces.md](./plugin-interfaces.md)
- Ablaufdarstellung: [local-directory-plugin-flow.md](../flows/local-directory-plugin-flow.md)

## Verknüpfte Dokumentation

- Flows-Index: [docs/flows/README.md](../flows/README.md)
- Feature-Index: [docs/business/features.md](../business/features.md)
- Plugin-Prinzip (Business): [F010 – Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
