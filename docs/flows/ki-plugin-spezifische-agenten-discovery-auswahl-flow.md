# Ablauf – KI-Plugin-spezifische Agenten-Discovery/Auswahl (Issue 58)

## Kontext

Dieser Ablauf dokumentiert die Umsetzung von **Issue 58**:

- Discovery ist plugin-spezifisch.
- UI-Reihenfolge ist verbindlich: **KI-Plugin → Agentenpaket → Agent**.
- Start- und Folgeprompt verwenden dieselbe Plugin-Auflösung.
- `KiPluginPrefix` wird pro Aufgabe persistiert.

## Sequenzdiagramm – Auswahl, Persistenz und Start

```mermaid
sequenceDiagram
    actor U as Nutzer
    participant UI as AufgabeDetail
    participant PM as PluginManager
    participant PS as PluginSelectionService
    participant APS as AgentPackageService
    participant KI as IKiPlugin
    participant AS as AufgabeService
    participant EPS as EntwicklungsprozessService
    participant KAS as KiAusfuehrungsService

    U->>UI: Aufgabe öffnen
    UI->>PM: GetDevelopmentAutomationPlugins()
    UI->>PS: ResolveDevelopmentAutomationPluginAsync(selected/task/default)
    PS-->>UI: effektives KI-Plugin
    UI->>APS: GetPackagesAsync()
    loop je Paket
        UI->>KI: GetAvailableAgentsAsync(packagePath)
        KI-->>UI: Agentenliste (evtl. leer)
    end
    UI-->>U: nur kompatible Pakete/Agenten anzeigen

    U->>UI: Prozess starten
    UI->>AS: UpdateAsync(..., AgentenpaketName, AgentenName, KiPluginPrefix)
    UI->>EPS: ProzessStartenAsync(..., selectedKiPluginPrefix)
    EPS->>PS: ResolveDevelopmentAutomationPluginAsync(...)
    EPS->>KI: IsAgentPackageCompatibleAsync(packagePath)
    KI-->>EPS: true/false
    EPS-->>UI: Start ok / Fehler (inkompatibel)

    U->>UI: Folgeprompt senden
    UI->>KAS: StartKiLauf(..., selectedKiPluginPrefix)
    KAS->>EPS: KiStartenAsync(..., selectedKiPluginPrefix)
    EPS->>PS: ResolveDevelopmentAutomationPluginAsync(...)
    EPS->>KI: StartDevelopmentAsync(...)
```

## Entscheidungslogik – Zustände und Reset-Regeln

```mermaid
flowchart TD
    A([UI lädt Aufgabe]) --> B{KI-Plugins verfügbar?}
    B -- Nein --> B1[Fehlerzustand: Kein KI-Plugin verfügbar]
    B1 --> Z[Start/Senden deaktiviert]
    B -- Ja --> C[Plugin auflösen]
    C --> D[Agentenpakete plugin-spezifisch laden]
    D --> E{Kompatible Pakete vorhanden?}
    E -- Nein --> E1[Hinweis: keine kompatiblen Pakete]
    E1 --> Z
    E -- Ja --> F[Agenten für ausgewähltes Paket laden]
    F --> G{Kompatible Agenten vorhanden?}
    G -- Nein --> G1[Hinweis: keine kompatiblen Agenten]
    G1 --> Z
    G -- Ja --> H[Auswahl vollständig]
    H --> I[Start/Senden aktiv]
    I --> J{Pluginwechsel?}
    J -- Ja --> J1[Paket + Agent zurücksetzen]
    J1 --> D
    J -- Nein --> K{Paketwechsel?}
    K -- Ja --> K1[Agent zurücksetzen]
    K1 --> F
    K -- Nein --> L[Auswahl persistieren und Lauf starten]
```

## Technische Referenzen

- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`
- `src/Softwareschmiede/Migrations/20260524151645_202605241703_AddKiPluginPrefix.cs`

## Testbezug

- `PluginSelectionServiceTests`
- `EntwicklungsprozessServiceTests`
- `AufgabeDetailFolgePromptTests`
- `AufgabeServiceTests`

## Verwandte Dokumentation

- [API-Contract](../api/ki-plugin-spezifische-agenten-discovery-auswahl.md)
- [F026 – Business-Sicht](../business/features/F026-ki-plugin-spezifische-agenten-discovery-auswahl.md)
