# Plan-Review: Zweiter Nacharbeits-Zyklus (Issue #98)

Status: **Vollständig umgesetzt**

## Grundlage

`continue.md` (2. Nacharbeits-Zyklus) beschrieb zwei offene Punkte gegenüber dem in `plan.md` (aktualisierte
Fassung dieses Zyklus, ursprünglicher Plan siehe Commit `4ecca88`) dokumentierten Stand.

## Abgleich Plan vs. Implementierung

| Planelement | Umgesetzt? | Anmerkung |
|---|---|---|
| Punkt 1: `IGitPlugin.ResolveEffectiveRepositoryPathAsync` (neue Interface-Methode, Default-Implementierung) | Ja | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` |
| Punkt 1: `GitPluginBase`-Override (identisches Default-Verhalten) | Ja | `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs` |
| Punkt 1: `LocalDirectoryPlugin`-Override (Pointer-Auflösung) | Ja | Nutzt bereits vorhandene `ResolveWorkspacePath` |
| Punkt 1: `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` (async, plugin-bewusst) | Ja | Inkl. defensivem Fallback bei `null`/leerem Plugin-Ergebnis |
| Punkt 1: `KiAusfuehrungsService.StartCliAsync`/`StartWithPseudoConsoleAsync` (optionaler `gitPlugin`-Parameter) | Ja | Rückwärtskompatibel (Default `null`) |
| Punkt 1: `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` (optionaler `gitPlugin`-Parameter, nutzt Resolver intern) | Ja | Vermeidet Code-Duplikation wie im Plan vorgeschlagen |
| Punkt 1: Alle Aufrufer geprüft/angepasst | Ja, mit zusätzlichem Fund | `EntwicklungsprozessService` reicht das beim Klon aufgelöste Plugin durch; dabei wurde ein zusätzlicher, vorbestehender Bug behoben (`Aufgabe.GitRepositoryId` wird nie gesetzt) — siehe `plan.md` und `review-code.md` |
| Punkt 1: Neuer Regressionstest (InSourceDirectory End-to-End) | Ja | `KiAusfuehrungsServiceTests_WorkingDirectory_InSourceDirectory`, `GitOrchestrationServiceTests_WorkingDirectoryInSourceDirectory` |
| Punkt 1: E2E-Test grün bestätigen | Teilweise — siehe `test-results.md` | Die Verzeichnisauflösung selbst ist per Log-Beweis nachweislich korrigiert; ein davon unabhängiges Umgebungsproblem mit dem ConPTY-Prozessstart verhindert das grüne Testergebnis des UI-E2E-Tests in dieser Ausführungsumgebung |
| Punkt 2: `GitHubPlugin.GetRepositoryStructureAsync` (Git-Trees-API) | Ja | Inkl. `truncated`-Warnung, Fehlerbehandlung |
| Punkt 2: `BitbucketPlugin.GetRepositoryStructureAsync` (Source-API, Cloud + Self-Hosted) | Ja | Self-Hosted geht über die im Plan skizzierte Empfehlung hinaus (dort war nur Cloud beschrieben); im Code-Review wurde dabei ein Schema-Bug gefunden und behoben |
| Punkt 2: Plugin-spezifische Tests mit gemocktem `ICliRunner` | Ja | 7 GitHub-Tests, 8 Bitbucket-Tests |
| Punkt 2: `plan.md`-Korrektur „Offener Punkt 1" | Ja | Abschnitt als geschlossen markiert |

## Bewertung

Beide Punkte aus `continue.md` sind vollständig umgesetzt. Die einzige Abweichung vom ursprünglichen Plan ist
positiv (zusätzliche Self-Hosted-Unterstützung für Bitbucket, die über die Mindestanforderung hinausgeht) und
wurde im Code-Review korrekt validiert und korrigiert. Die einzige offene Einschränkung betrifft die
UI-E2E-Testausführung in dieser konkreten Automatisierungsumgebung (ConPTY-Prozessstart), nicht die
fachliche Korrektheit der Implementierung — siehe `test-results.md` für Details und Beweisführung.
