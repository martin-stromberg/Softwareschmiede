# Ablauf – GitOrchestrationService (Git-Aktionen, Issue-Import, PR-Auflösung)

## Titel & Kontext

Dieser Ablauf dokumentiert die zentrale Git-Orchestrierung in `GitOrchestrationService`.
Der Service kapselt die UI-nahen Git-Aktionen (Issues laden, Commit/Reset/Push/Pull, Pull Request erstellen, Merge ins Quellverzeichnis) und ergänzt sie um fachliche Guards sowie Protokolleinträge.
Für `LocalDirectoryPlugin` gelten dabei Fallback-Semantiken: Push als Datei-Synchronisation (`WorkingDirectory -> SourceDirectory`), Pull als No-Merge-Sync mit Hinweis und Delete-Sync über `git status --porcelain`.
Ein besonderer Fokus liegt auf der Repository-Auflösung für Pull Requests (Aufgaben-Repository vs. Projekt-Repository).
Zusätzlich liefert der Service Capability-Flags für die Aktionsmatrix in `AufgabeDetail` (`GetGitActionCapabilitiesAsync`).

---

## Diagramm A – Sequenz: Pull Request aus der Aufgabenansicht

```mermaid
sequenceDiagram
    actor U as Nutzer
    participant UI as AufgabeDetail
    participant GS as GitOrchestrationService
    participant AS as AufgabeService
    participant PS as ProjektService
    participant GP as IGitPlugin
    participant PR as ProtokollService

    U->>UI: "Pull Request erstellen"
    UI->>GS: PullRequestErstellenAsync(aufgabeId, title?, body?)
    GS->>AS: GetDetailAsync(aufgabeId)
    GS->>GS: ResolveRepositoryIdAsync(aufgabe)
    alt Aufgabe mit GitRepository verknüpft
        GS->>GS: ExtractRepositoryIdFromUrl(repositoryUrl)
    else Aufgabe ohne Verknüpfung
        GS->>PS: GetDetailAsync(projektId)
        GS->>GS: Aktive Repositories filtern
    end
    GS->>GP: CreatePullRequestAsync(repositoryId, branchName, title, body)
    GP-->>GS: PullRequest
    GS->>PR: AddEintragAsync(..., ProtokollTyp.GitAktion, "Pull Request erstellt ...")
    GS-->>UI: PullRequest
```

---

## Diagramm B – Programmablauf: Guards und Repository-Auflösung

```mermaid
flowchart TD
    A([Git-Orchestrierung aufgerufen]) --> B{Aktion = PullRequestErstellenAsync?}
    B -- Nein --> C[Aufgabe laden und Pflichtfelder prüfen]
    C --> D[IGitPlugin Aktion ausführen]
    D --> E[ProtokollService AddEintragAsync]
    E --> Z([Erfolg zurückgeben])

    B -- Ja --> F[Aufgabe-Detail laden]
    F --> G{BranchName gesetzt?}
    G -- Nein -.-> G1[InvalidOperationException]:::error
    G -- Ja --> H{GitRepository an Aufgabe?}
    H -- Ja --> I[RepositoryId aus RepositoryUrl extrahieren]
    H -- Nein --> J[Projekt laden und aktive Repositories ermitteln]
    J --> K{Genau 1 aktives Repository?}
    K -- Ja --> L[RepositoryId aus Projekt-Repository extrahieren]
    K -- Nein -.-> K1[InvalidOperationException]:::error
    I --> M[IGitPlugin CreatePullRequestAsync]
    L --> M
    M --> N[GitAktion protokollieren]
    N --> Z

    classDef error fill:#ffcccc,stroke:#cc0000,color:#333;
```

---

## Schrittbeschreibung

1. **Issue-Import für neue Aufgaben**
   - **Code:** `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs` (`IssuesAbrufenAsync`), `src/Softwareschmiede/Components/Pages/Aufgaben/NeueAufgabe.razor.cs` (`LadeIssuesAsync`)
   - **Eingaben:** `repositoryId` (`owner/repo`)
   - **Ausgaben/Seiteneffekte:** Rückgabe einer `Issue`-Liste; UI füllt Auswahlliste für `CreateFromIssueAsync`.

2. **Gemeinsamer Guard für Git-Aktionen auf Aufgaben**
   - **Code:** `GitOrchestrationService.CommitAsync`, `ResetAsync`, `PushAsync`, `PullAsync`
   - **Eingaben:** `aufgabeId`, aktionsspezifische Parameter (`message`, `resetType`, `targetRef`)
   - **Ausgaben/Seiteneffekte:** Aufgabe wird geladen; fehlender `LokalerKlonPfad` oder fehlender `BranchName` (bei Push) führt zum Abbruch per Exception.

3. **Ausführung der Plugin-Operation**
   - **Code:** `GitOrchestrationService.*` → `IGitPlugin` (`CommitAsync`, `ResetAsync`, `PushBranchAsync`, `PullAsync`, `CreatePullRequestAsync`)
   - **Eingaben:** Lokaler Repository-Pfad, Branch, PR-Metadaten
   - **Ausgaben/Seiteneffekte:** Plugin-Aktion läuft im Ziel-Workspace; je Plugin entweder Remote-Git (`git push`/`git pull`) oder Dateisynchronisation (inkl. Delete-Sync via `git status --porcelain`).

4. **Protokollierung jeder Git-Aktion**
   - **Code:** `GitOrchestrationService.*` → `ProtokollService.AddEintragAsync`
   - **Eingaben:** `aufgabeId`, `ProtokollTyp.GitAktion`, formatierter Text
   - **Ausgaben/Seiteneffekte:** Persistenter Audit-Log in der Aufgabenhistorie; bei LocalDirectory-Pull expliziter Hinweis „kein Merge, Dateisynchronisation“.

