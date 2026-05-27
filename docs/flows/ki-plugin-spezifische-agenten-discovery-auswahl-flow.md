# Ablauf – KI-Plugin-spezifische Agenten-Discovery/Auswahl (Issue 58)

## Titel & Kontext

Dieser Ablauf beschreibt die plugin-spezifische Agenten-Discovery in `AufgabeDetail` beim Aufgabenstart und bei Folgeprompts.  
Sollzustand: **KI-Plugin ist Pflicht**, **Agentenpaket und Agent sind optional**. Die effektive KI-Plugin-Auswahl wird als `KiPluginPrefix` pro Aufgabe gespeichert und in Start-/Folgepfaden konsistent genutzt.

## Diagramm A – Sequenz: Auswahl, Persistenz und Start/Folgeprompt

```mermaid
sequenceDiagram
    actor U as Nutzer
    participant UI as AufgabeDetail
    participant PS as PluginSelectionService
    participant APS as AgentPackageService
    participant KI as IKiPlugin
    participant AS as AufgabeService
    participant EPS as EntwicklungsprozessService
    participant KAS as KiAusfuehrungsService

    U->>UI: Aufgabe öffnen
    UI->>PS: ResolveDevelopmentAutomationPluginAsync(selected/task/default)
    PS-->>UI: effektives KI-Plugin
    UI->>APS: GetPackagesAsync()
    loop je Paket
        UI->>KI: GetAvailableAgentsAsync(packagePath)
        KI-->>UI: Agentenliste (evtl. leer)
    end
    UI-->>U: KI-Plugin gewählt, Paket/Agent optional

    U->>UI: Prozess starten
    UI->>AS: UpdateAsync(..., AgentenpaketName?, AgentenName?, KiPluginPrefix)
    UI->>EPS: ProzessStartenAsync(..., selectedKiPluginPrefix)
    opt Agentenpaket gesetzt
        EPS->>KI: IsAgentPackageCompatibleAsync(packagePath)
        KI-->>EPS: true/false
    end
    EPS-->>UI: Start ok / Fehler

    U->>UI: Folgeprompt senden
    UI->>KAS: StartKiLauf(..., selectedKiPluginPrefix)
    KAS->>EPS: KiStartenAsync(..., selectedKiPluginPrefix)
    EPS->>PS: ResolveDevelopmentAutomationPluginAsync(...)
    EPS->>KI: StartDevelopmentAsync(...)
```

## Diagramm B – Programmablauf: Pflicht-/Optional-Logik und Resets

```mermaid
flowchart TD
    A([UI lädt Aufgabe]) --> B{KI-Plugins verfügbar?}
    B -- Nein --> B1[Fehlerzustand: Kein KI-Plugin verfügbar]
    B1 --> Z[Start/Senden deaktiviert]
    B -- Ja --> C[Effektives KI-Plugin auflösen]
    C --> D[Kompatible Agentenpakete laden]
    D --> E{Pluginwechsel?}
    E -- Ja --> E1[Paket + Agent zurücksetzen]
    E1 --> D
    E -- Nein --> F{Agentenpaket gewählt?}
    F -- Nein --> G[Start/Senden aktiv mit Plugin-Default]
    F -- Ja --> H[Agenten für Paket laden]
    H --> I{Agent gewählt?}
    I -- Nein --> J[Start/Senden aktiv ohne --agent]
    I -- Ja --> K[Start/Senden aktiv mit Agent]
    G --> L[Auswahl persistieren und Lauf starten]
    J --> L
    K --> L
```

## Schrittbeschreibung

