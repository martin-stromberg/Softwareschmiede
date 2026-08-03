# Logik & Services

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|------------|------------------|
| `ProzessStartenAsync()` | öffentlich | Richtet Git-Repository für Aufgabe ein: Klon, Branch, optionales Startskript. Nimmt optionalen `basisBranchName`-Parameter. |
| `ProzessStartenUndCliStartenAsync()` | öffentlich | Kombiniert Repository-Setup und CLI-Start in einem Schritt. |
| `RepositoryStartskriptAusfuehrenAsync()` | öffentlich | Führt Startskript aus, falls konfiguriert. |
| `SetupBranchAsync()` | privat | Erstellt oder checkt einen Branch. Wenn `basisBranchName` vorhanden und nicht Default-Branch: `CheckoutRemoteBranchAsync`. Ansonsten: `CreateBranchAsync` auf lokalem Head. |
| `PrepareCloneDirectoryAsync()` | privat | Bereitet Klon-Verzeichnis vor und klont Repository. |
| `FinalizeStartAsync()` | privat | Führt Startskript aus, erstellt Issue-Datei, aktualisiert .gitignore. |
| `ResolvePluginAsync()` | privat | Ermittelt Git-Plugin basierend auf Repository-Konfiguration oder Default. |
| `ResolveRepositoryAsync()` | privat | Ermittelt Repository-Entität. |
| `RollbackStartAsync()` | privat | Rollback im Fehlerfall: löscht Klon-Verzeichnis und setzt Status zurück. |

**Wichtig:** `SetupBranchAsync()` erstellt neue Branches OHNE Angabe eines Basis-Branch. Feature-Branch wird vom aktuellen HEAD erstellt, nicht vom konfigurierten Basis-Branch.

## `GitOrchestrationService`
Datei: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|------------|------------------|
| `PullRequestErstellenAsync()` | öffentlich | Erstellt PR mit optional übergebenem Title und Body. Ruft `gitPlugin.CreatePullRequestAsync()` auf. |
| `CommitAsync()` | öffentlich | Führt Commit durch und protokolliert Aktion. |
| `PushAsync()` | öffentlich | Pusht Branch auf Remote. |
| `PullAsync()` | öffentlich | Holt Änderungen vom Remote. |
| `ResetAsync()` | öffentlich | Setzt Commits zurück. |
| `MergeToSourceAsync()` | öffentlich | Übernimmt Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis. |
| `GetGitActionCapabilitiesAsync()` | öffentlich | Liefert Git-Aktions-Capabilities des Plugins. |
| `ValidateWorkingDirectoryAfterCloneAsync()` | öffentlich | Validiert Arbeitsverzeichnis nach Git-Klon. |
| `ResolveRepositoryIdAsync()` | privat | Ermittelt Repository-ID aus Aufgabe oder Projekt. |
| `ResolveGitPluginAsync()` | privat | Ermittelt Git-Plugin für Aufgabe. |
| `ExtractRepositoryIdFromUrl()` | privat (static) | Extrahiert Owner/Repo aus URL. |

**Wichtig:** `PullRequestErstellenAsync()` übergibt `aufgabe.BranchName` an `CreatePullRequestAsync()`, aber NICHT den Basis-Branch als Ziel-Branch.

## `RepositoryStartskriptService`
Datei: `src/Softwareschmiede/Application/Services/RepositoryStartskriptService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|------------|------------------|
| `RunAsync()` | öffentlich | Führt das konfigurierte Startskript über PowerShell aus. Validiert Skriptpfad, prüft Aktiv-Flag. |
| `ResolveScriptPath()` | privat (static) | Löst Skriptpfad auf und validiert, dass er innerhalb des Repositorys liegt. |
| `BuildArguments()` | privat (static) | Erstellt PowerShell-Argumente für Skriptausführung. |

## `PullRequestReferenzService`
Datei: `src/Softwareschmiede/Application/Services/PullRequestReferenzService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|------------|------------------|
| `SaveCreatedAsync()` | öffentlich | Speichert oder aktualisiert erstellten PR in DB. Setzt `TargetBranch` aus PR-Objekt. |
| `GetByAufgabeAsync()` | öffentlich | Ruft PRs einer Aufgabe ab. |
| `GetDueForMonitoringAsync()` | öffentlich | Ruft PRs ab, die überwacht werden sollen. |
| `GetRefreshableByAufgabeAsync()` | öffentlich | Ruft alle PRs einer Aufgabe für Refresh ab. |
| `UpdateFromProviderAsync()` | öffentlich | Aktualisiert PR-Status und Workflow-Runs. |
| `SetProviderUncertaintyAsync()` | öffentlich | Speichert Provider-Unsicherheit. |
| `SetRetryableErrorAsync()` | öffentlich | Speichert retryfähigen Fehler. |
| `SetProblemAsync()` | öffentlich | Speichert blockierten oder fehlgeschlagenen Zustand. |
| `SetPhaseAsync()` | öffentlich | Speichert Monitoring-Phase ohne Fehlerzustand. |

**Wichtig:** `SaveCreatedAsync()` speichert `TargetBranch` aus dem PR-Objekt, das vom Plugin zurückkommt. Kein Bezug zu konfiguriertem Basis-Branch.