5. **Repository-Auflösung für Pull Requests**
   - **Code:** `GitOrchestrationService.PullRequestErstellenAsync`, `ResolveRepositoryIdAsync`, `ExtractRepositoryIdFromUrl`
   - **Eingaben:** Aufgabe inkl. optionaler `GitRepository`-Verknüpfung
   - **Ausgaben/Seiteneffekte:** Eindeutige `repositoryId`; bei fehlender oder mehrdeutiger Projektzuordnung wird kontrolliert abgebrochen.

6. **PR-Body-Aufbau mit Issue-Closing-Direktive**
   - **Code:** `GitOrchestrationService.BuildPullRequestBody`, `ContainsClosingDirectiveForIssue`
   - **Eingaben:** bestehender PR-Body + optionale `IssueReferenz.IssueNummer`
   - **Ausgaben/Seiteneffekte:** Ergänzt `Closes #<IssueNummer>`, wenn für die aktuelle Issue noch keine Closing-Direktive vorhanden ist; bei Whitespace-Body wird nur die Direktive verwendet.

7. **UI-Integration für Commit/Push/Pull/Reset/PR/Merge**
   - **Code:** `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs` (`LadeGitActionCapabilitiesAsync`, `EvaluateGitActionVisibility`, `CommitAsync`, `PushAsync`, `PullAsync`, `MergeToSourceAsync`, `ResetAsync`, `PullRequestErstellenAsync`)
   - **Eingaben:** Formulardaten in der Aufgabenansicht
   - **Ausgaben/Seiteneffekte:** Erfolg-/Fehlermeldungen in der UI, anschließendes Reload via `LadeAsync`; bei `LocalDirectory + IsWorkingDirectoryCopy` werden Push/Pull/PR ausgeblendet und Merge sichtbar.

8. **Projektspezifische Plugin-Auflösung für Aufgabenaktionen**
   - **Code:** `GitOrchestrationService.ResolveGitPluginAsync`, `ResolveSelectedPluginPrefixAsync`, `PluginSelectionService.ResolveSourceCodeManagementPluginAsync`
   - **Eingaben:** `Aufgabe.GitRepository.PluginTyp`, aktive Projekt-Repositories, gespeichertes Standardplugin
   - **Ausgaben/Seiteneffekte:** Das effektive `IGitPlugin` wird pro Aktion neu aufgelöst. Ohne Aufgaben-Repository wird bei genau einem aktiven Projekt-Repository dessen `PluginTyp` genutzt (inkl. LocalRepository/`LocalDirectoryPlugin`); bei Mehrdeutigkeit greift der definierte Standard-Fallback.
   - **Tests:** `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs` und `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`.

---

## Fehlerbehandlung

- **Aufgabe nicht gefunden**
  - Pfad: alle Methoden mit `GetByIdAsync`/`GetDetailAsync`
  - Behandlung: `InvalidOperationException`, UI zeigt Fehlermeldung.

- **Fehlender Klonpfad oder Branch**
  - Pfad: `CommitAsync`/`ResetAsync`/`PullAsync`/`PushAsync`/`PullRequestErstellenAsync`
  - Behandlung: `InvalidOperationException`; keine Plugin-Aktion wird gestartet.

- **Mehrdeutige oder fehlende Projekt-Repositories bei PR**
  - Pfad: `ResolveRepositoryIdAsync`
  - Behandlung: `InvalidOperationException` mit konkretem Hinweis auf notwendige Repository-Verknüpfung.

- **Ungültige Repository-URL**
  - Pfad: `ExtractRepositoryIdFromUrl`
  - Behandlung: `InvalidOperationException`; PR-Erstellung wird abgebrochen.

- **Plugin-/Netzwerk-/CLI-Fehler**
  - Pfad: `IGitPlugin`-Aufrufe
  - Behandlung: Exception propagiert; Aufrufseite (`AufgabeDetail`/`NeueAufgabe`) setzt UI-Fehlermeldung.

- **Pull im LocalDirectory-Workspace mit Merge-Erwartung**
  - Pfad: `GitOrchestrationService.PullAsync` + `LocalDirectoryPlugin.PullAsync`
  - Behandlung: Kein Merge; stattdessen Hinweis-Log und Dateisynchronisation `SourceDirectory -> WorkingDirectory`.

---

## Abhängigkeiten

- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Application/Services/ProtokollService.cs`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Components/Pages/Aufgaben/NeueAufgabe.razor.cs`
- `src/Softwareschmiede/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`

> Verwandte Flows: [Entwicklungsprozess-Abläufe](./development-process-flow.md) · [ProjektService](./projekt-service-flow.md) · [KI-Ausführung im Hintergrund](./ki-ausfuehrungs-service-flow.md)

---

## Bekannte Einschränkungen / nächste Schritte

- Für `LocalDirectoryPlugin` sind Push/Pull bewusst lokale Datei-Synchronisationen; Remote-Operationen bleiben ausgeschlossen.
- Pull im lokalen Workspace führt keinen Merge aus und bricht bei dirty Workspace kontrolliert ab.
- Als Restpunkt ist ein UI-seitiger Pull-Bestätigungsdialog in `AufgabeDetail` dokumentiert, aber noch nicht automatisiert getestet.
- Bei mehreren aktiven Projekt-Repositories ohne Aufgabenverknüpfung nutzt die Plugin-Auflösung bewusst den Standard-Fallback statt einer impliziten Auswahl.
