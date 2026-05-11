# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und Plugin-APIs der Softwareschmiede.

---

## Dokumentierte Schnittstellen

| Dokument | Beschreibung |
|---|---|
| [plugin-interfaces.md](./plugin-interfaces.md) | Plugin-Entwickler-Dokumentation: `IPlugin`, `IGitPlugin`, `IKiPlugin`, `PluginType` und `PluginManager` – Schnittstellenreferenz, Discovery/DI und Implementierungsanleitungen |
| [workdir-configuration.md](./workdir-configuration.md) | Technische Dokumentation des Features „konfigurierbares Arbeitsverzeichnis“ (Settings, Resolver, Fallback, Klonpfadbildung) |
| [http-endpoints.md](./http-endpoints.md) | Aktueller Stand der HTTP-Schnittstellen (keine öffentlichen REST-Endpoints, Blazor-Razor-Host) |

## Feature-Hinweis: Agent-Auswahl bei Folgeanweisungen

- **Kein API-Impact auf HTTP-Ebene:** Für das Feature wurden keine öffentlichen REST-/Minimal-API-Endpunkte ergänzt oder geändert.
- Details zum HTTP-Status und zur AC-Nachvollziehbarkeit: [http-endpoints.md](./http-endpoints.md#feature-impact-agent-auswahl-bei-folgeanweisungen)
- Details zum unveränderten KI-Plugin-Contract (`IKiPlugin.StartDevelopmentAsync`): [plugin-interfaces.md](./plugin-interfaces.md#startdevelopmentasync)

---

## Überblick Plugin-System

Die Softwareschmiede verwendet ein Plugin-System mit zwei fachlichen Schnittstellen und einer gemeinsamen Basis:

- **`IPlugin`** – Gemeinsame Metadaten (`PluginName`, `PluginPrefix`, `PluginType`) und konfigurierbare Settings.
- **`IGitPlugin`** – Kapselt alle Git-Operationen (Issues laden, Repository klonen, Branches verwalten, Pull Requests erstellen, …). Referenzimplementierung: `GitHubPlugin`.
- **`IKiPlugin`** – Kapselt die KI-Integration (Agenten verwalten, Entwicklung starten, Tests ausführen, …). Referenzimplementierung: `GitHubCopilotPlugin`.

`IPluginManager` wird als **Singleton** registriert und lädt Plugins dynamisch aus dem `plugins`-Ordner.  
`IGitPlugin` und `IKiPlugin` werden als **Scoped** aus dem Default-Plugin des `PluginManager` aufgelöst.

## Verknüpfte Dokumentation

- Ablaufdiagramm: [Plugin-Discovery und Laden](../flows/plugin-discovery-load-flow.md)
- Fachliche Sicht: [F010 – Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
- Testabdeckung: [Testplan Plugin-Klassenbibliotheken](../tests/testplan-plugin-klassenbibliotheken-github-und-copilot.md)
- HTTP-Schnittstellenstatus: [HTTP-Endpunkte](./http-endpoints.md)

Weitere Informationen: [plugin-interfaces.md](./plugin-interfaces.md)