1. **Pflichtfeld KI-Plugin auflösen**  
   - **Code:** `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`LadeKiPluginsAsync`), `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` (`ResolveDevelopmentAutomationPluginAsync`)  
   - **Eingaben:** `_selectedKiPluginPrefix`, gespeicherter `Aufgabe.KiPluginPrefix`, verfügbare Plugins  
   - **Ausgaben/Seiteneffekte:** Effektiver `PluginPrefix` wird gesetzt; bei fehlenden KI-Plugins bleiben Start/Senden deaktiviert.

2. **Plugin-spezifische Paket-/Agenten-Discovery laden**  
   - **Code:** `AufgabeDetail.razor.cs` (`LadeAgentenpaketeAsync`)  
   - **Eingaben:** Effektives `IKiPlugin`, `AgentPackageService.GetPackagesAsync()`  
   - **Ausgaben/Seiteneffekte:** Es werden nur Pakete mit mindestens einem kompatiblen Agenten angeboten; leere Auswahl ist zulässig.

3. **Reset-Regeln bei Auswahländerungen anwenden**  
   - **Code:** `AufgabeDetail.razor.cs` (`KiPluginGeaendertAsync`, `PaketGeaendertAsync`)  
   - **Eingaben:** UI-Änderung von Plugin oder Paket  
   - **Ausgaben/Seiteneffekte:** Pluginwechsel setzt Paket+Agent zurück; Paketwechsel setzt Agent zurück.

4. **Auswahl vor Prozessstart persistieren (optional für Paket/Agent)**  
   - **Code:** `AufgabeDetail.razor.cs` (`ProzessStartenAsync`), `src/Softwareschmiede/Application/Services/AufgabeService.cs` (`UpdateAsync`)  
   - **Eingaben:** `AgentenpaketName?`, `AgentenName?`, `KiPluginPrefix`  
   - **Ausgaben/Seiteneffekte:** `KiPluginPrefix` wird immer gespeichert; Paket/Agent werden bei leerer UI-Auswahl als `null` persistiert.

5. **Startlauf mit optionalem Agentenpaket ausführen**  
   - **Code:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`ProzessStartenAsync`)  
   - **Eingaben:** `selectedKiPluginPrefix`, optionales `Aufgabe.AgentenpaketName`  
   - **Ausgaben/Seiteneffekte:** Kompatibilitätscheck/Deploy nur bei gesetztem Paket; ansonsten Start ohne Paketdeployment.

6. **Folgeprompt mit derselben Plugin-Auflösung starten**  
   - **Code:** `AufgabeDetail.razor.cs` (`KiMitPromptStartenAsync`), `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (`StartKiLauf`), `EntwicklungsprozessService.cs` (`KiStartenAsync`)  
   - **Eingaben:** Prompt, optionaler Agent, `selectedKiPluginPrefix`  
   - **Ausgaben/Seiteneffekte:** Bei leerem Agentnamen wird `AgentInfo(string.Empty, ...)` verwendet; Plugin startet ohne `--agent`.

## Fehlerbehandlung

- **Kein KI-Plugin verfügbar**  
  - **Pfad:** `AufgabeDetail.IsAgentenauswahlGueltig`, `ProzessStartenAsync`, `KiStartenAsync`  
  - **Behandlung:** UI blockiert Start/Senden; Fehlerhinweis „Kein KI-Plugin verfügbar“.

- **Ungültiger/fehlender gespeicherter PluginPrefix**  
  - **Pfad:** `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync`  
  - **Behandlung:** Fallback-Kette `explizit → gespeichertes Default → priorisierter Fallback`; Warn-Log bei ungültigem gespeichertem Prefix.

- **Inkompatibles Agentenpaket**  
  - **Pfad:** `EntwicklungsprozessService.ProzessStartenAsync` (`IsAgentPackageCompatibleAsync`)  
  - **Behandlung:** `InvalidOperationException`; Start wird vor Deployment abgebrochen.

- **Leere Agentenliste für Paket**  
  - **Pfad:** `AufgabeDetail.LadeAgentenpaketeAsync`  
  - **Behandlung:** Paket wird nicht in `_agentenpakete` übernommen; Lauf bleibt ohne Paket/Agent möglich.

## Abhängigkeiten

- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

## Verwandte Dokumentation

- [Entwicklungsprozess-Abläufe](./development-process-flow.md)
- [Kontextsteuerung bei Folgeanweisungen](./follow-up-context-steering-flow.md)
- [API-Contract](../api/ki-plugin-spezifische-agenten-discovery-auswahl.md)
