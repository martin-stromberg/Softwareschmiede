# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und internen API-Contracts.

## API-Status (HTTP)

Die Softwareschmiede stellt aktuell **keine öffentlichen HTTP-Endpunkte** bereit (keine REST- und keine Minimal-API-Routen).  
Details: [http-endpoints.md](./http-endpoints.md)

## Dokumentierte API-Bereiche

| Dokument | Kurzbeschreibung |
|---|---|
| [http-endpoints.md](./http-endpoints.md) | Verbindlicher HTTP-Status: keine öffentlichen Endpunkte, inkl. einheitlicher Dokumentation für Request/Response = *nicht anwendbar*. |
| [issue-branch-pr-linking.md](./issue-branch-pr-linking.md) | Interner Contract für Issue-Auswahl, issuebezogene Branch-Erzeugung und PR-Closing-Direktive (`Closes #<Issue>`). |
| [live-project-browser-git-status.md](./live-project-browser-git-status.md) | Technischer Contract für den lokalen Repository-Browser auf der Aufgabenseite inkl. Snapshot, Tree-/Listenansicht, Dateivorschau und defensiver Git-Status-Auswertung. |
| [local-directory-plugin.md](./local-directory-plugin.md) | Technischer Contract der `IGitPlugin`-Implementierung `LocalDirectoryPlugin` inkl. Workspace-Modi, `git-init`-Fallback, Pull ohne Merge, Push-/Delete-Sync sowie Capability-Flags für die Aktionsmatrix (Push/Pull/PR ausblenden, Merge einblenden bei Arbeitskopie). |
| [plugin-default-selection.md](./plugin-default-selection.md) | Interner API-Contract für **Standardplugin** je **Pluginart**, **KI-Plugin-Auswahl** sowie die projektspezifische/aufgabenbezogene `IGitPlugin`-Auflösung (vor Default) inkl. Fallback bei fehlender Repo-Verknüpfung. |
| [plugin-interfaces.md](./plugin-interfaces.md) | Schnittstellenreferenz für `IPlugin`, `IGitPlugin`, `IKiPlugin`, `PluginType`, Plugin-Discovery sowie dynamische Repository-Feldschemata (`GetRepositoryLinkFields`). |
| [repository-startskript-freier-port.md](./repository-startskript-freier-port.md) | Interner Contract für repositorybezogene Startskripte mit freier Portreservierung, Persistenz (`RepositoryStartKonfiguration`) und Ausführung beim Prozessstart. |
| [start-ps1-visual-studio-freier-http-port.md](./start-ps1-visual-studio-freier-http-port.md) | Skriptvertrag für `start.ps1`: parameterloser Aufruf, autonome Mehrprojekt-Portzuweisung, Exit-Codes und VS-kompatibler Host-Fallback auf `localhost`. |
| [workdir-configuration.md](./workdir-configuration.md) | Interner Contract für Arbeitsverzeichnis-Auflösung und Laufzeit-**Fallback** beim Klonpfad. |

## Feature-Fokus: Standardplugin je Pluginart & KI-Plugin-Auswahl

- Technischer Contract: [plugin-default-selection.md](./plugin-default-selection.md)
- Fachliche Einordnung: [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](../business/features/F014-standardplugin-ki-plugin-auswahl.md)
- Ablaufdarstellung: [plugin-default-selection-flow.md](../flows/plugin-default-selection-flow.md)

## Feature-Fokus: Lokales Verzeichnis Plugin

- Technischer Contract: [local-directory-plugin.md](./local-directory-plugin.md)
- Schnittstellen-Referenz: [plugin-interfaces.md](./plugin-interfaces.md)
- Ablaufdarstellung: [local-directory-plugin-flow.md](../flows/local-directory-plugin-flow.md)

## Feature-Fokus: Live Project Browser mit Git-Status

- Technischer Contract: [live-project-browser-git-status.md](./live-project-browser-git-status.md)
- Ablaufdarstellung: [live-project-browser-git-status-flow.md](../flows/live-project-browser-git-status-flow.md)
- Fachliche Einordnung: [F021 – Live Project Browser mit Git-Status](../business/features/F021-live-project-browser-git-status.md)

## Feature-Fokus: Issue-, Branch- und PR-Verknüpfung

- Technischer Contract: [issue-branch-pr-linking.md](./issue-branch-pr-linking.md)
- Ablaufdarstellung: [issue-branch-pr-linking-flow.md](../flows/issue-branch-pr-linking-flow.md)
- Fachliche Einordnung: [F019 – Issue-, Branch- und PR-Verknüpfung](../business/features/F019-issue-branch-pr-verknuepfung.md)

## Feature-Fokus: Repository-Startskript mit freier Portzuweisung

- Technischer Contract: [repository-startskript-freier-port.md](./repository-startskript-freier-port.md)
- Skriptvertrag für VS-Debug: [start-ps1-visual-studio-freier-http-port.md](./start-ps1-visual-studio-freier-http-port.md)
- Ablaufdarstellung: [repository-startskript-freier-port-flow.md](../flows/repository-startskript-freier-port-flow.md)
- Fachliche Einordnung: [F020 – Repository-Startskript mit freier Portzuweisung](../business/features/F020-repository-startskript-freier-port.md)

## Verknüpfte Dokumentation

- Flows-Index: [docs/flows/README.md](../flows/README.md)
- Feature-Index: [docs/business/features.md](../business/features.md)
- Plugin-Prinzip (Business): [F010 – Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
