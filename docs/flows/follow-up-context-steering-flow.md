# Ablauf – Kontextsteuerung bei Folgeanweisungen

## Titel & Kontext

Dieser Ablauf dokumentiert die Kontextsteuerung für Folgeanweisungen in der Aufgaben-Detailansicht.  
Der Nutzer kann pro Folgeanweisung zwischen **Kontext mitgeben**, **Kontext ignorieren** und **Kontext neu beginnen** wählen.  
Die Verarbeitung kombiniert UI-Guardrails (`AufgabeDetail`), Hintergrundlauf (`KiAusfuehrungsService`) und Prompt-/Kontextlogik in `EntwicklungsprozessService` – inklusive provider-spezifischer Dateinamen für GitHub Copilot (`*.copilot.*`) und Claude CLI (`*.claude.*`).

> Verwandte Artefakte:  
> [Requirements Kontextsteuerung](../requirements/kontextsteuerung-folgeanweisungen-requirements-analysis.md) ·
> [Architektur Kontextsteuerung](../architecture/kontextsteuerung-folgeanweisungen-architecture-blueprint.md) ·
> [Review Kontextsteuerung](../improvements/kontextsteuerung-folgeanweisungen-architecture-review.md) ·
> [Claude-CLI Testplan](../tests/testplan-claude-cli-integration.md)

---

## Diagramm A – Sequenz: Folgeanweisung mit Plugin-Auswahl und Kontextsteuerung

```mermaid
sequenceDiagram
    actor Anwender
    participant UI as AufgabeDetail.razor/.cs
    participant Runner as KiAusfuehrungsService
    participant Prozess as EntwicklungsprozessService
    participant PM as IPluginManager
    participant KI as IKiPlugin (Copilot/Claude)
    participant Datei as {id}.<provider>.context.md
    participant Log as ProtokollService

    Anwender->>UI: Folge-Prompt + Agent + Kontextmodus wählen
    UI->>UI: Neu beginnen bestätigt?
    UI->>Runner: StartKiLauf(prompt, agent, kontextmodus)
    Runner->>Prozess: KiStartenAsync(...)
    Prozess->>PM: Default-KI-Plugin (DI in Program.cs)
    PM-->>Prozess: GitHub Copilot oder Claude CLI
    Prozess->>Prozess: BuildFollowPromptWithContextAsync(...)
    Prozess->>Datei: Kontext lesen/resetten/komprimieren
    Prozess->>Log: Prompt + Kontextevent protokollieren
    Prozess->>KI: StartDevelopmentAsync(finalPrompt)
    KI-->>Prozess: Stream-Chunks / Fehler
    Prozess-->>Runner: Ausgabezeilen
    Runner-->>UI: Live-Streaming + Completion-Callback
    Prozess->>Datei: Verlaufseintrag anhängen
```

---

## Diagramm B – Programmablauf: Moduslogik inkl. Claude-/Copilot-Sichtbarkeit

```mermaid
flowchart TD
    A([FolgePromptAsync]) --> B{Modus = NeuBeginnen<br/>und unbestätigt?}
    B -- Ja -.-> B1[Abbruch mit UI-Fehlerhinweis]
    B -- Nein --> C[KiStartenAsync mit kontextmodus]
    C --> C0{Default KI-Plugin}
    C0 -- Copilot --> C1[Dateien mit Präfix copilot]
    C0 -- Claude CLI --> C2[Dateien mit Präfix claude]
    C1 --> D{Kontextmodus = KontextNeuBeginnen?}
    C2 --> D
    D -- Ja --> E[Kontextdatei atomisch resetten]
    D -- Nein --> D2{Kontextmodus = KontextIgnorieren?}
    D2 -- Ja --> F[Nur Nutzerprompt verwenden]
    D2 -- Nein --> G[Kontextdatei sicher lesen]
    G --> H{Soft-Limit überschritten?}
    H -- Ja --> I[KI-Komprimierung + speichern]
    H -- Nein --> J[Kontext unverändert verwenden]
    I --> K{Hard-Limit überschritten?}
    J --> K
    K -- Ja -.-> K1[Preflight-Fehler protokollieren<br/>und Lauf abbrechen]
    K -- Nein --> L[Finalen Prompt senden]
    E --> L
    F --> L
    L --> M[Streaming verarbeiten]
    M --> N[Kontexteintrag mit Status anhängen]
    M -.-> O[Fehlerstatus setzen + Fehlereintrag]
```

---

## Schrittbeschreibung

1. **Folge-Prompt-Eingabe mit Modus-Guardrails**  
   - **Code:** `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`, `AufgabeDetail.razor.cs` (`FolgePromptAsync`)  
   - **Eingaben:** `_folgePrompt`, `_folgeAgentName`, `_folgeKontextmodus`, `_folgeKontextNeuBeginnenBestaetigt`  
   - **Ausgabe/Seiteneffekt:** Bei unbestätigtem „Kontext neu beginnen“ wird der Lauf blockiert und `_fehler` gesetzt.

