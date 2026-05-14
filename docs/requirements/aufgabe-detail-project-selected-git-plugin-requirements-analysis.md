# Anforderungsanalyse – AufgabeDetail: projektspezifisches Git-Plugin in GitOrchestrationService

> **Dokument-Typ:** Requirements Analysis  
> **Status:** Geplant  
> **Version:** 1.0.0

---

## 1. Kontext

In `AufgabeDetail.razor.cs` wird `GitOrchestrationService` per DI verwendet.  
`GitOrchestrationService` arbeitet aktuell mit einem injizierten `IGitPlugin` und berücksichtigt dadurch nicht zuverlässig das im Aufgaben-/Projektkontext ausgewählte Repository-Plugin (`GitRepository.PluginTyp`).

Der bestehende Test  
`AufgabeDetail_ShouldUseProjectSelectedGitPlugin_InInjectedGitOrchestrationService`  
ist nicht praxisnah, da die Auswahl faktisch fest auf GitHub verdrahtet ist. Zusätzlich fehlt ein expliziter Testfall für `LocalDirectoryPlugin`.

---

## 2. Referenzen

- Architektur: [`../architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md`](../architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md)
- ERM: [`../architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md`](../architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md)
- Review: [`../improvements/aufgabe-detail-project-selected-git-plugin-architecture-review.md`](../improvements/aufgabe-detail-project-selected-git-plugin-architecture-review.md)
- Übersicht: [`../planning-overview-aufgabe-detail-project-selected-git-plugin.md`](../planning-overview-aufgabe-detail-project-selected-git-plugin.md)

---

## 3. Funktionale Anforderungen

| Kennung | Anforderung | Priorität |
|---|---|---|
| FR-1 | `GitOrchestrationService` verwendet pro Git-Aktion das aus Aufgabe/Repository aufgelöste Plugin (`GitRepository.PluginTyp`) statt pauschal des DI-Defaults. | MUST |
| FR-2 | Die Auflösung ist für alle relevanten Aktionen konsistent (`GetGitActionCapabilitiesAsync`, `CommitAsync`, `PushAsync`, `PullAsync`, `ResetAsync`, `MergeToSourceAsync`, `PullRequestErstellenAsync`). | MUST |
| FR-3 | Fallback-Regel ist deterministisch, wenn `Aufgabe.GitRepositoryId` fehlt (z. B. genau ein aktives Projekt-Repository, sonst kontrollierter Abbruch/Fallback). | MUST |
| FR-4 | Der bestehende praxisferne Test wird in einen verhaltensnahen Test umgebaut (Remote/GitHub-Fall mit konkurrierendem Default). | MUST |
| FR-5 | Ein zweiter Testfall für `LocalDirectoryPlugin` wird ergänzt. | MUST |

---

## 4. Nicht-funktionale Anforderungen

| Kennung | Anforderung | Priorität |
|---|---|---|
| NFR-1 | Plugin-Auflösung ist nachvollziehbar (Logging mit PluginPrefix/Quelle, ohne Secrets). | HIGH |
| NFR-2 | Keine Datenbankschemaänderung erforderlich. | MUST |
| NFR-3 | Keine UI-Logikduplikation in `AufgabeDetail`; Auflösung bleibt in der Application-Schicht. | MUST |
| NFR-4 | Bestehende Git-Workflows bleiben rückwärtskompatibel bei Default/Fallback. | HIGH |

---

## 5. Akzeptanzkriterien

- AC-1: Bei `GitRepository.PluginTyp = "Softwareschmiede.GitHub"` wird für Git-Aktionen effektiv das GitHub-Plugin verwendet, auch wenn Default = `LocalDirectoryPlugin`.
- AC-2: Bei `GitRepository.PluginTyp = "LocalDirectoryPlugin"` wird effektiv das lokale Plugin verwendet, auch wenn Default = `Softwareschmiede.GitHub`.
- AC-3: Beide Testfälle sind automatisiert und grün.
- AC-4: Der bisherige Test ist nicht mehr auf eine künstlich fest verdrahtete Plugininstanz aufgebaut, sondern prüft tatsächliches Laufzeitverhalten.
- AC-5: Bei fehlender eindeutiger Repository-Zuordnung ist das Verhalten deterministisch und fachlich dokumentiert.
- AC-6: Keine Migration / keine neuen Tabellen oder Spalten.

---

## 6. Scope

**In Scope**
- Plan für Korrektur der Plugin-Auflösung in `GitOrchestrationService`
- Testabdeckung für Remote/GitHub + LocalDirectory
- Konsolidierte Dokumentation inkl. Review

**Out of Scope**
- Neue Plugin-Typen
- Größeres UI-Redesign
- Änderung des Datenbankschemas

---

## 7. Betroffene Dateien

- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Program.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`
- ggf. ergänzend: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`

---

## 8. Risiken und Annahmen

**Risiken**
- Falsches Plugin bei gemischten Projekt-Repositories
- Scheinbar grüne, aber praxisferne Tests
- Regression in Capabilities-/Button-Logik

**Annahmen**
- `GitRepository.PluginTyp` bleibt fachliche Hauptquelle
- `PluginSelectionService` bleibt zentrale Resolver-Komponente

