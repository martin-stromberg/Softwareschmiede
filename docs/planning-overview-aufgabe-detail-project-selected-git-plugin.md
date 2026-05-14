# Planungsübersicht – AufgabeDetail: projektspezifisches Git-Plugin

## Primäre Anforderungsquelle
- User-Task: Korrektur der Plugin-Auswahl in `AufgabeDetail.razor.cs` / `GitOrchestrationService` inkl. Testabdeckung für Remote + LocalDirectory.

## Erzeugte Planungsdokumente
- Anforderungen: [requirements/aufgabe-detail-project-selected-git-plugin-requirements-analysis.md](requirements/aufgabe-detail-project-selected-git-plugin-requirements-analysis.md)
- Architektur: [architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md](architecture/aufgabe-detail-project-selected-git-plugin-architecture-blueprint.md)
- ERM: [architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md](architecture/aufgabe-detail-project-selected-git-plugin-entity-relationship-model.md)
- Review: [improvements/aufgabe-detail-project-selected-git-plugin-architecture-review.md](improvements/aufgabe-detail-project-selected-git-plugin-architecture-review.md)

## Kurzfazit
- Das effektive `IGitPlugin` muss je Aufgabe aus `GitRepository.PluginTyp` aufgelöst werden.
- `GitOrchestrationService` ist die zentrale Stelle für diese Auflösung.
- Testabdeckung wird praxisnah durch zwei Pflichtfälle erzwungen:
  1. Remote/GitHub
  2. LocalDirectoryPlugin

## Betroffene Dateien (Umsetzung)
- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Program.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`
- optional ergänzend: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`

## Wichtige Akzeptanzkriterien (Auszug)
- Bei GitHub-Repository wird GitHub-Plugin verwendet, unabhängig vom Default.
- Bei Local-Repository wird LocalDirectoryPlugin verwendet, unabhängig vom Default.
- Beide Tests sind automatisiert und grün.