2. **Hintergrundlauf starten und Session verwalten**  
   - **Code:** `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (`StartKiLauf`, `Subscribe`)  
   - **Eingaben:** Prompt, Agent, optionales Model, `FolgeanweisungsKontextmodus`  
   - **Ausgabe/Seiteneffekt:** Session wird im Singleton gehalten, Running-Count-Event ausgelöst, UI erhält Live-Stream.

3. **Default-KI-Plugin auflösen (Copilot priorisiert, Claude als Fallback)**  
   - **Code:** `src/Softwareschmiede/Program.cs` (DI für `IKiPlugin`), `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` (`GetDefaultDevelopmentAutomationPlugin`)  
   - **Eingaben:** Geladene Development-Automation-Plugins  
   - **Ausgabe/Seiteneffekt:** Nutzung von GitHub Copilot, sofern vorhanden; sonst z. B. Claude CLI.

4. **Kontextdateipfad provider-spezifisch bestimmen**  
   - **Code:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`ResolveContextFilePath`), `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`  
   - **Eingaben:** `localRepoPath`, `aufgabeId`, `ProviderDateiPraefix`  
   - **Ausgabe/Seiteneffekt:** Dateinamen wie `{id}.copilot.context.md` oder `{id}.claude.context.md`.

5. **Finalen Prompt je Kontextmodus aufbauen**  
   - **Code:** `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`BuildFollowPromptWithContextAsync`)  
   - **Eingaben:** Nutzerprompt, Modus, Kontextdateiinhalt  
   - **Ausgabe/Seiteneffekt:**  
     - `KontextNeuBeginnen`: Kontextdatei resetten, nur Nutzerprompt senden.  
     - `KontextIgnorieren`: Kontextdatei ignorieren, nur Nutzerprompt senden.  
     - `KontextMitgeben`: Kontext als Präfix voranstellen.

6. **Kontextlimits prüfen und ggf. KI-Komprimierung ausführen**  
   - **Code:** `EntwicklungsprozessService.cs` (`EnsureContextWithinLimitsAsync`, `CompressContextAsync`)  
   - **Eingaben:** Kontextinhalt, `KiKontext:SoftLimitChars`, `KiKontext:HardLimitChars`  
   - **Ausgabe/Seiteneffekt:** Bei Soft-Limit KI-Komprimierung und atomisches Speichern; bei Hard-Limit Abbruch vor KI-Start.

7. **KI-Ausführung streamen (Copilot/Claude CLI)**  
   - **Code:** `EntwicklungsprozessService.cs` (`KiStartenAsync`),  
     `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` (`StartDevelopmentAsync`),  
     `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs` (`StartDevelopmentAsync`)  
   - **Eingaben:** Finaler Prompt, Agent, Modell  
   - **Ausgabe/Seiteneffekt:** CLI-Streaming (`copilot` oder `claude`), Statuswechsel `KiAktiv` → `InBearbeitung` oder `Fehlgeschlagen`, Protokolleinträge.

8. **Kontextverlauf persistieren und UI zurücksetzen**  
   - **Code:** `EntwicklungsprozessService.cs` (`BuildContextEntry`, `AppendContextEntryAsync`), `AufgabeDetail.razor.cs` (Completion-Callback)  
   - **Eingaben:** `runId`, `contextEventId`, Modus, Antwort/Fehler  
   - **Ausgabe/Seiteneffekt:** Verlaufseintrag mit Status (`Erfolgreich`/`Fehler`) in Kontextdatei; UI setzt Folgeformular auf Standardwerte zurück.

---

## Fehlerbehandlung

- **Neu-beginnen ohne Bestätigung**  
  - **Pfad:** `AufgabeDetail.FolgePromptAsync`  
  - **Behandlung:** Sofortiger UI-Abbruch; kein Hintergrundlauf.

- **Preflight-Fehler vor KI-Start (z. B. Hard-Limit, ungültige Komprimierung)**  
  - **Pfad:** `EntwicklungsprozessService.KiStartenAsync` (`catch` um `BuildFollowPromptWithContextAsync`)  
  - **Behandlung:** Fehler als Kontexteintrag + Protokolleintrag mit `RunId`/`ContextEventId`; Exception wird propagiert.

- **Streamingfehler im KI-Plugin**  
  - **Pfad:** `KiStartenAsync` (Enumerator-Fehlerpfad)  
  - **Behandlung:** Aufgabe auf `Fehlgeschlagen`, Fehler-Markdown im Protokoll, Kontext-Fehlereintrag.

- **Dateilesefehler Kontextdatei**  
  - **Pfad:** `ReadFileTextSafeAsync`  
  - **Behandlung:** Fallback auf `.bak`; nur ohne Backup wird der Fehler weitergeworfen.

- **Dateischreibfehler bei Kontextpersistenz**  
  - **Pfad:** `WriteTextAtomicallyWithBackupAsync`  
  - **Behandlung:** Atomisches Schreiben via `.tmp` + `File.Replace`/`File.Move`; bei Fehler Exception an Aufrufer.

---

## Abhängigkeiten

- **UI / Komponenten**
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`

- **Application Services**
  - `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
  - `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
  - `src/Softwareschmiede/Application/Services/ProtokollService.cs`
  - `src/Softwareschmiede/Application/Services/AufgabeService.cs`

- **Plugin-Management / Verträge**
  - `src/Softwareschmiede/Program.cs`
  - `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
  - `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
  - `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`

- **Plugin-Implementierungen**
  - `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
  - `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`

---

## Verwandte Flows

- [Entwicklungsprozess-Abläufe](./development-process-flow.md#ablauf-2b-agent-auswahl-bei-folgeanweisungen)
- [Plugin-Settings-Service](./plugin-settings-service-flow.md)
- [AufgabeService Statusübergänge](./aufgabe-service-status-flow.md)
