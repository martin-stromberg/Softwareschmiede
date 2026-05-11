# API-Dokumentation – Softwareschmiede

Technische Dokumentation der öffentlichen Schnittstellen und Plugin-APIs der Softwareschmiede.

---

## Dokumentierte Schnittstellen

| Dokument | Beschreibung |
|---|---|
| [plugin-interfaces.md](./plugin-interfaces.md) | Plugin-Entwickler-Dokumentation: `IPlugin`, `IGitPlugin`, `IKiPlugin`, `PluginType` und `PluginManager` – Schnittstellenreferenz, Discovery/DI und Implementierungsanleitungen |
| [workdir-configuration.md](./workdir-configuration.md) | Technische Dokumentation des Features „konfigurierbares Arbeitsverzeichnis“ (Settings, Resolver, Fallback, Klonpfadbildung) |
| [http-endpoints.md](./http-endpoints.md) | Aktueller Stand der HTTP-Schnittstellen (keine öffentlichen REST-Endpoints) inkl. interner technischer Contracts zu KI-Protokollformat und Rendering |

## Feature-Hinweis: Kontextsteuerung bei Folgeanweisungen

- **Kein API-Impact auf HTTP-Ebene:** Für das implementierte Feature wurden keine öffentlichen REST-/Minimal-API-Endpunkte ergänzt oder geändert.
- **Interner Contract-Impact (Application-Schicht):** Der Folgeanweisungsfluss nutzt `FolgeanweisungsKontextmodus` (`KontextMitgeben`, `KontextIgnorieren`, `KontextNeuBeginnen`) beim Start eines KI-Laufs.
- **Plugin-Contract bleibt stabil:** `IKiPlugin.StartDevelopmentAsync(...)` wurde nicht erweitert; die Kontextsteuerung wird vor dem Plugin-Aufruf im Prompt-Building verarbeitet.
- Details zum HTTP-Status und zur technischen Nachvollziehbarkeit: [http-endpoints.md](./http-endpoints.md#feature-impact-kontextsteuerung-bei-folgeanweisungen)
- Details zum unveränderten KI-Plugin-Contract: [plugin-interfaces.md](./plugin-interfaces.md#startdevelopmentasync)

### Referenzdokumente (ohne Inhaltsduplikation)

- Anforderungen: [kontextsteuerung-folgeanweisungen-requirements-analysis.md](../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md)
- Architektur: [kontextsteuerung-folgeanweisungen-architecture-blueprint.md](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md)
- Architecture Review: [kontextsteuerung-folgeanweisungen-architecture-review.md](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md)
- Testplan: [testplan-kontextsteuerung-folgeanweisungen.md](../tests/testplan-kontextsteuerung-folgeanweisungen.md)
- Testlücken: [testluecken-kontextsteuerung-folgeanweisungen.md](../tests/testluecken-kontextsteuerung-folgeanweisungen.md)

---

## Feature-Hinweis: KI-Arbeitsprotokoll als Markdown

- **Kein API-Impact auf HTTP-Ebene:** Es wurden keine öffentlichen REST-/Minimal-API-Endpunkte ergänzt oder geändert.
- **Interner technischer Contract-Impact:** KI-Antworten werden im Protokoll konsistent als Markdown persistiert (`# {Datum}`, `## Schritt n`, inkl. `RunId`) und in der UI als Markdown gerendert.
- **Rendering-Sicherheitscontract:** Die Webausgabe verwendet Markdown-Rendering mit Sanitizing (`DisableHtml`, Entfernen von Event-Handler-Attributen, Neutralisieren unsicherer URI-Schemata) sowie einen `<pre>`-Fallback bei Fehlern oder leerem Ergebnis.
- Technische Details: [http-endpoints.md](./http-endpoints.md#feature-impact-ki-arbeitsprotokoll-als-markdown-und-sichere-render-pipeline)
- Flow-Referenz: [development-process-flow.md – Ablauf 2: KI-Streaming und Protokollierung](../flows/development-process-flow.md#ablauf-2-ki-streaming-und-protokollierung)
- Detaillierter Rendering-Flow: [ki-arbeitsprotokoll-rendering-flow.md](../flows/ki-arbeitsprotokoll-rendering-flow.md)
- Fachliche Einordnung: [F005 – Aufgabenprotokoll](../business/features/F005-aufgabenprotokoll.md)

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
